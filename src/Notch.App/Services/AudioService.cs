using System.Windows.Threading;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Notch.App.Services;

/// <summary>Volume do Windows (alerta ao mudar) e nível do som para as barrinhas.</summary>
internal sealed class AudioService : IMMNotificationClient
{
    private readonly Dispatcher _dispatcher;
    private readonly MMDeviceEnumerator _enumerator = new();
    private MMDevice? _device;
    private MMDevice? _mic;
    private float _lastVolume = -1;
    private bool _lastMute;

    /// <summary>(volume 0..1, mudo)</summary>
    public event Action<float, bool>? VolumeChanged;

    /// <summary>Microfone padrão foi mutado/desmutado (por aqui ou por outro app).</summary>
    public event Action<bool>? MicChanged;

    public bool HasMic => _mic != null;

    public bool MicMuted { get; private set; }

    public AudioService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        try { _enumerator.RegisterEndpointNotificationCallback(this); } catch { }
        Attach();
        AttachMic();
    }

    /// <summary>Microfone padrão de comunicação (o mesmo que Teams, Zoom e Meet usam).</summary>
    private void AttachMic()
    {
        try
        {
            if (_mic != null) _mic.AudioEndpointVolume.OnVolumeNotification -= OnMicVolume;
            _mic = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            MicMuted = _mic.AudioEndpointVolume.Mute;
            _mic.AudioEndpointVolume.OnVolumeNotification += OnMicVolume;
        }
        catch (Exception ex)
        {
            if (_mic != null) Core.Paths.WriteLog("Microfone desconectado: " + ex.Message);
            _mic = null;
            MicMuted = false;
        }
        MicChanged?.Invoke(MicMuted);
    }

    private void OnMicVolume(AudioVolumeNotificationData data)
    {
        if (data.Muted == MicMuted) return;
        _dispatcher.BeginInvoke(() =>
        {
            MicMuted = data.Muted;
            MicChanged?.Invoke(MicMuted);
        });
    }

    /// <summary>
    /// Confere o estado real no Windows. Alguns headsets (ex.: Astro A50) nem sempre avisam
    /// as mudanças por evento; chamada a cada segundo, garante que o indicador nunca minta.
    /// </summary>
    public void SyncMic()
    {
        try
        {
            var current = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            if (_mic == null || current.ID != _mic.ID)
            {
                AttachMic(); // trocou o microfone padrão ou um foi conectado
                return;
            }
            var muted = _mic.AudioEndpointVolume.Mute;
            if (muted == MicMuted) return;
            MicMuted = muted;
            MicChanged?.Invoke(muted);
        }
        catch (Exception)
        {
            if (_mic == null) return;
            AttachMic(); // microfone removido
        }
    }

    /// <summary>Muta ou libera o microfone. Retorna o novo estado (true = mudo).</summary>
    public bool ToggleMic()
    {
        if (_mic == null) AttachMic();
        if (_mic == null) return false;
        try
        {
            var muted = !_mic.AudioEndpointVolume.Mute;
            _mic.AudioEndpointVolume.Mute = muted;
            MicMuted = muted;
            MicChanged?.Invoke(muted);
        }
        catch (Exception ex)
        {
            Core.Paths.WriteLog("Falha ao mutar o microfone: " + ex.Message);
        }
        return MicMuted;
    }

    private void Attach()
    {
        try
        {
            if (_device != null) _device.AudioEndpointVolume.OnVolumeNotification -= OnVolume;
            _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _lastVolume = _device.AudioEndpointVolume.MasterVolumeLevelScalar;
            _lastMute = _device.AudioEndpointVolume.Mute;
            _device.AudioEndpointVolume.OnVolumeNotification += OnVolume;
        }
        catch (Exception ex)
        {
            _device = null;
            Core.Paths.WriteLog("Áudio indisponível: " + ex.Message);
        }
    }

    private void OnVolume(AudioVolumeNotificationData data)
    {
        // ignora notificações repetidas (alguns drivers mandam várias)
        if (Math.Abs(data.MasterVolume - _lastVolume) < 0.004 && data.Muted == _lastMute) return;
        _lastVolume = data.MasterVolume;
        _lastMute = data.Muted;
        _dispatcher.BeginInvoke(() => VolumeChanged?.Invoke(data.MasterVolume, data.Muted));
    }

    public float Peak()
    {
        try { return _device?.AudioMeterInformation.MasterPeakValue ?? 0; }
        catch { return 0; }
    }

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow == DataFlow.Render && role == Role.Multimedia) _dispatcher.BeginInvoke(Attach);
        if (flow == DataFlow.Capture && role == Role.Communications) _dispatcher.BeginInvoke(AttachMic);
    }

    public void OnDeviceStateChanged(string deviceId, DeviceState newState)
    {
        // microfone (fone USB/Bluetooth) conectado ou removido
        _dispatcher.BeginInvoke(AttachMic);
    }
    public void OnDeviceAdded(string pwstrDeviceId) => _dispatcher.BeginInvoke(AttachMic);
    public void OnDeviceRemoved(string deviceId) { }
    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
}
