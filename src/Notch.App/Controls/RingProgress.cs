using System.Windows;
using System.Windows.Media;
using Notch.Sdk;

namespace Notch.App.Controls;

/// <summary>Anel de progresso (medidor da água, pomodoro).</summary>
internal sealed class RingProgress : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(RingProgress), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Brush Track { get; set; } = NotchTheme.Surface;
    public Brush Fill { get; set; } = NotchTheme.Accent;
    public double Thickness { get; set; } = 7;

    /// <summary>Abertura embaixo, em graus (0 = círculo completo).</summary>
    public double Gap { get; set; } = 70;

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= Thickness) return;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = (size - Thickness) / 2;
        var start = 90 + Gap / 2;
        var sweep = 360 - Gap;

        var trackPen = new Pen(Track, Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawGeometry(null, trackPen, Arc(center, radius, start, sweep));

        var v = Math.Clamp(Value, 0, 1);
        if (v > 0.001)
        {
            var fillPen = new Pen(Fill, Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawGeometry(null, fillPen, Arc(center, radius, start, sweep * v));
        }
    }

    private static Geometry Arc(Point c, double r, double startDeg, double sweepDeg)
    {
        if (sweepDeg >= 359.9) return new EllipseGeometry(c, r, r);
        Point P(double deg) => new(c.X + r * Math.Cos(deg * Math.PI / 180), c.Y + r * Math.Sin(deg * Math.PI / 180));
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(P(startDeg), false, false);
            ctx.ArcTo(P(startDeg + sweepDeg), new Size(r, r), 0, sweepDeg > 180, SweepDirection.Clockwise, true, false);
        }
        geo.Freeze();
        return geo;
    }
}
