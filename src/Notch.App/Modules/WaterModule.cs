using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Notch.App.Controls;
using Notch.Sdk;

namespace Notch.App.Modules;

/// <summary>
/// Lembrete de água: distribui a meta do dia entre o horário de início e fim,
/// avisa quando é hora de beber e quando a garrafa deve estar vazia.
/// </summary>
internal sealed class WaterModule : ModuleBase
{
    private static readonly CultureInfo Br = new("pt-BR");
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(20) };
    private KeyValueFile _s = null!;
    private int[] _hours = new int[24];
    private DateTime _snoozeUntil;
    private string _panel = "main"; // main | settings | history | confirm

    public override string Id => "agua";
    public override string Title => "Lembrete";
    public override string TitleAccent => "de Água";
    public override int Order => 40;

    private int Bottle => _s.GetInt("bottle", 500);
    private int Goal => _s.GetInt("goal", 2000);
    private int Dose => _s.GetInt("dose", 250);
    private int StartMin => _s.GetInt("start", 8 * 60);
    private int EndMin => _s.GetInt("end", 18 * 60);
    private int Taken => _s.GetInt("taken", 0);
    private int InBottle => _s.GetInt("inb", Bottle);
    private bool RemindersOn => _s.GetBool("on", true);

    public event Action? Changed;

    public override void Start(INotchHost host)
    {
        base.Start(host);
        _s = host.OpenStore("water");
        _hours = _s.Get("hours").Split(',').Select(v => int.TryParse(v, out var n) ? n : 0).Concat(Enumerable.Repeat(0, 24)).Take(24).ToArray();
        RollDay();
        _timer.Tick += (_, _) => Check();
        _timer.Start();
    }

    private void RollDay()
    {
        var today = DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        if (_s.Get("day") == today) return;
        _s.Set("day", today);
        _s.Set("taken", 0);
        _s.Set("fired", 0);
        // primeiro lembrete do dia vem um intervalo depois de ligar o PC, não na hora
        _s.Set("last", DateTime.Now.Hour * 60 + DateTime.Now.Minute);
        _hours = new int[24];
        Save();
    }

    private void Save()
    {
        _s.Set("hours", string.Join(",", _hours));
        _s.Save();
        Changed?.Invoke();
    }

    /// <summary>Intervalo escolhido em "A cada" (0 = automático).</summary>
    private int EveryMin => _s.GetInt("every", 0);

    /// <summary>Automático: minutos entre lembretes para cumprir a meta dentro do horário.</summary>
    private int AutoIntervalMin => Math.Max(15, (EndMin - StartMin) / Math.Max(1, (int)Math.Ceiling(Goal / (double)Dose)));

    private int IntervalMin => EveryMin > 0 ? EveryMin : AutoIntervalMin;

    private static string Duration(int minutes) =>
        minutes < 60 ? $"{minutes} min" : minutes % 60 == 0 ? $"{minutes / 60} h" : $"{minutes / 60}h{minutes % 60:00}";

    /// <summary>Texto curto do estado dos lembretes (para a página e a tela de lembretes).</summary>
    private string ReminderStatus()
    {
        if (!RemindersOn) return "lembretes desligados";
        if (Taken >= Goal) return "meta batida";
        var now = DateTime.Now.Hour * 60 + DateTime.Now.Minute;
        if (NextReminder() is { } n) return $"a cada {Duration(IntervalMin)} · próximo {Hm(n)}";
        return now >= EndMin || now < StartMin
            ? $"fora do horário ({Hm(StartMin)}–{Hm(EndMin)})"
            : "sem mais lembretes hoje";
    }

    /// <summary>Horário (minutos do dia) do próximo lembrete, ou null se não há mais hoje.</summary>
    private int? NextReminder()
    {
        if (!RemindersOn || Taken >= Goal) return null;
        var now = DateTime.Now.Hour * 60 + DateTime.Now.Minute;
        var last = Math.Max(_s.GetInt("last", 0), StartMin);
        var next = Math.Max(last + IntervalMin, StartMin);
        if (now < StartMin) next = StartMin + IntervalMin;
        if (next > EndMin) return null;
        if (_snoozeUntil > DateTime.Now) next = Math.Max(next, _snoozeUntil.Hour * 60 + _snoozeUntil.Minute);
        return next;
    }

    private void Check()
    {
        RollDay();
        var next = NextReminder();
        var now = DateTime.Now.Hour * 60 + DateTime.Now.Minute;
        if (next is not { } n || now < n || _snoozeUntil > DateTime.Now) return;
        _s.Set("last", now);
        _s.Set("fired", _s.GetInt("fired", 0) + 1);
        Save();
        ShowReminder();
    }

    private void ShowReminder()
    {
        if (InBottle < Dose / 2)
        {
            Host.ShowAlert(new NotchAlert("Hora de reabastecer", "Sua garrafa provavelmente está vazia")
            {
                Glyph = Glyphs.Drop,
                Accent = NotchTheme.Accent,
                Duration = TimeSpan.FromSeconds(12),
                Actions = [new NotchAlertAction("Reabasteci", Refill), new NotchAlertAction("Adiar", Snooze)],
            });
        }
        else
        {
            Host.ShowAlert(new NotchAlert("Hora de beber água", $"Beba {Dose} ml · {Liters(Taken)} de {Liters(Goal)} L hoje")
            {
                Glyph = Glyphs.Drop,
                Accent = NotchTheme.Accent,
                Duration = TimeSpan.FromSeconds(12),
                Progress = Math.Min(1, Taken / (double)Goal),
                Actions = [new NotchAlertAction("Bebi", Drink), new NotchAlertAction("Adiar", Snooze)],
            });
        }
    }

    public void Drink()
    {
        RollDay();
        _s.Set("taken", Taken + Dose);
        _s.Set("inb", Math.Max(0, InBottle - Dose));
        _s.Set("last", DateTime.Now.Hour * 60 + DateTime.Now.Minute);
        _hours[DateTime.Now.Hour] += Dose;
        Save();
        if (Taken >= Goal && Taken - Dose < Goal)
            Host.ShowAlert(new NotchAlert("Meta batida! 🎉", $"Você bebeu {Liters(Taken)} L hoje") { Glyph = Glyphs.Drop, Accent = NotchTheme.Green, Progress = 1 });
    }

    public void Refill()
    {
        _s.Set("inb", Bottle);
        Save();
    }

    private void Snooze()
    {
        _snoozeUntil = DateTime.Now.AddMinutes(10);
        Changed?.Invoke();
    }

    private static string Liters(int ml) => (ml / 1000.0).ToString("0.0", Br);

    private static string Hm(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";

    public override FrameworkElement CreateView()
    {
        var root = new ContentControl();
        void Render() => root.Content = _panel switch
        {
            "settings" => SettingsView(Render),
            "lembretes" => RemindersView(Render),
            "confirm" => ConfirmView(Render),
            "history" => MainView(Render, true),
            _ => MainView(Render, false),
        };
        Changed += Render;
        Render();
        WhileVisible(root, TimeSpan.FromSeconds(30), () => { RollDay(); });
        return root;
    }

    private FrameworkElement MainView(Action render, bool history)
    {
        var pct = Math.Min(1, Taken / (double)Goal);
        var big = Ui.Text(Liters(Taken), 28, FontWeights.Bold);
        var of = Ui.Text($" / {Liters(Goal)} L", 12, FontWeights.SemiBold, NotchTheme.TextDim);
        of.VerticalAlignment = VerticalAlignment.Bottom;
        of.Margin = new Thickness(2, 0, 0, 6);
        var amount = Ui.Row(0, big, of);

        var toggle = Ui.Toggle(RemindersOn, on => { _s.Set("on", on); Save(); }, Glyphs.Bell);
        toggle.ToolTip = "Lembretes";
        var top = new Grid();
        top.Children.Add(amount);
        toggle.HorizontalAlignment = HorizontalAlignment.Right;
        top.Children.Add(toggle);

        var status = new TextBlock { FontFamily = NotchTheme.Font, FontSize = 11, Foreground = NotchTheme.TextDim, TextTrimming = TextTrimming.CharacterEllipsis };
        status.Inlines.Add(new System.Windows.Documents.Run($"{Math.Round(pct * 100)}%") { Foreground = NotchTheme.Accent, FontWeight = FontWeights.SemiBold });
        status.Inlines.Add(" · " + ReminderStatus());
        status.ToolTip = $"Na garrafa: {InBottle} de {Bottle} ml";

        var bar = Ui.ProgressBar(pct, NotchTheme.Accent, 5);
        bar.Margin = new Thickness(0, 6, 0, 8);

        var buttons = Ui.Row(6,
            Ui.Button(Glyphs.Drop, $"Beber {Dose} ml", Drink, NotchTheme.AccentDeep),
            Ui.Button(Glyphs.Clock, "Lembretes", () => { _panel = "lembretes"; render(); }),
            Ui.Button(Glyphs.Settings, null, () => { _panel = "settings"; render(); }));
        ((Border)buttons.Children[1]).ToolTip = "Quando e de quanto em quanto tempo lembrar";
        ((Border)buttons.Children[2]).ToolTip = "Garrafa, meta e dose";

        var left = Ui.Column(0, top, status, bar, buttons);
        left.VerticalAlignment = VerticalAlignment.Center;

        FrameworkElement right;
        if (history)
        {
            right = HistoryChart();
        }
        else
        {
            var ring = new RingProgress { Width = 80, Height = 80, Value = pct, Fill = NotchTheme.Accent, Track = NotchTheme.Solid(0x10, 0x2A, 0x45) };
            var center = Ui.Column(-2, Ui.Text($"{Math.Round(pct * 100)}", 22, FontWeights.Bold), Ui.Row(2, Ui.Icon(Glyphs.Drop, 9, NotchTheme.Accent), Ui.Text("%", 10, FontWeights.SemiBold)));
            center.HorizontalAlignment = HorizontalAlignment.Center;
            center.VerticalAlignment = VerticalAlignment.Center;
            ((FrameworkElement)center.Children[0]).HorizontalAlignment = HorizontalAlignment.Center;
            ((FrameworkElement)center.Children[1]).HorizontalAlignment = HorizontalAlignment.Center;
            var g = new Grid { Width = 80, Height = 80 };
            g.Children.Add(ring);
            g.Children.Add(center);
            right = g;
        }
        var rightBox = new Border { Child = right, Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand, ToolTip = history ? "Ver progresso" : "Ver histórico por hora", VerticalAlignment = VerticalAlignment.Center };
        rightBox.MouseLeftButtonUp += (_, _) => { _panel = history ? "main" : "history"; render(); };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(history ? 140 : 104) });
        grid.Children.Add(left);
        var divider = new Border { Background = NotchTheme.Surface, Margin = new Thickness(0, 6, 0, 6) };
        Grid.SetColumn(divider, 1);
        grid.Children.Add(divider);
        Grid.SetColumn(rightBox, 2);
        rightBox.HorizontalAlignment = HorizontalAlignment.Center;
        grid.Children.Add(rightBox);
        left.Margin = new Thickness(0, 0, 14, 0);
        return grid;
    }

    private FrameworkElement HistoryChart()
    {
        var from = Math.Max(0, StartMin / 60);
        var to = Math.Min(23, Math.Max(from + 1, (EndMin - 1) / 60));
        var max = Math.Max(Dose, _hours.Skip(from).Take(to - from + 1).DefaultIfEmpty(0).Max());
        var bars = new StackPanel { Orientation = Orientation.Horizontal, Height = 60, HorizontalAlignment = HorizontalAlignment.Center };
        var count = to - from + 1;
        var w = Math.Max(3, Math.Min(10, 130.0 / count - 2));
        for (var h = from; h <= to; h++)
        {
            var bar = new Border
            {
                Width = w,
                Height = Math.Max(3, 60 * _hours[h] / (double)max),
                CornerRadius = new CornerRadius(w / 2),
                Background = h == DateTime.Now.Hour ? NotchTheme.Accent : _hours[h] > 0 ? NotchTheme.AccentDeep : NotchTheme.Surface,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(1, 0, 1, 0),
                ToolTip = $"{h:00}h · {_hours[h]} ml",
            };
            bars.Children.Add(bar);
        }
        var labels = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        var l1 = Ui.Text($"{from:00}h", 9, null, NotchTheme.TextDim);
        var l2 = Ui.Text($"{to:00}h", 9, null, NotchTheme.TextDim);
        l2.HorizontalAlignment = HorizontalAlignment.Right;
        labels.Children.Add(l1);
        labels.Children.Add(l2);
        return Ui.Column(2, Ui.Text("Por hora", 10, FontWeights.SemiBold, NotchTheme.TextDim), bars, labels);
    }

    private FrameworkElement SettingsView(Action render)
    {
        StackPanel Option(string label, string key, int[] values, Func<int, string> fmt, int current)
        {
            var row = Ui.Row(4, Ui.Text(label, 11, FontWeights.SemiBold, NotchTheme.TextDim));
            ((FrameworkElement)row.Children[0]).Width = 52;
            foreach (var v in values)
            {
                var chip = Ui.Chip(fmt(v), current == v, () => { _s.Set(key, v); if (key == "bottle") _s.Set("inb", v); Save(); });
                chip.Margin = new Thickness(4, 0, 0, 0);
                row.Children.Add(chip);
            }
            return row;
        }

        return Ui.Column(6,
            Option("Garrafa", "bottle", [500, 750, 1000, 1500], v => $"{v} ml", Bottle),
            Option("Meta", "goal", [1500, 2000, 2500, 3000], v => $"{Liters(v)} L", Goal),
            Option("Dose", "dose", [150, 200, 250, 300], v => $"{v} ml", Dose),
            Ui.Row(8,
                Ui.Button(Glyphs.Check, "Pronto", () => { _panel = "main"; render(); }, NotchTheme.AccentDeep),
                Ui.Button(Glyphs.Refresh, "Reabasteci", Refill),
                Ui.Button(Glyphs.Reset, "Zerar dia", () => { _panel = "confirm"; render(); })));
    }

    /// <summary>Tela "Lembretes": liga/desliga, de quanto em quanto tempo e em que horário.</summary>
    private FrameworkElement RemindersView(Action render)
    {
        TextBlock Label(string text)
        {
            var t = Ui.Text(text, 11, FontWeights.SemiBold, NotchTheme.TextDim);
            t.Width = 52;
            return t;
        }

        var onRow = Ui.Row(8, Label("Lembrar"), Ui.Toggle(RemindersOn, on => { _s.Set("on", on); Save(); }, Glyphs.Bell),
            Ui.Text(RemindersOn ? "ligado" : "desligado", 11, FontWeights.SemiBold, RemindersOn ? NotchTheme.Text : NotchTheme.TextDim));

        var every = Ui.Row(4, Label("A cada"));
        foreach (var m in new[] { 0, 30, 45, 60, 90, 120 })
        {
            var text = m == 0 ? "Auto" : Duration(m);
            var chip = Ui.Chip(text, EveryMin == m, () =>
            {
                _s.Set("every", m);
                // novo intervalo começa a contar agora
                _s.Set("last", DateTime.Now.Hour * 60 + DateTime.Now.Minute);
                _snoozeUntil = default;
                Save();
            });
            chip.ToolTip = m == 0 ? $"Divide a meta ao longo do horário (hoje: a cada {Duration(AutoIntervalMin)})" : null;
            chip.Margin = new Thickness(4, 0, 0, 0);
            every.Children.Add(chip);
        }

        Border Step(string glyph, string key, int delta) => Ui.IconButton(glyph, () =>
        {
            var v = Math.Clamp(_s.GetInt(key, key == "start" ? 480 : 1080) + delta, 0, 24 * 60);
            if (key == "start" && v >= EndMin) return;
            if (key == "end" && v <= StartMin) return;
            _s.Set(key, v);
            Save();
        }, 20, null, null, 9);
        var hours = Ui.Row(4, Label("Horário"),
            Step(Glyphs.ChevronLeft, "start", -60), Ui.Text(Hm(StartMin), 11, FontWeights.SemiBold), Step(Glyphs.ChevronRight, "start", 60),
            Ui.Text("até", 11, null, NotchTheme.TextDim),
            Step(Glyphs.ChevronLeft, "end", -60), Ui.Text(Hm(EndMin), 11, FontWeights.SemiBold), Step(Glyphs.ChevronRight, "end", 60));

        var bottom = new Grid();
        bottom.Children.Add(Ui.Row(8,
            Ui.Button(Glyphs.Check, "Pronto", () => { _panel = "main"; render(); }, NotchTheme.AccentDeep),
            Ui.Button(Glyphs.Bell, "Testar", ShowReminder)));
        var status = Ui.Text(ReminderStatus(), 11, null, NotchTheme.Accent);
        status.HorizontalAlignment = HorizontalAlignment.Right;
        status.MaxWidth = 170;
        bottom.Children.Add(status);

        var col = Ui.Column(6, onRow, every, hours, bottom);
        col.VerticalAlignment = VerticalAlignment.Center;
        return col;
    }

    private FrameworkElement ConfirmView(Action render)
    {
        var col = Ui.Column(10,
            Ui.Text("Tem certeza?", 16, FontWeights.Bold),
            Ui.Text("O consumo de hoje volta para zero.", 12, null, NotchTheme.TextDim),
            Ui.Row(8,
                Ui.Button(Glyphs.Reset, "Zerar", () =>
                {
                    _s.Set("taken", 0);
                    _s.Set("last", 0);
                    _s.Set("inb", Bottle);
                    _hours = new int[24];
                    _panel = "main";
                    Save();
                }, NotchTheme.Solid(0x5C, 0x1A, 0x16), NotchTheme.Red),
                Ui.Button(null, "Cancelar", () => { _panel = "settings"; render(); })));
        col.VerticalAlignment = VerticalAlignment.Center;
        return col;
    }
}
