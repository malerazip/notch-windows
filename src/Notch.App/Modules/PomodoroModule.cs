using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Notch.App.Controls;
using Notch.Sdk;

namespace Notch.App.Modules;

internal enum PomodoroMode { Foco, PausaCurta, PausaLonga }

internal sealed class PomodoroModule : ModuleBase
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private KeyValueFile _store = null!;
    private DateTime _endsAt;
    private TimeSpan _remaining;
    private bool _showSettings;

    public override string Id => "pomodoro";
    public override string Title => "Timer";
    public override string TitleAccent => "Pomodoro";
    public override int Order => 30;

    public PomodoroMode Mode { get; private set; } = PomodoroMode.Foco;
    public bool IsRunning { get; private set; }
    public int CompletedFocus { get; private set; }
    public TimeSpan Remaining => IsRunning ? (_endsAt - DateTime.Now is var r && r > TimeSpan.Zero ? r : TimeSpan.Zero) : _remaining;
    public TimeSpan Length => TimeSpan.FromMinutes(Mode switch
    {
        PomodoroMode.Foco => _store.GetInt("foco", 25),
        PomodoroMode.PausaCurta => _store.GetInt("curta", 5),
        _ => _store.GetInt("longa", 15),
    });
    public Brush ModeBrush => Mode == PomodoroMode.Foco ? NotchTheme.Orange : NotchTheme.Green;
    public string ModeName => Mode switch
    {
        PomodoroMode.Foco => "Foco",
        PomodoroMode.PausaCurta => "Pausa curta",
        _ => "Pausa longa",
    };

    /// <summary>Estado mudou (iniciou, pausou, trocou de modo).</summary>
    public event Action? StateChanged;

    public override void Start(INotchHost host)
    {
        base.Start(host);
        _store = host.OpenStore("pomodoro");
        _remaining = Length;
        _timer.Tick += (_, _) => { if (IsRunning && Remaining <= TimeSpan.Zero) Finish(); };
    }

    public void Toggle()
    {
        if (IsRunning)
        {
            _remaining = Remaining;
            IsRunning = false;
            _timer.Stop();
        }
        else
        {
            if (_remaining <= TimeSpan.Zero) _remaining = Length;
            _endsAt = DateTime.Now + _remaining;
            IsRunning = true;
            _timer.Start();
        }
        StateChanged?.Invoke();
    }

    public void Reset()
    {
        IsRunning = false;
        _timer.Stop();
        _remaining = Length;
        StateChanged?.Invoke();
    }

    public void Skip() => Advance(false);

    private void Finish()
    {
        Advance(true);
        var (title, msg) = Mode switch
        {
            PomodoroMode.Foco => ("Hora do foco!", $"De volta ao trabalho, {Length.TotalMinutes:0} min"),
            PomodoroMode.PausaCurta => ("Hora da pausa!", $"Pausa curta de {Length.TotalMinutes:0} min"),
            _ => ("Pausa longa!", $"{Length.TotalMinutes:0} min, você mereceu"),
        };
        Host.ShowAlert(new NotchAlert(title, msg)
        {
            Glyph = Mode == PomodoroMode.Foco ? Glyphs.Timer : Glyphs.Coffee,
            Accent = ModeBrush,
            Duration = TimeSpan.FromSeconds(10),
            Actions = [new NotchAlertAction("Iniciar", () => { if (!IsRunning) Toggle(); })],
        });
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void Advance(bool completed)
    {
        if (Mode == PomodoroMode.Foco)
        {
            if (completed) CompletedFocus++;
            var every = Math.Max(2, _store.GetInt("ciclos", 4));
            Mode = completed && CompletedFocus % every == 0 ? PomodoroMode.PausaLonga : PomodoroMode.PausaCurta;
        }
        else Mode = PomodoroMode.Foco;
        IsRunning = false;
        _timer.Stop();
        _remaining = Length;
        StateChanged?.Invoke();
    }

    public override FrameworkElement CreateView()
    {
        var root = new Grid();
        var content = new ContentControl();
        root.Children.Add(content);

        // recriados a cada Render: um elemento WPF não pode estar em dois painéis ao mesmo tempo
        TextBlock modeText = null!, time = null!, sessions = null!, ringIcon = null!;
        RingProgress ring = null!;

        Border Main()
        {
            modeText = Ui.Text("", 12, FontWeights.SemiBold);
            time = Ui.Text("25:00", 34, FontWeights.Bold);
            time.FontFamily = new FontFamily("Segoe UI Variable Display Semibold, Segoe UI");
            sessions = Ui.Text("", 11, null, NotchTheme.TextDim);
            ring = new RingProgress { Width = 84, Height = 84, Gap = 0, Thickness = 6 };
            ringIcon = Ui.Icon(Glyphs.Timer, 20);
            var startBtn = Ui.Button(IsRunning ? Glyphs.Pause : Glyphs.Play, IsRunning ? "Pausar" : "Iniciar", () => { Toggle(); }, NotchTheme.AccentDeep);
            var left = Ui.Column(2,
                Ui.Row(6, Ui.Icon(Mode == PomodoroMode.Foco ? Glyphs.Timer : Glyphs.Coffee, 12, ModeBrush), modeText),
                time,
                Ui.Row(8, startBtn,
                    Ui.IconButton(Glyphs.Reset, Reset, 28),
                    Ui.IconButton(Glyphs.Skip, Skip, 28),
                    Ui.IconButton(Glyphs.Settings, () => { _showSettings = true; Render(); }, 28)));
            left.VerticalAlignment = VerticalAlignment.Center;

            var ringGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            ringGrid.Children.Add(ring);
            ringGrid.Children.Add(Ui.Column(0, ringIcon, sessions));
            ((StackPanel)ringGrid.Children[1]).VerticalAlignment = VerticalAlignment.Center;
            sessions.HorizontalAlignment = HorizontalAlignment.Center;

            var g = new Grid();
            g.Children.Add(left);
            g.Children.Add(ringGrid);
            return new Border { Child = g };
        }

        Border Settings()
        {
            StackPanel Option(string label, string key, int[] values, int fallback)
            {
                var row = Ui.Row(5, Ui.Text(label, 11, FontWeights.SemiBold, NotchTheme.TextDim));
                ((FrameworkElement)row.Children[0]).Width = 74;
                foreach (var v in values)
                {
                    var chip = Ui.Chip($"{v} min", _store.GetInt(key, fallback) == v, () =>
                    {
                        _store.Set(key, v);
                        _store.Save();
                        if (!IsRunning) _remaining = Length;
                        Render();
                    });
                    chip.Margin = new Thickness(5, 0, 0, 0);
                    row.Children.Add(chip);
                }
                return row;
            }
            var col = Ui.Column(6,
                Option("Foco", "foco", [15, 25, 45, 50], 25),
                Option("Pausa curta", "curta", [3, 5, 10], 5),
                Option("Pausa longa", "longa", [10, 15, 20, 30], 15),
                Ui.Row(8, Ui.Button(Glyphs.Check, "Pronto", () => { _showSettings = false; Render(); }, NotchTheme.AccentDeep)));
            return new Border { Child = col, VerticalAlignment = VerticalAlignment.Center };
        }

        void Render()
        {
            content.Content = _showSettings ? Settings() : Main();
            Tick();
        }

        void Tick()
        {
            if (_showSettings || time == null) return;
            var rem = Remaining;
            modeText.Text = ModeName + (IsRunning ? "" : rem < Length ? " · pausado" : "");
            modeText.Foreground = ModeBrush;
            time.Text = $"{(int)rem.TotalMinutes:00}:{rem.Seconds:00}";
            ring.Fill = ModeBrush;
            ring.Value = 1 - rem.TotalSeconds / Math.Max(1, Length.TotalSeconds);
            ringIcon.Foreground = ModeBrush;
            ringIcon.Text = Mode == PomodoroMode.Foco ? Glyphs.Timer : Glyphs.Coffee;
            sessions.Text = $"{CompletedFocus} ciclo{(CompletedFocus == 1 ? "" : "s")}";
        }

        StateChanged += Render;
        Render();
        WhileVisible(root, TimeSpan.FromMilliseconds(250), Tick);
        return root;
    }
}
