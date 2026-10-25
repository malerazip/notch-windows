using System.Windows;
using System.Windows.Media;
using Notch.Sdk;

namespace Notch.App.Controls;

/// <summary>Barrinhas de áudio que reagem ao volume real do som que está tocando.</summary>
internal sealed class AudioBars : FrameworkElement
{
    private const int Count = 5;
    private readonly double[] _levels = new double[Count];
    private readonly double[] _targets = new double[Count];
    private readonly Random _random = new();
    private bool _animating;

    public Brush Fill { get; set; } = NotchTheme.Text;

    /// <summary>Retorna o pico de áudio atual (0..1). Definido pelo app.</summary>
    public static Func<float>? PeakSource { get; set; }

    public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(
        nameof(IsPlaying), typeof(bool), typeof(AudioBars), new PropertyMetadata(false, (d, _) => ((AudioBars)d).UpdateAnimation()));

    public bool IsPlaying
    {
        get => (bool)GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public AudioBars()
    {
        Width = 22;
        Height = 14;
        IsVisibleChanged += (_, _) => UpdateAnimation();
        for (var i = 0; i < Count; i++) _levels[i] = 0.15;
    }

    private void UpdateAnimation()
    {
        var shouldAnimate = IsPlaying && IsVisible;
        if (shouldAnimate == _animating) return;
        _animating = shouldAnimate;
        if (shouldAnimate) CompositionTarget.Rendering += OnFrame;
        else
        {
            CompositionTarget.Rendering -= OnFrame;
            for (var i = 0; i < Count; i++) _levels[i] = 0.15;
            InvalidateVisual();
        }
    }

    private int _frame;

    private void OnFrame(object? sender, EventArgs e)
    {
        // ~30 fps é suficiente e economiza CPU
        if (++_frame % 2 != 0) return;
        var peak = Math.Clamp(PeakSource?.Invoke() ?? 0.5f, 0f, 1f);
        var energy = Math.Pow(peak, 0.6);
        for (var i = 0; i < Count; i++)
        {
            if (_random.NextDouble() < 0.35)
            {
                var shape = i is 1 or 3 ? 1.0 : i == 2 ? 0.85 : 0.6;
                _targets[i] = 0.15 + energy * shape * (0.45 + _random.NextDouble() * 0.55);
            }
            _levels[i] += (_targets[i] - _levels[i]) * 0.45;
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var barWidth = 2.4;
        var gap = (ActualWidth - Count * barWidth) / (Count - 1);
        for (var i = 0; i < Count; i++)
        {
            var h = Math.Max(2.4, _levels[i] * ActualHeight);
            var x = i * (barWidth + gap);
            var y = (ActualHeight - h) / 2;
            dc.DrawRoundedRectangle(Fill, null, new Rect(x, y, barWidth, h), 1.2, 1.2);
        }
    }
}
