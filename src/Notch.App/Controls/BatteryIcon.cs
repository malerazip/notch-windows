using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Notch.Sdk;

namespace Notch.App.Controls;

/// <summary>Anel de bateria com raio quando está carregando.</summary>
internal sealed class BatteryIcon : FrameworkElement
{
    private double _percent = 1;
    private bool _charging;

    public BatteryIcon()
    {
        Width = 16;
        Height = 16;
        ToolTip = "";
    }

    public void Update(double percent, bool charging)
    {
        _percent = percent;
        _charging = charging;
        ToolTip = $"Bateria {Math.Round(percent * 100)}%" + (charging ? " · carregando" : "");
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var brush = _charging ? NotchTheme.Green
            : _percent <= 0.1 ? NotchTheme.Red
            : _percent <= 0.2 ? NotchTheme.Orange
            : NotchTheme.Green;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        var r = Math.Min(ActualWidth, ActualHeight) / 2 - 1.2;

        dc.DrawEllipse(null, new Pen(NotchTheme.SurfaceStrong, 2), c, r, r);
        var sweep = Math.Clamp(_percent, 0.02, 1) * 360;
        if (sweep >= 359.9) dc.DrawEllipse(null, new Pen(brush, 2), c, r, r);
        else
        {
            Point P(double deg) => new(c.X + r * Math.Cos((deg - 90) * Math.PI / 180), c.Y + r * Math.Sin((deg - 90) * Math.PI / 180));
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(P(0), false, false);
                ctx.ArcTo(P(sweep), new Size(r, r), 0, sweep > 180, SweepDirection.Clockwise, true, false);
            }
            dc.DrawGeometry(null, new Pen(brush, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, g);
        }

        if (_charging)
        {
            var text = new FormattedText(Glyphs.Charging, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(NotchTheme.Icons, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 8, brush, 1.25);
            dc.DrawText(text, new Point(c.X - text.Width / 2, c.Y - text.Height / 2));
        }
    }
}
