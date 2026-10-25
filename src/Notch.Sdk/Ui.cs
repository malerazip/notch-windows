using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Notch.Sdk;

/// <summary>Construtores de elementos com o visual do notch (sem XAML).</summary>
public static class Ui
{
    public static TextBlock Text(string text, double size = 12, FontWeight? weight = null, Brush? color = null)
    {
        return new TextBlock
        {
            Text = text,
            FontFamily = NotchTheme.Font,
            FontSize = size,
            FontWeight = weight ?? FontWeights.Normal,
            Foreground = color ?? NotchTheme.Text,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    public static TextBlock Icon(string glyph, double size = 13, Brush? color = null)
    {
        return new TextBlock
        {
            Text = glyph,
            FontFamily = NotchTheme.Icons,
            FontSize = size,
            Foreground = color ?? NotchTheme.Text,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
    }

    public static StackPanel Row(double spacing, params UIElement[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        for (var i = 0; i < children.Length; i++)
        {
            if (i > 0 && children[i] is FrameworkElement fe) fe.Margin = new Thickness(spacing, fe.Margin.Top, fe.Margin.Right, fe.Margin.Bottom);
            panel.Children.Add(children[i]);
        }
        return panel;
    }

    public static StackPanel Column(double spacing, params UIElement[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        for (var i = 0; i < children.Length; i++)
        {
            if (i > 0 && children[i] is FrameworkElement fe) fe.Margin = new Thickness(fe.Margin.Left, spacing, fe.Margin.Right, fe.Margin.Bottom);
            panel.Children.Add(children[i]);
        }
        return panel;
    }

    /// <summary>Faz qualquer Border se comportar como botão (hover, clique).</summary>
    public static T Clickable<T>(T border, Action onClick, Brush? hover = null) where T : Border
    {
        var normal = border.Background;
        border.Cursor = Cursors.Hand;
        border.MouseEnter += (_, _) => border.Background = hover ?? Lighten(normal);
        border.MouseLeave += (_, _) => { border.Background = normal; border.Opacity = 1; };
        border.MouseLeftButtonDown += (_, e) => { border.Opacity = 0.7; e.Handled = true; };
        border.MouseLeftButtonUp += (_, e) =>
        {
            border.Opacity = 1;
            if (border.IsMouseOver) onClick();
            e.Handled = true;
        };
        return border;
    }

    /// <summary>Botão em pílula com ícone e/ou texto (ex.: "Beber", "Ajustes").</summary>
    public static Border Button(string? glyph, string? text, Action onClick, Brush? background = null, Brush? foreground = null)
    {
        var fg = foreground ?? NotchTheme.Text;
        var content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (glyph != null) content.Children.Add(Icon(glyph, 12, fg));
        if (text != null)
        {
            var t = Text(text, 11.5, FontWeights.SemiBold, fg);
            if (glyph != null) t.Margin = new Thickness(6, 0, 0, 0);
            content.Children.Add(t);
        }
        var border = new Border
        {
            Background = background ?? NotchTheme.Surface,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(text == null ? 7 : 12, 6, text == null ? 7 : 12, 6),
            Child = content,
            VerticalAlignment = VerticalAlignment.Center,
        };
        return Clickable(border, onClick);
    }

    /// <summary>Botão redondo só com ícone.</summary>
    public static Border IconButton(string glyph, Action onClick, double size = 28, Brush? background = null, Brush? foreground = null, double iconSize = 12)
    {
        var border = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            Background = background ?? NotchTheme.Surface,
            Child = Icon(glyph, iconSize, foreground),
            VerticalAlignment = VerticalAlignment.Center,
        };
        return Clickable(border, onClick);
    }

    /// <summary>Chip selecionável (usado em opções como 500 ml / 750 ml).</summary>
    public static Border Chip(string text, bool selected, Action onClick)
    {
        var border = new Border
        {
            Background = selected ? NotchTheme.AccentDeep : NotchTheme.Surface,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(9, 3, 9, 3),
            Child = Text(text, 11, FontWeights.SemiBold, selected ? NotchTheme.Text : NotchTheme.TextDim),
        };
        return Clickable(border, onClick);
    }

    /// <summary>Interruptor liga/desliga no estilo iOS.</summary>
    public static Border Toggle(bool initial, Action<bool> changed, string? glyph = null)
    {
        var on = initial;
        var knob = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(8),
            Background = NotchTheme.Text,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        if (glyph != null) knob.Child = Icon(glyph, 9, NotchTheme.AccentDeep);
        var track = new Border
        {
            Width = 36,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(2),
            Child = knob,
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
        };
        void Apply()
        {
            track.Background = on ? NotchTheme.Accent : NotchTheme.SurfaceStrong;
            knob.HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }
        Apply();
        track.MouseLeftButtonUp += (_, e) =>
        {
            on = !on;
            Apply();
            changed(on);
            e.Handled = true;
        };
        return track;
    }

    /// <summary>Barra de progresso fina e arredondada.</summary>
    public static Grid ProgressBar(double value, Brush? fill = null, double height = 4)
    {
        var grid = new Grid { Height = height, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(new Border { Background = NotchTheme.Surface, CornerRadius = new CornerRadius(height / 2) });
        var bar = new Border
        {
            Background = fill ?? NotchTheme.Accent,
            CornerRadius = new CornerRadius(height / 2),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        grid.Children.Add(bar);
        void Resize() => bar.Width = Math.Max(0, Math.Min(1, (double)(grid.Tag ?? 0d))) * grid.ActualWidth;
        grid.Tag = value;
        grid.SizeChanged += (_, _) => Resize();
        return grid;
    }

    /// <summary>Atualiza o valor (0..1) de uma barra criada por <see cref="ProgressBar"/>.</summary>
    public static void SetProgress(Grid progressBar, double value)
    {
        progressBar.Tag = value;
        if (progressBar.Children.Count > 1 && progressBar.Children[1] is Border bar)
            bar.Width = Math.Max(0, Math.Min(1, value)) * progressBar.ActualWidth;
    }

    /// <summary>Caixa de texto escura com texto de exemplo (placeholder).</summary>
    public static Grid Input(string placeholder, out TextBox box, string initial = "")
    {
        box = new TextBox
        {
            Text = initial,
            Background = Brushes.Transparent,
            Foreground = NotchTheme.Text,
            CaretBrush = NotchTheme.Text,
            SelectionBrush = NotchTheme.Accent,
            BorderThickness = new Thickness(0),
            FontFamily = NotchTheme.Font,
            FontSize = 12,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(8, 0, 8, 0),
        };
        var hint = Text(placeholder, 12, null, NotchTheme.TextDim);
        hint.Margin = new Thickness(10, 0, 0, 0);
        hint.IsHitTestVisible = false;
        var tb = box;
        void Refresh() => hint.Visibility = tb.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        tb.TextChanged += (_, _) => Refresh();
        Refresh();
        var grid = new Grid { Height = 28 };
        grid.Children.Add(new Border { Background = NotchTheme.Surface, CornerRadius = new CornerRadius(8) });
        grid.Children.Add(hint);
        grid.Children.Add(tb);
        return grid;
    }

    private static Brush Lighten(Brush? brush)
    {
        if (brush is SolidColorBrush s)
        {
            var c = s.Color;
            if (c.A == 0) return NotchTheme.Surface;
            byte L(byte v) => (byte)Math.Min(255, v + 22);
            var b = new SolidColorBrush(Color.FromArgb(c.A, L(c.R), L(c.G), L(c.B)));
            b.Freeze();
            return b;
        }
        return NotchTheme.SurfaceHover;
    }
}
