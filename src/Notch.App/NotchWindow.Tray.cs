using System.Diagnostics;
using System.Windows;
using Notch.App.Core;
using Notch.Sdk;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace Notch.App;

/// <summary>Ícone na bandeja do Windows e menu de configurações (também abre com clique direito no notch).</summary>
internal sealed partial class NotchWindow
{
    private Forms.NotifyIcon? _tray;
    private Forms.ContextMenuStrip? _menu;

    private static readonly (string Key, string Label)[] AlertOptions =
    [
        ("alerts.bateria", "Bateria e carregador"),
        ("alerts.volume", "Volume"),
        ("alerts.brilho", "Brilho"),
        ("alerts.caps", "Caps Lock"),
        ("alerts.idioma", "Idioma do teclado"),
        ("alerts.rede", "Internet / Wi-Fi"),
        ("alerts.bluetooth", "Dispositivos Bluetooth"),
    ];

    private void CreateTray()
    {
        _menu = new Forms.ContextMenuStrip
        {
            Renderer = new Forms.ToolStripProfessionalRenderer(new DarkColors()) { RoundedEdges = false },
            ShowImageMargin = false,
            ShowCheckMargin = true,
            Font = new Drawing.Font("Segoe UI", 9.5f),
        };
        _menu.Opening += (_, _) => BuildMenu();
        BuildMenu();

        Drawing.Icon icon;
        try
        {
            var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/notch.ico"))!.Stream;
            icon = new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
        }
        catch { icon = Drawing.SystemIcons.Application; }

        _tray = new Forms.NotifyIcon
        {
            Icon = icon,
            Text = Paths.AppName,
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) Expand();
        };
    }

    private void ShowTrayMenuAtCursor()
    {
        if (_menu == null) return;
        BuildMenu();
        _menu.Show(Forms.Cursor.Position);
    }

    private void BuildMenu()
    {
        if (_menu == null) return;
        _menu.Items.Clear();

        var header = new Forms.ToolStripMenuItem($"{Paths.AppName} {Paths.AppVersion}") { Enabled = false };
        _menu.Items.Add(header);
        _menu.Items.Add(new Forms.ToolStripSeparator());

        // Ferramentas (páginas) ligadas/desligadas
        var tools = new Forms.ToolStripMenuItem("Ferramentas") { ForeColor = Drawing.Color.White };
        var disabled = DisabledModules();
        foreach (var m in _modules.OrderBy(m => m.Order))
        {
            var id = m.Id;
            tools.DropDownItems.Add(Check($"{m.Title} {m.TitleAccent}".Trim(), !disabled.Contains(id), on =>
            {
                var set = DisabledModules();
                if (on) set.Remove(id); else set.Add(id);
                _settings.Set("disabled", string.Join(",", set));
                _settings.Save();
                RebuildPages();
            }));
        }
        _menu.Items.Add(tools);

        if (_audio.HasMic)
            _menu.Items.Add(Check("Microfone mudo", _audio.MicMuted, on => { if (on != _audio.MicMuted) ToggleMic(); }));

        var alerts = new Forms.ToolStripMenuItem("Alertas do sistema") { ForeColor = Drawing.Color.White };
        foreach (var (key, label) in AlertOptions)
            alerts.DropDownItems.Add(Check(label, Setting(key), on => SaveSetting(key, on)));
        _menu.Items.Add(alerts);

        _menu.Items.Add(Check("Notificações do Windows no notch", Setting("winnotif", false), async on =>
        {
            SaveSetting("winnotif", on);
            if (on)
            {
                var error = await _winNotif.StartAsync();
                if (error != null)
                {
                    SaveSetting("winnotif", false);
                    ShowAlert(new NotchAlert("Notificações do Windows", error) { Glyph = Glyphs.Message, Accent = NotchTheme.Orange, Duration = TimeSpan.FromSeconds(8) });
                }
                else ShowAlert(new NotchAlert("Notificações do Windows", "Elas vão aparecer aqui no notch") { Glyph = Glyphs.Message, Accent = NotchTheme.Green });
            }
            else _winNotif.Stop();
        }));

        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(Check("Modo minimalista", Setting("minimal", false), on =>
        {
            SaveSetting("minimal", on);
            if (_state == State.Compact) SetState(State.Compact);
        }));
        _menu.Items.Add(Check("Mascote", Setting("mascot", true), on => { SaveSetting("mascot", on); UpdateCompact(); }));
        _menu.Items.Add(Check("Iniciar com o Windows", Autostart.IsEnabled, Autostart.Set));

        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(Item("Abrir pasta de plugins", () => Open(Paths.Plugins)));
        _menu.Items.Add(Item("Abrir pasta de dados", () => Open(Paths.Data)));
        _menu.Items.Add(Item("Sobre", () => ShowAlert(new NotchAlert($"{Paths.AppName} {Paths.AppVersion}", "Música, letras, foco, água, tarefas e prateleira · Letras: LRCLIB")
        {
            Glyph = Glyphs.Info,
            Accent = NotchTheme.Accent,
            Duration = TimeSpan.FromSeconds(6),
        })));
        _menu.Items.Add(Item("Sair", () =>
        {
            Close();
            Application.Current.Shutdown();
        }));
    }

    private void SaveSetting(string key, bool value)
    {
        _settings.Set(key, value);
        _settings.Save();
    }

    private static void Open(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
    }

    private Forms.ToolStripMenuItem Item(string text, Action onClick)
    {
        var item = new Forms.ToolStripMenuItem(text) { ForeColor = Drawing.Color.White };
        item.Click += (_, _) => Dispatcher.BeginInvoke(onClick);
        return item;
    }

    private Forms.ToolStripMenuItem Check(string text, bool on, Action<bool> changed)
    {
        var item = new Forms.ToolStripMenuItem(text) { Checked = on, CheckOnClick = true, ForeColor = Drawing.Color.White };
        item.CheckedChanged += (_, _) => Dispatcher.BeginInvoke(() => changed(item.Checked));
        return item;
    }

    private void DisposeTray()
    {
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        _menu?.Dispose();
    }

    private sealed class DarkColors : Forms.ProfessionalColorTable
    {
        private static readonly Drawing.Color Back = Drawing.Color.FromArgb(28, 28, 30);
        private static readonly Drawing.Color Hover = Drawing.Color.FromArgb(44, 44, 46);
        private static readonly Drawing.Color Line = Drawing.Color.FromArgb(58, 58, 60);
        public override Drawing.Color ToolStripDropDownBackground => Back;
        public override Drawing.Color MenuBorder => Line;
        public override Drawing.Color MenuItemBorder => Hover;
        public override Drawing.Color MenuItemSelected => Hover;
        public override Drawing.Color MenuItemSelectedGradientBegin => Hover;
        public override Drawing.Color MenuItemSelectedGradientEnd => Hover;
        public override Drawing.Color MenuItemPressedGradientBegin => Hover;
        public override Drawing.Color MenuItemPressedGradientMiddle => Hover;
        public override Drawing.Color MenuItemPressedGradientEnd => Hover;
        public override Drawing.Color ImageMarginGradientBegin => Back;
        public override Drawing.Color ImageMarginGradientMiddle => Back;
        public override Drawing.Color ImageMarginGradientEnd => Back;
        public override Drawing.Color SeparatorDark => Line;
        public override Drawing.Color SeparatorLight => Back;
        public override Drawing.Color CheckBackground => Drawing.Color.FromArgb(11, 79, 140);
        public override Drawing.Color CheckSelectedBackground => Drawing.Color.FromArgb(11, 79, 140);
        public override Drawing.Color CheckPressedBackground => Drawing.Color.FromArgb(11, 79, 140);
    }
}
