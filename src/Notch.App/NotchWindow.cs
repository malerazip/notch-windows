using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Notch.App.Controls;
using Notch.App.Core;
using Notch.App.Modules;
using Notch.App.Services;
using Notch.Sdk;
using Glyphs = Notch.Sdk.Glyphs;

namespace Notch.App;

/// <summary>
/// A janela do notch: uma pílula preta no topo da tela que cresce para mostrar alertas
/// e se expande (ao clicar, ou ao passar o mouse se a opção estiver ligada) num painel com páginas de ferramentas.
/// </summary>
internal sealed partial class NotchWindow : Window, INotchHost
{
    private enum State { Compact, Alert, Expanded }

    private sealed class Page(INotchModule module)
    {
        public INotchModule Module { get; } = module;
        public FrameworkElement? View { get; set; }
    }

    private static readonly CultureInfo Br = new("pt-BR");
    private static readonly string[] Days = ["Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb"];
    private static readonly string[] Months = ["jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez"];

    private const double ExpandedW = 450, ExpandedH = 178;
    private const double AlertH = 64;

    private readonly HttpClient _http;
    private readonly KeyValueFile _settings;
    private readonly MediaService _media;
    private readonly AudioService _audio;
    private readonly SystemStatusService _status;
    private readonly WindowsNotificationService _winNotif;
    private readonly PomodoroModule _pomodoro = new();
    private readonly ShelfModule _shelf = new();
    private readonly LyricsModule _lyricsModule;
    private readonly List<INotchModule> _modules = new();
    private readonly List<Page> _pages = new();
    private readonly Queue<NotchAlert> _alerts = new();

    private readonly DispatcherTimer _alertTimer = new();
    private readonly DispatcherTimer _hoverTimer = new() { Interval = TimeSpan.FromMilliseconds(140) };
    private readonly DispatcherTimer _leaveTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _guardTimer = new() { Interval = TimeSpan.FromSeconds(1.5) };

    private State _state = State.Compact;
    private NotchAlert? _currentAlert;
    private int _pageIndex;
    private nint _hwnd;
    private bool _dragging;
    private bool _hiddenForFullscreen;
    private DateTime _lastWheel;

    // elementos visuais
    private readonly Grid _root = new();
    private readonly Border _shell = new();
    private readonly Grid _compactLayer = new();
    private readonly Grid _alertLayer = new();
    private readonly Grid _expandedLayer = new();
    private readonly TextBlock _compactTime = Ui.Text("", 13, FontWeights.SemiBold);
    private readonly BatteryIcon _compactBattery = new();
    private readonly Border _compactArt = new() { Width = 20, Height = 20, CornerRadius = new CornerRadius(5) };
    private readonly TextBlock _compactTimerIcon = Ui.Icon(Glyphs.Timer, 13);
    private readonly TextBlock _compactMicIcon = Ui.Icon(Glyphs.Microphone, 13);
    private readonly TextBlock _compactCam = Ui.Icon(Glyphs.Webcam, 13, NotchTheme.Green);
    private readonly TextBlock _headerCam = Ui.Icon(Glyphs.Webcam, 13, NotchTheme.Green);
    private bool _cameraOn;
    private readonly CameraService _camera = new();
    private readonly Border _compactMic = new() { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = Brushes.Transparent, Cursor = Cursors.Hand };
    private Border _micButton = null!;
    private bool? _lastMicMuted;
    private readonly AudioBars _compactBars = new();
    private readonly Mascot _compactMascot = new();
    private readonly TextBlock _title = new() { FontFamily = NotchTheme.Font, FontSize = 15, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
    private readonly BatteryIcon _headerBattery = new();
    private readonly TextBlock _headerDate = Ui.Text("", 12.5, FontWeights.SemiBold);
    private readonly TextBlock _headerTime = Ui.Text("", 12.5, FontWeights.Bold);
    private readonly AudioBars _headerBars = new();
    private readonly Mascot _headerMascot = new();
    private readonly ContentControl _pageHost = new();
    private readonly StackPanel _dots = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

    public NotchWindow()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"ProjetoNotch/{Paths.AppVersion} (Windows; +https://lrclib.net)");
        _settings = OpenStore("settings");

        _media = new MediaService(Dispatcher);
        _audio = new AudioService(Dispatcher);
        _status = new SystemStatusService(Dispatcher, ShowAlert, Setting);
        _winNotif = new WindowsNotificationService(Dispatcher, ShowAlert);
        _lyricsModule = new LyricsModule(_media, new LyricsService(_http));
        AudioBars.PeakSource = _audio.Peak;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Title = Paths.AppName;
        Width = 560;
        Height = 280;
        Top = 0;
        AllowDrop = true;
        FontFamily = NotchTheme.Font;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        BuildShell();
        Content = _root;

