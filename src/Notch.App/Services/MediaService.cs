using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using GsmtcManager = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;
using GsmtcSession = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;

namespace Notch.App.Services;

/// <summary>Música tocando em qualquer player (Spotify, YouTube no navegador, etc.) via controles de mídia do Windows.</summary>
internal sealed class MediaService(Dispatcher dispatcher)
{
    private GsmtcManager? _manager;
    private GsmtcSession? _session;
    private TimeSpan _position;
    private DateTimeOffset _positionUpdated;

    public string Title { get; private set; } = "";
    public string Artist { get; private set; } = "";
    public string Album { get; private set; } = "";
    public ImageSource? Art { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool HasSession => _session != null && Title.Length > 0;
    public TimeSpan Duration { get; private set; }
    public string SourceApp { get; private set; } = "";

    /// <summary>Mudou faixa, capa ou estado de reprodução.</summary>
    public event Action? Changed;

    /// <summary>Mudou só a faixa (título/artista).</summary>
    public event Action? TrackChanged;

    public TimeSpan Position
    {
        get
        {
            var p = _position;
            if (IsPlaying) p += DateTimeOffset.Now - _positionUpdated;
            if (Duration > TimeSpan.Zero && p > Duration) p = Duration;
            return p < TimeSpan.Zero ? TimeSpan.Zero : p;
        }
    }

    public async Task StartAsync()
    {
        try
        {
            _manager = await GsmtcManager.RequestAsync();
            _manager.CurrentSessionChanged += (_, _) => dispatcher.BeginInvoke(() => Hook(_manager.GetCurrentSession()));
            Hook(_manager.GetCurrentSession());
        }
        catch (Exception ex)
        {
            Core.Paths.WriteLog("Mídia indisponível: " + ex.Message);
        }
    }

    private void Hook(GsmtcSession? session)
    {
        if (_session != null)
        {
            _session.MediaPropertiesChanged -= OnProps;
            _session.PlaybackInfoChanged -= OnPlayback;
            _session.TimelinePropertiesChanged -= OnTimeline;
        }
        _session = session;
        if (session == null)
        {
            Title = Artist = Album = SourceApp = "";
            Art = null;
            IsPlaying = false;
            Changed?.Invoke();
            TrackChanged?.Invoke();
            return;
        }
        SourceApp = session.SourceAppUserModelId;
        session.MediaPropertiesChanged += OnProps;
        session.PlaybackInfoChanged += OnPlayback;
        session.TimelinePropertiesChanged += OnTimeline;
        _ = RefreshPropsAsync(session);
        RefreshPlayback(session);
        RefreshTimeline(session);
    }

    private void OnProps(GsmtcSession s, MediaPropertiesChangedEventArgs e) => dispatcher.BeginInvoke(() => _ = RefreshPropsAsync(s));
    private void OnPlayback(GsmtcSession s, PlaybackInfoChangedEventArgs e) => dispatcher.BeginInvoke(() => RefreshPlayback(s));
    private void OnTimeline(GsmtcSession s, TimelinePropertiesChangedEventArgs e) => dispatcher.BeginInvoke(() => RefreshTimeline(s));

    private async Task RefreshPropsAsync(GsmtcSession s)
    {
        try
        {
            var props = await s.TryGetMediaPropertiesAsync();
            if (s != _session || props == null) return;
            var changedTrack = props.Title != Title || props.Artist != Artist;
            Title = props.Title ?? "";
            Artist = string.IsNullOrEmpty(props.Artist) ? props.AlbumArtist ?? "" : props.Artist;
            Album = props.AlbumTitle ?? "";
            Art = null;
            if (props.Thumbnail != null)
            {
                using var ras = await props.Thumbnail.OpenReadAsync();
                using var stream = ras.AsStreamForRead();
                var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                ms.Position = 0;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 160;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                if (s == _session) Art = bmp;
            }
            RefreshTimeline(s);
            Changed?.Invoke();
            if (changedTrack) TrackChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Core.Paths.WriteLog("Falha ao ler mídia: " + ex.Message);
        }
    }

    private void RefreshPlayback(GsmtcSession s)
    {
        if (s != _session) return;
        var wasPlaying = IsPlaying;
        var keep = Position;
        IsPlaying = s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        if (wasPlaying != IsPlaying)
        {
            _position = keep;
            _positionUpdated = DateTimeOffset.Now;
        }
        Changed?.Invoke();
    }

    private void RefreshTimeline(GsmtcSession s)
    {
        if (s != _session) return;
        var t = s.GetTimelineProperties();
        if (t == null) return;
        Duration = t.EndTime - t.StartTime;
        _position = t.Position;
        _positionUpdated = t.LastUpdatedTime.Year < 2000 ? DateTimeOffset.Now : t.LastUpdatedTime;
    }

    public async void PlayPause() { try { if (_session != null) await _session.TryTogglePlayPauseAsync(); } catch { } }
    public async void Next() { try { if (_session != null) await _session.TrySkipNextAsync(); } catch { } }
    public async void Previous() { try { if (_session != null) await _session.TrySkipPreviousAsync(); } catch { } }

    public async void Seek(TimeSpan position)
    {
        try
        {
            if (_session == null) return;
            if (await _session.TryChangePlaybackPositionAsync(position.Ticks))
            {
                _position = position;
                _positionUpdated = DateTimeOffset.Now;
            }
        }
        catch { }
    }
}
