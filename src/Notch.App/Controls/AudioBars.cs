using System.Windows;
using System.Windows.Media;
using Notch.Sdk;

namespace Notch.App.Controls;

/// <summary>
/// Barrinhas de áudio em formato de onda (como no Nusa): grossas e altas para ficar claro que há som tocando,
/// e reagindo ao volume real do que está tocando.
/// </summary>
internal sealed class AudioBars : FrameworkElement
{
    private const int Count = 5;
    private const double BarWidth = 3.2;

    // formato de onda: as do meio são mais altas
    private static readonly double[] Shape = [0.55, 0.85, 1.0, 0.85, 0.55];

    // altura mínima enquanto toca, para nunca virar "pontinhos" com som baixo
    private const double MinPlaying = 0.4;

    private readonly double[] _levels = new double[Count];
    private readonly double[] _targets = new double[Count];
    private readonly Random _random = new();
    private bool _animating;
    private int _frame;

    // ganho automático: o maior pico recente, que decai devagar (som baixo também faz as barras dançarem)
    private double _peakMax = 0.05;

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
        Width = 24;
        Height = 16;
        IsVisibleChanged += (_, _) => UpdateAnimation();
        ResetLevels();
    }

    private void ResetLevels()
    {
        // parado: onda baixa e discreta
        for (var i = 0; i < Count; i++) _levels[i] = _targets[i] = Shape[i] * 0.35;
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
            ResetLevels();
            InvalidateVisual();
        }
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        // ~30 fps é suficiente e economiza CPU
        if (++_frame % 2 != 0) return;
        var peak = Math.Clamp(PeakSource?.Invoke() ?? 0.6f, 0f, 1f);

        // Normaliza pelo maior pico recente: uma transmissão baixa (pico ~0,02) e uma música alta (~0,5)
        // geram a mesma amplitude visual. Silêncio de verdade (< 0,002) deixa a onda baixa.
        _peakMax = Math.Max(Math.Max(peak, 0.01), _peakMax * 0.992);
        var norm = peak < 0.002 ? 0 : Math.Clamp(peak / _peakMax, 0, 1);
        var energy = MinPlaying + (1 - MinPlaying) * Math.Sqrt(norm);

        for (var i = 0; i < Count; i++)
        {
            if (_random.NextDouble() < 0.45)
            {
                var wobble = 0.6 + _random.NextDouble() * 0.4;
                _targets[i] = Shape[i] * Math.Max(MinPlaying, energy * wobble);
            }
            _levels[i] += (_targets[i] - _levels[i]) * 0.5;
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var gap = (ActualWidth - Count * BarWidth) / (Count - 1);
        var fill = IsPlaying ? Fill : NotchTheme.TextDim;
        for (var i = 0; i < Count; i++)
        {
            var h = Math.Max(BarWidth, Math.Min(1, _levels[i]) * ActualHeight);
            var x = i * (BarWidth + gap);
            var y = (ActualHeight - h) / 2;
            dc.DrawRoundedRectangle(fill, null, new Rect(x, y, BarWidth, h), BarWidth / 2, BarWidth / 2);
        }
    }
}