        LoadModules();
        WireEvents();
        Reposition();
        SetState(State.Compact, animate: false);
        UpdateClock();
        UpdateCompact();
    }

    // ------------------------------------------------------------------ construção

    private void BuildShell()
    {
        _shell.Background = NotchTheme.Background;
        _shell.HorizontalAlignment = HorizontalAlignment.Center;
        _shell.VerticalAlignment = VerticalAlignment.Top;
        _shell.ClipToBounds = true;
        _shell.SnapsToDevicePixels = true;
        _shell.Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 2, Direction = 270, Opacity = 0.45, Color = Colors.Black };

        var layers = new Grid();
        layers.Children.Add(_compactLayer);
        layers.Children.Add(_alertLayer);
        layers.Children.Add(_expandedLayer);
        _shell.Child = layers;
        _root.Children.Add(_shell);

        BuildCompact();
        BuildExpanded();
        _alertLayer.Height = AlertH;
        _alertLayer.VerticalAlignment = VerticalAlignment.Top;
    }

    private void BuildCompact()
    {
        _compactLayer.VerticalAlignment = VerticalAlignment.Top;
        // colunas "Auto": cada espaço só ocupa o tamanho do ícone visível, então o notch fechado fica estreito
        _compactLayer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _compactLayer.ColumnDefinitions.Add(new ColumnDefinition());
        _compactLayer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _compactLayer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _compactLayer.Margin = new Thickness(10, 0, 5, 0);

        var left = new Grid { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(_compactBattery);
        left.Children.Add(_compactArt);
        left.Children.Add(_compactTimerIcon);
        _compactLayer.Children.Add(left);

        // microfone sempre visível na pílula: clique muta/libera sem abrir o painel
        _compactMic.Child = _compactMicIcon;
        _compactMic.HorizontalAlignment = HorizontalAlignment.Right;
        _compactMic.VerticalAlignment = VerticalAlignment.Center;
        _compactMic.MouseEnter += (_, _) => { _hoverTimer.Stop(); _compactMic.Background = NotchTheme.SurfaceHover; };
        _compactMic.MouseLeave += (_, _) =>
        {
            _compactMic.Background = Brushes.Transparent;
            if (OpenOnHover && _state == State.Compact && _shell.IsMouseOver) _hoverTimer.Start();
        };
        _compactMic.MouseLeftButtonDown += (_, e) => e.Handled = true;
        _compactMic.MouseLeftButtonUp += (_, e) => { ToggleMic(showAlert: false); e.Handled = true; };
        // webcam (só aparece quando está em uso) fica ao lado do microfone
        _compactCam.Margin = new Thickness(0, 0, 2, 0);
        _compactCam.Visibility = Visibility.Collapsed;
        var status = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        status.Children.Add(_compactCam);
        status.Children.Add(_compactMic);
        Grid.SetColumn(status, 3);
        _compactLayer.Children.Add(status);

        _compactTime.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetColumn(_compactTime, 1);
        _compactLayer.Children.Add(_compactTime);

        var right = new Grid { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(_compactBars);
        right.Children.Add(_compactMascot);
        _compactBars.Margin = new Thickness(0, 0, 3, 0);
        _compactMascot.Margin = new Thickness(0, 0, 3, 0);
        Grid.SetColumn(right, 2);
        _compactLayer.Children.Add(right);

        _compactLayer.Background = Brushes.Transparent;
        _compactLayer.Cursor = Cursors.Hand; // indica que o notch fechado abre com clique
        _compactLayer.MouseLeftButtonUp += (_, _) => { if (_state == State.Compact) Expand(); };
    }

    private void BuildExpanded()
    {
        _expandedLayer.Width = ExpandedW;
        _expandedLayer.Height = ExpandedH;
        _expandedLayer.VerticalAlignment = VerticalAlignment.Top;
        _expandedLayer.Margin = new Thickness(0);
        _expandedLayer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
        _expandedLayer.RowDefinitions.Add(new RowDefinition());
        _expandedLayer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });

        // cabeçalho
        var header = new Grid { Margin = new Thickness(20, 6, 20, 0) };
        header.Children.Add(_title);
        _headerTime.Margin = new Thickness(0);
        _micButton = Ui.IconButton(Glyphs.Microphone, () => ToggleMic(), 24, NotchTheme.Surface, NotchTheme.Text, 11);
        _headerCam.Visibility = Visibility.Collapsed;
        var right = Ui.Row(8, _headerCam, _micButton, _headerBattery, _headerDate, _headerTime, new Grid { Children = { _headerBars, _headerMascot }, Margin = new Thickness(12, 0, 0, 0) });
        right.HorizontalAlignment = HorizontalAlignment.Right;
        ((FrameworkElement)right.Children[5]).Margin = new Thickness(14, 0, 0, 0);
        header.Children.Add(right);
        _expandedLayer.Children.Add(header);

        // páginas com setas nas laterais
        var body = new Grid { Margin = new Thickness(6, 4, 6, 0) };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        body.ColumnDefinitions.Add(new ColumnDefinition());
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        var prev = Ui.IconButton(Glyphs.ChevronLeft, () => SetPage(_pageIndex - 1), 22, NotchTheme.Surface, NotchTheme.TextDim, 9);
        var next = Ui.IconButton(Glyphs.ChevronRight, () => SetPage(_pageIndex + 1), 22, NotchTheme.Surface, NotchTheme.TextDim, 9);
        body.Children.Add(prev);
        _pageHost.Margin = new Thickness(8, 0, 8, 0);
        _pageHost.RenderTransform = new TranslateTransform();
        Grid.SetColumn(_pageHost, 1);
        body.Children.Add(_pageHost);
        Grid.SetColumn(next, 2);
        body.Children.Add(next);
        Grid.SetRow(body, 1);
        _expandedLayer.Children.Add(body);

        body.MouseWheel += (_, e) =>
        {
            if (e.Handled || (DateTime.Now - _lastWheel).TotalMilliseconds < 300) return;
            _lastWheel = DateTime.Now;
            SetPage(_pageIndex + (e.Delta < 0 ? 1 : -1));
        };

        _dots.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetRow(_dots, 2);
        _expandedLayer.Children.Add(_dots);
    }

    private void LoadModules()
    {
        _modules.AddRange(new INotchModule[]
        {
            new MusicModule(_media),
            _lyricsModule,
            _pomodoro,
            new WaterModule(),
            new TodoModule(),
            _shelf,
            new SiteModule(),
        });
        _modules.AddRange(PluginLoader.Load(Paths.Plugins));

        foreach (var m in _modules.ToList())
        {
            try { m.Start(this); }
            catch (Exception ex)
            {
                Paths.WriteLog($"Módulo {m.Id} falhou ao iniciar: {ex}");
                _modules.Remove(m);
            }
        }
        RebuildPages();
        var saved = _settings.Get("page");
        var idx = _pages.FindIndex(p => p.Module.Id == saved);
        _pageIndex = idx >= 0 ? idx : 0;
    }

    private void RebuildPages()
    {
        var current = _pages.Count > 0 && _pageIndex < _pages.Count ? _pages[_pageIndex].Module.Id : null;
        var disabled = DisabledModules();
        var views = _pages.ToDictionary(p => p.Module.Id, p => p.View);
        _pages.Clear();
        foreach (var m in _modules.Where(m => !disabled.Contains(m.Id)).OrderBy(m => m.Order))
            _pages.Add(new Page(m) { View = views.GetValueOrDefault(m.Id) });
        var idx = current == null ? 0 : _pages.FindIndex(p => p.Module.Id == current);
        _pageIndex = Math.Max(0, idx);
        if (_state == State.Expanded) SetPage(_pageIndex, animate: false);
    }

    private HashSet<string> DisabledModules() =>
        _settings.Get("disabled").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

    private void WireEvents()
    {
        _shell.MouseEnter += (_, _) =>
        {
            _leaveTimer.Stop();
            if (_state == State.Alert) _alertTimer.Stop();       // não some enquanto o mouse está em cima
            // por padrão o notch só abre com clique; abrir ao passar o mouse é uma opção do menu
            else if (_state == State.Compact && OpenOnHover && !_compactMic.IsMouseOver) _hoverTimer.Start();
        };
        _shell.MouseLeave += (_, _) =>
        {
            _hoverTimer.Stop();
            if (_state == State.Alert)
            {
                _alertTimer.Interval = TimeSpan.FromSeconds(1.5);
                _alertTimer.Start();
            }
            else if (_state == State.Expanded) _leaveTimer.Start();
        };
        _shell.MouseRightButtonUp += (_, e) => { ShowTrayMenuAtCursor(); e.Handled = true; };

        _hoverTimer.Tick += (_, _) =>
        {
            _hoverTimer.Stop();
            if (OpenOnHover && _shell.IsMouseOver && !_compactMic.IsMouseOver && _state == State.Compact) Expand();
        };

        _leaveTimer.Tick += (_, _) =>
        {
            _leaveTimer.Stop();
            if (_state != State.Expanded || _shell.IsMouseOver || _dragging) return;
            if (IsActive && Keyboard.FocusedElement is TextBox) return; // digitando: só fecha ao clicar fora
            Collapse();
        };
        Deactivated += (_, _) =>
        {
            if (_state == State.Expanded && !_shell.IsMouseOver && !_dragging) Collapse();
        };
        _alertTimer.Tick += (_, _) =>
        {
            _alertTimer.Stop();
            _currentAlert = null;
            if (_alerts.Count > 0) ShowNextAlert();
            else SetState(State.Compact);
        };

        // arrastar arquivos para o notch abre a prateleira
        _shell.DragEnter += (_, e) =>
        {
            _dragging = true;
            _leaveTimer.Stop();
            var shelf = _pages.FindIndex(p => p.Module == _shelf);
            if (shelf >= 0)
            {
                if (_state != State.Expanded) Expand();
                if (_pageIndex != shelf) SetPage(shelf);
            }
            e.Effects = shelf >= 0 ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        _shell.DragOver += (_, e) =>
        {
            e.Effects = _pages.Any(p => p.Module == _shelf) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        _shell.DragLeave += (_, _) =>
        {
            _dragging = false;
            _leaveTimer.Start();
        };
        _shell.Drop += (_, e) =>
        {
            _dragging = false;
            if (_pages.Any(p => p.Module == _shelf)) _shelf.Accept(e.Data);
            e.Handled = true;
            _leaveTimer.Start();
        };

        _clockTimer.Tick += (_, _) => { UpdateClock(); UpdateCompact(); _audio.SyncMic(); UpdateCamera(); };
        _clockTimer.Start();
        _guardTimer.Tick += (_, _) => GuardTick();
        _guardTimer.Start();

        _media.Changed += UpdateCompact;
        _pomodoro.StateChanged += UpdateCompact;
        _status.BatteryChanged += () =>
        {
            _headerBattery.Update(_status.BatteryPercent, _status.Charging);
            _compactBattery.Update(_status.BatteryPercent, _status.Charging);
            _headerBattery.Visibility = _status.HasBattery ? Visibility.Visible : Visibility.Collapsed;
            UpdateCompact();
        };
        _audio.MicChanged += OnMicChanged;
        OnMicChanged(_audio.MicMuted);
        _audio.VolumeChanged += (volume, muted) =>
        {
            if (!Setting("alerts.volume")) return;
            ShowAlert(new NotchAlert("Volume", muted ? "Mudo" : $"{Math.Round(volume * 100)}%")
            {
                Glyph = muted || volume < 0.005 ? Glyphs.Mute : Glyphs.Volume,
                Accent = NotchTheme.Text,
                Progress = muted ? 0 : volume,
                Duration = TimeSpan.FromSeconds(1.6),
                Key = "volume",
            });
        };

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            var ex = Native.GetWindowLongPtr(_hwnd, Native.GWL_EXSTYLE);
            Native.SetWindowLongPtr(_hwnd, Native.GWL_EXSTYLE, ex | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);
        };
        Loaded += async (_, _) =>
        {
            _status.Start();
            await _media.StartAsync();
            if (Setting("winnotif", false)) await _winNotif.StartAsync();
            CreateTray();
            if (!_settings.GetBool("welcomed", false))
            {
                _settings.Set("welcomed", true);
                _settings.Save();
                Autostart.Set(true);
                ShowAlert(new NotchAlert("Olá! Eu sou o seu notch 👋", "Clique aqui em cima para abrir")
                {
                    Glyph = Glyphs.Info,
                    Accent = NotchTheme.Accent,
                    Duration = TimeSpan.FromSeconds(7),
                });
            }
        };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.BeginInvoke(Reposition);
        DpiChanged += (_, _) => Reposition();
    }

    // ------------------------------------------------------------------ microfone

    private bool _quietMicChange;

    /// <param name="showAlert">false quando o clique foi no ícone da pílula: o próprio ícone já mostra o estado.</param>
    private void ToggleMic(bool showAlert = true)
    {
        if (!_audio.HasMic)
        {
            ShowAlert(new NotchAlert("Microfone", "Nenhum microfone encontrado") { Glyph = Glyphs.MicOff, Accent = NotchTheme.TextDim, Key = "mic" });
            return;
        }
        _quietMicChange = !showAlert;
        _audio.ToggleMic();
        _quietMicChange = false;
    }

    private void OnMicChanged(bool muted)
    {
        // verde = ligado (estão te ouvindo) · vermelho cortado = mudo · cinza = sem microfone
        var glyph = !_audio.HasMic || muted ? Glyphs.MicOff : Glyphs.Microphone;
        var color = !_audio.HasMic ? NotchTheme.TextDim : muted ? NotchTheme.Red : NotchTheme.Green;
        var tip = !_audio.HasMic ? "Nenhum microfone conectado"
            : muted ? "Microfone MUDO · clique para ativar" : "Microfone LIGADO · clique para mutar";
        foreach (var icon in new[] { (TextBlock)_micButton.Child, _compactMicIcon })
        {
            icon.Text = glyph;
            icon.Foreground = color;
        }
        _micButton.ToolTip = tip;
        _compactMic.ToolTip = tip;

        // alerta só quando o estado muda de verdade (não ao trocar de fone, por exemplo)
        // (com o painel aberto, alertas com Key são ignorados: o próprio botão já mostra o estado)
        if (_lastMicMuted.HasValue && _lastMicMuted != muted && !_quietMicChange)
        {
            ShowAlert(new NotchAlert(muted ? "Microfone mudo" : "Microfone ativado", muted ? "Ninguém está te ouvindo" : "Você está sendo ouvido")
            {
                Glyph = muted ? Glyphs.MicOff : Glyphs.Microphone,
                Accent = muted ? NotchTheme.Red : NotchTheme.Green,
                Duration = TimeSpan.FromSeconds(1.8),
                Key = "mic",
            });
        }
        _lastMicMuted = muted;
        UpdateCompact();
    }

    // ------------------------------------------------------------------ estados

    // Notch fechado não mostra a hora; só alarga quando há contagem do Pomodoro no meio.
    // Inclui o espaço do ícone de microfone, que fica sempre visível.
    // A webcam, quando está em uso, soma mais um ícone ao lado do microfone.
    private (double w, double h) CompactSize
    {
        get
        {
            var (w, h) = (Setting("minimal", false), _pomodoro.IsRunning) switch
            {
                (true, true) => (96d, 26d),     // minimalista: contagem + microfone
                (true, false) => (56d, 26d),    // minimalista: só o microfone
                (false, true) => (146d, 30d),   // ícone · contagem · barrinhas · microfone
                (false, false) => (100d, 30d),  // ícone · barrinhas · microfone, sem vão no meio
            };
            return (_cameraOn ? w + 18 : w, h);
        }
    }

    private (double w, double h) _appliedCompactSize;

    // ------------------------------------------------------------------ webcam

    /// <summary>Mostra o ícone da webcam só enquanto algum app estiver usando a câmera.</summary>
    private void UpdateCamera()
    {
        var apps = _camera.AppsInUse();
        var on = apps.Count > 0;
        var tip = on ? "Webcam em uso: " + string.Join(", ", apps) : null;
        _compactCam.ToolTip = tip;
        _headerCam.ToolTip = tip;
        if (on == _cameraOn) return;
        _cameraOn = on;
        _compactCam.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        _headerCam.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        UpdateCompact(); // ajusta a largura do notch fechado
    }

    private void SetState(State state, bool animate = true)
    {
        _state = state;
        double w, h, radius;
        switch (state)
        {
            case State.Expanded:
                (w, h, radius) = (ExpandedW, ExpandedH, 30);
                break;
            case State.Alert:
                w = _currentAlert?.Actions.Count > 0 ? 430 : 370;
                (h, radius) = (AlertH, 24);
                _alertLayer.Width = w;
                break;
            default:
                (w, h) = CompactSize;
                _appliedCompactSize = (w, h);
                radius = h / 2.2;
                _compactLayer.Height = h;
                break;
        }
        _shell.CornerRadius = new CornerRadius(0, 0, radius, radius);
        Animate(w, h, animate, bouncy: state != State.Compact);
        ShowLayer(state switch { State.Expanded => _expandedLayer, State.Alert => _alertLayer, _ => _compactLayer }, animate);
        _compactMascot.Happy = false;
        UpdateCompact();
    }

    private void Animate(double w, double h, bool animate, bool bouncy)
    {
        if (!animate)
        {
            _shell.BeginAnimation(WidthProperty, null);
            _shell.BeginAnimation(HeightProperty, null);
            _shell.Width = w;
            _shell.Height = h;
            return;
        }
        var duration = TimeSpan.FromMilliseconds(bouncy ? 460 : 320);
        IEasingFunction ease = bouncy
            ? new BackEase { Amplitude = 0.28, EasingMode = EasingMode.EaseOut }
            : new CubicEase { EasingMode = EasingMode.EaseOut };
        _shell.BeginAnimation(WidthProperty, new DoubleAnimation(w, duration) { EasingFunction = ease });
        _shell.BeginAnimation(HeightProperty, new DoubleAnimation(h, duration) { EasingFunction = ease });
    }

    private void ShowLayer(Grid target, bool animate)
    {
        foreach (var layer in new[] { _compactLayer, _alertLayer, _expandedLayer })
        {
            var show = layer == target;
            layer.IsHitTestVisible = show;
            if (!animate)
            {
                layer.BeginAnimation(OpacityProperty, null);
                layer.Opacity = show ? 1 : 0;
                layer.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                continue;
            }
            if (show)
            {
                layer.Visibility = Visibility.Visible;
                layer.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { BeginTime = TimeSpan.FromMilliseconds(90) });
            }
            else if (layer.Visibility == Visibility.Visible)
            {
                var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(110));
                fade.Completed += (_, _) => { if (!layer.IsHitTestVisible) layer.Visibility = Visibility.Collapsed; };
                layer.BeginAnimation(OpacityProperty, fade);
            }
        }
    }

    private void Expand()
    {
        _alertTimer.Stop();
        _currentAlert = null;
        _hoverTimer.Stop();
        if (_pages.Count == 0 || IsHidden) return;
        SetPage(_pageIndex, animate: false);
        SetState(State.Expanded);
    }

    private void Collapse()
    {
        if (IsActive && Keyboard.FocusedElement is TextBox)
            Keyboard.ClearFocus();
        SetState(State.Compact);
        if (_alerts.Count > 0)
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(380) };
            t.Tick += (_, _) => { t.Stop(); if (_state == State.Compact && !IsHidden) ShowNextAlert(); };
            t.Start();
        }
    }

    // ------------------------------------------------------------------ páginas

    private void SetPage(int index, bool animate = true)
    {
        if (_pages.Count == 0) return;
        var direction = index > _pageIndex ? 1 : -1;
        index = ((index % _pages.Count) + _pages.Count) % _pages.Count;
        var page = _pages[index];
        if (page.View == null)
        {
            try { page.View = page.Module.CreateView(); }
            catch (Exception ex)
            {
                Paths.WriteLog($"Página {page.Module.Id} falhou: {ex}");
                page.View = Ui.Text("Esta ferramenta encontrou um erro. Veja o log.", 12, null, NotchTheme.Red);
            }
        }
        var changed = _pageIndex != index || _pageHost.Content != page.View;
        _pageIndex = index;
        _pageHost.Content = page.View;
        _title.Inlines.Clear();
        _title.Inlines.Add(new Run(page.Module.Title) { Foreground = NotchTheme.Text });
        if (page.Module.TitleAccent.Length > 0)
            _title.Inlines.Add(new Run(" " + page.Module.TitleAccent) { Foreground = NotchTheme.Accent });
        UpdateDots();
        if (_settings.Get("page") != page.Module.Id)
        {
            _settings.Set("page", page.Module.Id);
            _settings.Save();
        }
        if (animate && changed)
        {
            var tt = (TranslateTransform)_pageHost.RenderTransform;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(28 * direction, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
            _pageHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
        }
    }

    private void UpdateDots()
    {
        _dots.Children.Clear();
        for (var i = 0; i < _pages.Count; i++)
        {
            var active = i == _pageIndex;
            var index = i;
            var dot = new Border
            {
                Width = active ? 14 : 5,
                Height = 5,
                CornerRadius = new CornerRadius(2.5),
                Background = active ? NotchTheme.Text : NotchTheme.TextFaint,
                Margin = new Thickness(2.5, 0, 2.5, 0),
                Cursor = Cursors.Hand,
                ToolTip = _pages[i].Module.Title + " " + _pages[i].Module.TitleAccent,
            };
            // área de clique maior que o pontinho
            var hit = new Border { Background = Brushes.Transparent, Padding = new Thickness(0, 5, 0, 5), Child = dot };
            hit.MouseLeftButtonUp += (_, e) => { SetPage(index); e.Handled = true; };
            _dots.Children.Add(hit);
        }
    }

    // ------------------------------------------------------------------ alertas

    public void ShowAlert(NotchAlert alert)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => ShowAlert(alert));
            return;
        }
        // escondido (tela cheia ou modo gamer): avisos rápidos são descartados; os importantes esperam o notch voltar
        if (IsHidden && alert.Key != null) return;

        // mesmo tipo (volume, caps lock...): atualiza o alerta atual em vez de enfileirar
        if (alert.Key != null && _state == State.Alert && _currentAlert?.Key == alert.Key)
        {
            _currentAlert = alert;
            BuildAlert(alert);
            _alertTimer.Stop();
            _alertTimer.Interval = alert.Duration;
            if (!_shell.IsMouseOver) _alertTimer.Start();
            return;
        }
        if (alert.Key != null && _state == State.Expanded) return; // avisos rápidos não interrompem o painel aberto
        if (alert.Key != null)
        {
            // remove da fila versões antigas do mesmo aviso
            var rest = _alerts.Where(a => a.Key != alert.Key).ToList();
            _alerts.Clear();
            foreach (var a in rest) _alerts.Enqueue(a);
        }
        _alerts.Enqueue(alert);
        if (_state == State.Compact && !IsHidden) ShowNextAlert();
    }

    private void ShowNextAlert()
    {
        if (_alerts.Count == 0) return;
        var alert = _alerts.Dequeue();
        _currentAlert = alert;
        BuildAlert(alert);
        SetState(State.Alert);
        _alertTimer.Stop();
        _alertTimer.Interval = alert.Duration;
        if (!_shell.IsMouseOver) _alertTimer.Start();
    }

    private void DismissAlert()
    {
        _alertTimer.Stop();
        _currentAlert = null;
        if (_alerts.Count > 0) ShowNextAlert();
        else SetState(State.Compact);
    }

    private void BuildAlert(NotchAlert alert)
    {
        _alertLayer.Children.Clear();
        _alertLayer.ColumnDefinitions.Clear();
        _alertLayer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _alertLayer.ColumnDefinitions.Add(new ColumnDefinition());
        _alertLayer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _alertLayer.Background = Brushes.Transparent;
        _alertLayer.Cursor = Cursors.Hand;

        FrameworkElement icon;
        if (alert.Image != null)
            icon = new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(10), Background = new ImageBrush(alert.Image) { Stretch = Stretch.UniformToFill } };
        else
            icon = new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(19),
                Background = NotchTheme.Surface,
                Child = Ui.Icon(alert.Glyph, 16, alert.Accent ?? NotchTheme.Text),
            };
        icon.Margin = new Thickness(16, 0, 12, 0);
        icon.VerticalAlignment = VerticalAlignment.Center;
        _alertLayer.Children.Add(icon);

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        text.Children.Add(Ui.Text(alert.Title, 13.5, FontWeights.Bold));
        if (alert.Progress is { } p && alert.Actions.Count == 0)
        {
            var row = new Grid { Margin = new Thickness(0, 5, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var bar = Ui.ProgressBar(p, alert.Accent ?? NotchTheme.Text, 5);
            row.Children.Add(bar);
            var label = Ui.Text(alert.Message, 11, FontWeights.SemiBold, NotchTheme.TextDim);
            label.Margin = new Thickness(10, 0, 0, 0);
            Grid.SetColumn(label, 1);
            row.Children.Add(label);
            text.Children.Add(row);
        }
        else
        {
            text.Children.Add(Ui.Text(alert.Message, 11.5, null, NotchTheme.TextDim));
        }
        Grid.SetColumn(text, 1);
        _alertLayer.Children.Add(text);

        if (alert.Actions.Count > 0)
        {
            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            for (var i = 0; i < alert.Actions.Count; i++)
            {
                var action = alert.Actions[i];
                var b = Ui.Button(null, action.Label, () =>
                {
                    try { action.OnClick(); } catch (Exception ex) { Paths.WriteLog("Ação do alerta: " + ex); }
                    DismissAlert();
                }, i == 0 ? NotchTheme.AccentDeep : NotchTheme.Surface);
                if (i > 0) b.Margin = new Thickness(6, 0, 0, 0);
                actions.Children.Add(b);
            }
            Grid.SetColumn(actions, 2);
            _alertLayer.Children.Add(actions);
        }

        _alertLayer.MouseLeftButtonUp -= OnAlertClick;
        _alertLayer.MouseLeftButtonUp += OnAlertClick;
    }

    private void OnAlertClick(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled || _state != State.Alert) return;
        var alert = _currentAlert;
        if (alert?.OnClick != null)
        {
            try { alert.OnClick(); } catch (Exception ex) { Paths.WriteLog("Clique no alerta: " + ex); }
            DismissAlert();
        }
        else Expand();
    }

    // ------------------------------------------------------------------ atualização

    private void UpdateClock()
    {
        var now = DateTime.Now;
        _headerDate.Text = $"{Days[(int)now.DayOfWeek]} {now.Day} {Months[now.Month - 1]} {now.Year}";
        _headerTime.Text = now.ToString("HH:mm", Br);
    }

    private void UpdateCompact()
    {
        var minimal = Setting("minimal", false);
        var playing = _media.IsPlaying && _media.HasSession;
        var timer = _pomodoro.IsRunning;

        // a hora não aparece no notch fechado; o meio só mostra a contagem do Pomodoro
        _compactTime.FontSize = minimal ? 11.5 : 13;
        _compactTime.Visibility = timer ? Visibility.Visible : Visibility.Collapsed;
        if (timer)
        {
            var r = _pomodoro.Remaining;
            _compactTime.Text = $"{(int)r.TotalMinutes:00}:{r.Seconds:00}";
            _compactTime.Foreground = _pomodoro.ModeBrush;
        }

        // Pomodoro começou/parou com o notch fechado: ajusta a largura com animação
        if (IsLoaded && _state == State.Compact && CompactSize != _appliedCompactSize)
            SetState(State.Compact);

        _compactTimerIcon.Visibility = !minimal && timer ? Visibility.Visible : Visibility.Collapsed;
        _compactTimerIcon.Foreground = _pomodoro.ModeBrush;
        _compactTimerIcon.Text = _pomodoro.Mode == PomodoroMode.Foco ? Glyphs.Timer : Glyphs.Coffee;

        var showArt = !minimal && !timer && playing && _media.Art != null;
        _compactArt.Visibility = showArt ? Visibility.Visible : Visibility.Collapsed;
        if (showArt) _compactArt.Background = new ImageBrush(_media.Art) { Stretch = Stretch.UniformToFill };

        _compactBattery.Visibility = !minimal && !timer && !showArt && _status.HasBattery ? Visibility.Visible : Visibility.Collapsed;

        var mascot = Setting("mascot", true);
        _compactBars.IsPlaying = playing;
        _compactBars.Visibility = !minimal && (playing || !mascot) ? Visibility.Visible : Visibility.Collapsed;
        _compactMascot.Visibility = !minimal && !playing && mascot ? Visibility.Visible : Visibility.Collapsed;
        _headerBars.IsPlaying = playing;
        _headerBars.Visibility = playing || !mascot ? Visibility.Visible : Visibility.Collapsed;
        _headerMascot.Visibility = !playing && mascot ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Esconde em tela cheia (filmes, jogos, apresentações) e mantém a janela por cima.</summary>
    private void GuardTick()
    {
        if (_hwnd == 0) return;
        var fg = Native.GetForegroundWindow();
        var fullscreen = false;
        if (fg != 0 && fg != _hwnd && fg != Native.GetShellWindow() && fg != Native.GetDesktopWindow())
        {
            var cls = Native.ClassName(fg);
            if (cls is not ("Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") && Native.GetWindowRect(fg, out var r))
            {
                var screen = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
                fullscreen = r.Left <= screen.Left && r.Top <= screen.Top && r.Right >= screen.Right && r.Bottom >= screen.Bottom;
            }
        }
        if (fullscreen != _hiddenForFullscreen)
        {
            _hiddenForFullscreen = fullscreen;
            ApplyVisibility();
        }

        // modo gamer com timer: volta sozinho na hora marcada
        if (_gamer && _gamerUntil is { } until && DateTime.Now >= until) StopGamer();

        if (!IsHidden)
            Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    // ------------------------------------------------------------------ modo gamer

    // Não é salvo em arquivo de propósito: reiniciar ou desligar o PC sempre volta ao modo normal.
    private bool _gamer;
    private DateTime? _gamerUntil;

    /// <summary>Escondido por tela cheia ou pelo modo gamer.</summary>
    private bool IsHidden => _hiddenForFullscreen || _gamer;

    private void ApplyVisibility()
    {
        var hidden = IsHidden;
        _root.BeginAnimation(OpacityProperty, new DoubleAnimation(hidden ? 0 : 1, TimeSpan.FromMilliseconds(200)));
        _root.IsHitTestVisible = !hidden;
        if (hidden)
        {
            if (_state == State.Expanded) Collapse();
            else if (_state == State.Alert) { _alertTimer.Stop(); _currentAlert = null; SetState(State.Compact, animate: false); }
        }
        else if (_state == State.Compact && _alerts.Count > 0)
        {
            // mostra o que ficou guardado enquanto estava escondido
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            t.Tick += (_, _) => { t.Stop(); if (_state == State.Compact && !IsHidden) ShowNextAlert(); };
            t.Start();
        }
    }

    /// <param name="duration">null = até desativar pela bandeja.</param>
    private void StartGamer(TimeSpan? duration)
    {
        _gamer = true;
        _gamerUntil = duration is { } d ? DateTime.Now + d : null;
        ApplyVisibility();
        UpdateTrayText();
    }

    private void StopGamer()
    {
        if (!_gamer) return;
        _gamer = false;
        _gamerUntil = null;
        UpdateTrayText();
        ShowAlert(new NotchAlert("Modo gamer desativado", "O notch voltou") { Glyph = Glyphs.Gamepad, Accent = NotchTheme.Green, Duration = TimeSpan.FromSeconds(2.5) });
        ApplyVisibility();
    }

    private void Reposition()
    {
        Left = SystemParameters.PrimaryScreenWidth / 2 - Width / 2;
        Top = 0;
    }

    // ------------------------------------------------------------------ INotchHost

    public KeyValueFile OpenStore(string name) => new(System.IO.Path.Combine(Paths.Data, name + ".txt"));

    public string DataDirectory => Paths.Data;

    public HttpClient Http => _http;

    public void ShowPage(string moduleId)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var i = _pages.FindIndex(p => p.Module.Id == moduleId);
            if (i < 0) return;
            _pageIndex = i;
            Expand();
            _leaveTimer.Start();
        });
    }

    public void RequestKeyboardFocus()
    {
        if (_hwnd == 0) return;
        Native.SetForegroundWindow(_hwnd);
        Activate();
    }

    private bool Setting(string key) => Setting(key, true);

    /// <summary>Opção do menu "Abrir ao passar o mouse" (desligada: abre só com clique).</summary>
    private bool OpenOnHover => Setting("hover", false);

    private bool Setting(string key, bool fallback) => _settings.GetBool(key, fallback);

    protected override void OnClosed(EventArgs e)
    {
        foreach (var m in _modules)
        {
            try { m.Stop(); } catch { }
        }
        _status.Dispose();
        _camera.Dispose();
        _winNotif.Stop();
        DisposeTray();
        base.OnClosed(e);
    }
}
