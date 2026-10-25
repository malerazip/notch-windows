using System.Globalization;
using System.Management;
using System.Net.NetworkInformation;
using System.Windows.Threading;
using Notch.App.Core;
using Notch.Sdk;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Networking.Connectivity;
using Forms = System.Windows.Forms;

namespace Notch.App.Services;

/// <summary>Bateria, carregador, rede, Bluetooth, Caps Lock, idioma do teclado e brilho.</summary>
internal sealed class SystemStatusService : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Action<NotchAlert> _alert;
    private readonly Func<string, bool> _enabled;
    private readonly DispatcherTimer _fast = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly DispatcherTimer _slow = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly Dictionary<string, string> _btNames = new();
    private readonly List<DeviceWatcher> _watchers = new();
    private ManagementEventWatcher? _brightness;

    private bool? _caps;
    private nint _lastFg;
    private nint _lastLayout;
    private bool? _plugged;
    private int _lastBatteryLevel = -1;
    private string _netState = "";
    private DispatcherTimer? _netDebounce;

    public bool HasBattery { get; private set; }
    public double BatteryPercent { get; private set; } = 1;
    public bool Charging { get; private set; }

    /// <summary>Bateria mudou (para o ícone do cabeçalho).</summary>
    public event Action? BatteryChanged;

    public SystemStatusService(Dispatcher dispatcher, Action<NotchAlert> alert, Func<string, bool> enabled)
    {
        _dispatcher = dispatcher;
        _alert = alert;
        _enabled = enabled;
    }

    public void Start()
    {
        _caps = (Native.GetKeyState(Native.VK_CAPITAL) & 1) != 0;
        _fast.Tick += (_, _) => FastTick();
        _slow.Tick += (_, _) => PollBattery(false);
        PollBattery(true);
        _fast.Start();
        _slow.Start();

        _netState = NetworkState().state;
        NetworkChange.NetworkAddressChanged += (_, _) => _dispatcher.BeginInvoke(ScheduleNetworkCheck);
        NetworkChange.NetworkAvailabilityChanged += (_, _) => _dispatcher.BeginInvoke(ScheduleNetworkCheck);

        StartBluetooth();
        StartBrightness();
    }

    private void FastTick()
    {
        // Caps Lock (consulta de estado, sem gancho de teclado)
        var caps = (Native.GetKeyState(Native.VK_CAPITAL) & 1) != 0;
        if (caps != _caps)
        {
            _caps = caps;
            if (_enabled("alerts.caps"))
                _alert(new NotchAlert("Caps Lock", caps ? "Ativado" : "Desativado")
                {
                    Glyph = Glyphs.CapsLock,
                    Accent = caps ? NotchTheme.Green : NotchTheme.TextDim,
                    Duration = TimeSpan.FromSeconds(1.6),
                    Key = "caps",
                });
        }

        // Idioma do teclado: só avisa quando muda na mesma janela (trocar de janela não conta)
        var fg = Native.GetForegroundWindow();
        var layout = Native.GetKeyboardLayout(Native.GetWindowThreadProcessId(fg, out _));
        if (fg == _lastFg && layout != _lastLayout && _lastLayout != 0 && layout != 0 && _enabled("alerts.idioma"))
        {
            _alert(new NotchAlert("Idioma do teclado", LayoutName(layout))
            {
                Glyph = Glyphs.Keyboard,
                Accent = NotchTheme.Accent,
                Duration = TimeSpan.FromSeconds(1.8),
                Key = "layout",
            });
        }
        _lastFg = fg;
        _lastLayout = layout;
    }

    private static string LayoutName(nint hkl)
    {
        try
        {
            var lang = (int)((long)hkl & 0xFFFF);
            var name = new CultureInfo(lang).DisplayName;
            return char.ToUpper(name[0], CultureInfo.CurrentCulture) + name[1..];
        }
        catch (CultureNotFoundException) { return "Alterado"; }
    }

    private void PollBattery(bool initial)
    {
        var ps = Forms.SystemInformation.PowerStatus;
        HasBattery = ps.BatteryChargeStatus != Forms.BatteryChargeStatus.NoSystemBattery && ps.BatteryChargeStatus != Forms.BatteryChargeStatus.Unknown;
        var plugged = ps.PowerLineStatus == Forms.PowerLineStatus.Online;
        var level = (int)Math.Round(ps.BatteryLifePercent * 100);
        BatteryPercent = Math.Clamp(ps.BatteryLifePercent, 0, 1);
        Charging = plugged && HasBattery;
        BatteryChanged?.Invoke();
        if (!HasBattery) return;

        if (initial)
        {
            _plugged = plugged;
            _lastBatteryLevel = level;
            return;
        }

        var on = _enabled("alerts.bateria");
        if (_plugged != plugged && on)
        {
            _alert(plugged
                ? new NotchAlert("Carregador conectado", $"Carregando · {level}%") { Glyph = Glyphs.Charging, Accent = NotchTheme.Green, Progress = BatteryPercent, Key = "power" }
                : new NotchAlert("Carregador desconectado", $"{level}% restantes") { Glyph = Glyphs.Battery, Accent = level <= 20 ? NotchTheme.Orange : NotchTheme.Text, Progress = BatteryPercent, Key = "power" });
        }
        _plugged = plugged;

        if (on && _lastBatteryLevel >= 0)
        {
            if (!plugged && level <= 10 && _lastBatteryLevel > 10)
                _alert(new NotchAlert("Bateria crítica", $"{level}% · conecte o carregador agora") { Glyph = Glyphs.Battery, Accent = NotchTheme.Red, Progress = BatteryPercent, Duration = TimeSpan.FromSeconds(8) });
            else if (!plugged && level <= 20 && _lastBatteryLevel > 20)
                _alert(new NotchAlert("Bateria fraca", $"{level}% · conecte o carregador em breve") { Glyph = Glyphs.Battery, Accent = NotchTheme.Orange, Progress = BatteryPercent, Duration = TimeSpan.FromSeconds(6) });
            else if (plugged && level >= 100 && _lastBatteryLevel < 100)
                _alert(new NotchAlert("Totalmente carregada", "Você já pode desconectar") { Glyph = Glyphs.Charging, Accent = NotchTheme.Green, Progress = 1 });
        }
        _lastBatteryLevel = level;
    }

    private void ScheduleNetworkCheck()
    {
        // a rede costuma oscilar alguns segundos ao trocar; espera estabilizar
        _netDebounce?.Stop();
        _netDebounce = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _netDebounce.Tick += (_, _) =>
        {
            _netDebounce.Stop();
            var (state, name) = NetworkState();
            if (state == _netState) return;
            _netState = state;
            if (!_enabled("alerts.rede")) return;
            _alert(state switch
            {
                "internet" => new NotchAlert("Conectado", name) { Glyph = Glyphs.Wifi, Accent = NotchTheme.Green, Key = "net" },
                "local" => new NotchAlert("Sem acesso à internet", name) { Glyph = Glyphs.WifiOff, Accent = NotchTheme.Orange, Key = "net" },
                _ => new NotchAlert("Desconectado", "Nenhuma rede") { Glyph = Glyphs.WifiOff, Accent = NotchTheme.Red, Key = "net" },
            });
        };
        _netDebounce.Start();
    }

    private static (string state, string name) NetworkState()
    {
        try
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            if (profile == null) return ("off", "");
            var level = profile.GetNetworkConnectivityLevel();
            var name = profile.ProfileName ?? "";
            return level switch
            {
                NetworkConnectivityLevel.InternetAccess => ("internet", name),
                NetworkConnectivityLevel.None => ("off", name),
                _ => ("local", name),
            };
        }
        catch { return ("off", ""); }
    }

    private void StartBluetooth()
    {
        try
        {
            foreach (var selector in new[]
            {
                BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected),
                Windows.Devices.Bluetooth.BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected),
            })
            {
                var watcher = DeviceInformation.CreateWatcher(selector);
                var ready = false;
                watcher.Added += (_, info) => _dispatcher.BeginInvoke(() =>
                {
                    var name = string.IsNullOrWhiteSpace(info.Name) ? "Dispositivo Bluetooth" : info.Name;
                    var isNew = _btNames.TryAdd(info.Id, name);
                    if (ready && isNew && _enabled("alerts.bluetooth"))
                        _alert(new NotchAlert("Conectado", name) { Glyph = Glyphs.Bluetooth, Accent = NotchTheme.Accent, Key = "bt" });
                });
                watcher.Removed += (_, upd) => _dispatcher.BeginInvoke(() =>
                {
                    if (_btNames.Remove(upd.Id, out var name) && _enabled("alerts.bluetooth"))
                        _alert(new NotchAlert("Desconectado", name) { Glyph = Glyphs.Bluetooth, Accent = NotchTheme.TextDim, Key = "bt" });
                });
                watcher.Updated += (_, _) => { };
                watcher.EnumerationCompleted += (_, _) => _dispatcher.BeginInvoke(() => ready = true);
                watcher.Start();
                _watchers.Add(watcher);
            }
        }
        catch (Exception ex)
        {
            Paths.WriteLog("Bluetooth indisponível: " + ex.Message);
        }
    }

    private void StartBrightness()
    {
        // Só existe em notebooks/monitores com controle de brilho via WMI
        try
        {
            _brightness = new ManagementEventWatcher(new ManagementScope(@"root\wmi"), new EventQuery("SELECT * FROM WmiMonitorBrightnessEvent"));
            _brightness.EventArrived += (_, e) =>
            {
                var value = Convert.ToDouble(e.NewEvent.Properties["Brightness"].Value, CultureInfo.InvariantCulture) / 100.0;
                _dispatcher.BeginInvoke(() =>
                {
                    if (!_enabled("alerts.brilho")) return;
                    _alert(new NotchAlert("Brilho", $"{Math.Round(value * 100)}%")
                    {
                        Glyph = Glyphs.Brightness,
                        Accent = NotchTheme.Yellow,
                        Progress = value,
                        Duration = TimeSpan.FromSeconds(1.6),
                        Key = "brightness",
                    });
                });
            };
            _brightness.Start();
        }
        catch (Exception)
        {
            _brightness = null; // PC de mesa sem suporte
        }
    }

    public void Dispose()
    {
        _fast.Stop();
        _slow.Stop();
        foreach (var w in _watchers)
        {
            try { w.Stop(); } catch { }
        }
        try { _brightness?.Stop(); _brightness?.Dispose(); } catch { }
    }
}
