using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Notch.App.Services;
using Notch.Sdk;

namespace Notch.App.Modules;

internal sealed class LyricsModule(MediaService media, LyricsService lyrics) : ModuleBase
{
    private Lyrics _current = Lyrics.None;
    private string _status = "";
    private CancellationTokenSource? _cts;

    public override string Id => "letra";
    public override string Title => "Letra";
    public override string TitleAccent => "da Música";
    public override int Order => 20;

    /// <summary>Linha atual da letra (vazia se não houver).</summary>
    public string CurrentLine { get; private set; } = "";

    public event Action? Updated;

    public override void Start(INotchHost host)
    {
        base.Start(host);
        media.TrackChanged += () => _ = LoadAsync();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _current = Lyrics.None;
        CurrentLine = "";
        if (!media.HasSession)
        {
            _status = "Nada tocando";
            Updated?.Invoke();
            return;
        }
        _status = "Carregando letra…";
        Updated?.Invoke();

        // espera a duração chegar (alguns players mandam depois do título)
        await Task.Delay(400, CancellationToken.None);
        if (ct.IsCancellationRequested) return;
        var result = await lyrics.FindAsync(media.Title, media.Artist, media.Album, media.Duration, ct);
        if (ct.IsCancellationRequested) return;
        _current = result;
        _status = result.Instrumental ? "♪ Instrumental ♪"
            : result.IsEmpty ? "Letra não disponível"
            : "";
        Updated?.Invoke();
    }

    public override FrameworkElement CreateView()
    {
        var prev = Line(12, FontWeights.SemiBold, NotchTheme.TextFaint);
        var cur = Line(17, FontWeights.Bold, NotchTheme.Text);
        var next = Line(12, FontWeights.SemiBold, NotchTheme.TextDim);
        var plain = new TextBlock
        {
            FontFamily = NotchTheme.Font,
            FontSize = 12,
            Foreground = NotchTheme.TextDim,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
        };
        var plainScroll = new ScrollViewer
        {
            Content = plain,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Visibility = Visibility.Collapsed,
        };
        var credit = Ui.Text("Letras fornecidas por LRCLIB (lrclib.net)", 9, null, NotchTheme.TextFaint);
        credit.HorizontalAlignment = HorizontalAlignment.Center;
        credit.VerticalAlignment = VerticalAlignment.Bottom;

        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        lines.Children.Add(prev);
        lines.Children.Add(cur);
        lines.Children.Add(next);

        var root = new Grid();
        root.Children.Add(lines);
        root.Children.Add(plainScroll);
        root.Children.Add(credit);

        var lastIndex = -2;
        void Tick()
        {
            var synced = _current.Synced;
            if (synced.Count == 0)
            {
                var hasPlain = _status.Length == 0 && _current.Plain.Length > 0;
                plainScroll.Visibility = hasPlain ? Visibility.Visible : Visibility.Collapsed;
                lines.Visibility = hasPlain ? Visibility.Collapsed : Visibility.Visible;
                if (hasPlain) plain.Text = _current.Plain;
                prev.Text = "";
                next.Text = "";
                cur.Text = _status.Length > 0 ? _status : "";
                lastIndex = -2;
                return;
            }
            plainScroll.Visibility = Visibility.Collapsed;
            lines.Visibility = Visibility.Visible;
            var pos = media.Position + TimeSpan.FromMilliseconds(250);
            var i = synced.Count - 1;
            while (i >= 0 && synced[i].Time > pos) i--;
            if (i == lastIndex) return;
            lastIndex = i;
            prev.Text = i > 0 ? synced[i - 1].Text : "";
            cur.Text = i >= 0 ? (synced[i].Text.Length > 0 ? synced[i].Text : "♪") : "♪";
            next.Text = i + 1 < synced.Count ? synced[i + 1].Text : "";
            CurrentLine = cur.Text;
            var anim = new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase() };
            cur.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        Updated += () => { lastIndex = -2; Tick(); };
        WhileVisible(root, TimeSpan.FromMilliseconds(150), Tick);
        return root;
    }

    private static TextBlock Line(double size, FontWeight weight, System.Windows.Media.Brush color)
    {
        var t = Ui.Text("", size, weight, color);
        t.HorizontalAlignment = HorizontalAlignment.Center;
        t.TextAlignment = TextAlignment.Center;
        t.Margin = new Thickness(0, 2, 0, 2);
        return t;
    }
}
