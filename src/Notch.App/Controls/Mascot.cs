using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Notch.Sdk;

namespace Notch.App.Controls;

/// <summary>Mascote: dois olhinhos que piscam e olham para os lados.</summary>
internal sealed class Mascot : FrameworkElement
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly Random _random = new();
    private double _blink = 1;     // 1 = aberto, 0 = fechado
    private double _look;          // -1..1
    private double _lookTarget;
    private int _blinkFrames;
    private int _ticksToBlink = 25;

    public bool Happy { get; set; }

    public Mascot()
    {
        Width = 22;
        Height = 14;
        _timer.Tick += (_, _) => Tick();
        IsVisibleChanged += (_, _) => { if (IsVisible) _timer.Start(); else _timer.Stop(); };
    }

    private void Tick()
    {
        if (_blinkFrames > 0)
        {
            _blinkFrames--;
            _blink = _blinkFrames == 1 ? 0.1 : 1;
        }
        else if (--_ticksToBlink <= 0)
        {
            _blinkFrames = 2;
            _ticksToBlink = 20 + _random.Next(30);
            if (_random.NextDouble() < 0.4) _lookTarget = _random.Next(-1, 2) * 0.8;
        }
        _look += (_lookTarget - _look) * 0.5;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var eyeW = 5.0;
        var eyeH = Math.Max(1.4, 9.0 * _blink);
        var spacing = 5.0;
        var total = eyeW * 2 + spacing;
        var x0 = (ActualWidth - total) / 2 + _look * 2.5;
        var y = (ActualHeight - eyeH) / 2;
        if (Happy)
        {
            var pen = new Pen(NotchTheme.Text, 1.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            for (var i = 0; i < 2; i++)
            {
                var x = x0 + i * (eyeW + spacing);
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(new Point(x, ActualHeight / 2 + 1.5), false, false);
                    ctx.QuadraticBezierTo(new Point(x + eyeW / 2, ActualHeight / 2 - 3.5), new Point(x + eyeW, ActualHeight / 2 + 1.5), true, false);
                }
                dc.DrawGeometry(null, pen, g);
            }
            return;
        }
        dc.DrawRoundedRectangle(NotchTheme.Text, null, new Rect(x0, y, eyeW, eyeH), 2.5, 2.5);
        dc.DrawRoundedRectangle(NotchTheme.Text, null, new Rect(x0 + eyeW + spacing, y, eyeW, eyeH), 2.5, 2.5);
    }
}
