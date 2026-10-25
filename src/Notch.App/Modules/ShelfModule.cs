using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Notch.App.Core;
using Notch.Sdk;

namespace Notch.App.Modules;

/// <summary>
/// Prateleira: solte arquivos no notch para guardá-los temporariamente e arraste para fora quando quiser.
/// Os arquivos são copiados para %LOCALAPPDATA%\ProjetoNotch\Shelf.
/// </summary>
internal sealed class ShelfModule : ModuleBase
{
    private static readonly HashSet<string> ImageExt = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };

    public override string Id => "prateleira";
    public override string Title => "Prateleira";
    public override string TitleAccent => "de Arquivos";
    public override int Order => 60;

    private event Action? Changed;

    /// <summary>Recebe o que foi solto no notch (arquivos, pastas, links ou texto).</summary>
    public async void Accept(IDataObject data)
    {
        try
        {
            if (data.GetData(DataFormats.FileDrop) is string[] files)
            {
                await Task.Run(() =>
                {
                    foreach (var src in files) CopyIn(src);
                });
            }
            else if (data.GetData(DataFormats.UnicodeText) is string text && text.Trim().Length > 0)
            {
                text = text.Trim();
                if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
                {
                    var name = SafeName(uri.Host + uri.AbsolutePath.TrimEnd('/').Replace('/', '-'));
                    File.WriteAllText(Unique(Path.Combine(Paths.Shelf, name + ".url")), $"[InternetShortcut]\r\nURL={uri}\r\n");
                }
                else
                {
                    var first = SafeName(new string(text.Take(30).ToArray()));
                    File.WriteAllText(Unique(Path.Combine(Paths.Shelf, (first.Length > 0 ? first : "texto") + ".txt")), text);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Host.ShowAlert(new NotchAlert("Não foi possível guardar", ex.Message) { Glyph = Glyphs.Folder, Accent = NotchTheme.Red });
        }
        Changed?.Invoke();
    }

    private static void CopyIn(string src)
    {
        if (File.Exists(src))
            File.Copy(src, Unique(Path.Combine(Paths.Shelf, Path.GetFileName(src))));
        else if (Directory.Exists(src))
            CopyDir(src, Unique(Path.Combine(Paths.Shelf, Path.GetFileName(src.TrimEnd('\\')))));
    }

    private static void CopyDir(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(src)) CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
    }

    private static string Unique(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var i = 2; ; i++)
        {
            var p = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(p) && !Directory.Exists(p)) return p;
        }
    }

    private static string SafeName(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Trim().TrimEnd('.');
    }

    private static List<string> Items()
    {
        if (!Directory.Exists(Paths.Shelf)) return [];
        return Directory.GetFileSystemEntries(Paths.Shelf)
            .OrderByDescending(p => File.GetCreationTime(p))
            .ToList();
    }

    private void Clear()
    {
        foreach (var p in Items())
        {
            try
            {
                if (Directory.Exists(p)) Directory.Delete(p, true);
                else File.Delete(p);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        Changed?.Invoke();
    }

    public override FrameworkElement CreateView()
    {
        var root = new ContentControl();
        void Render() => root.Content = View();
        Changed += Render;
        root.IsVisibleChanged += (_, _) => { if (root.IsVisible) Render(); };
        Render();
        return root;
    }

    private FrameworkElement View()
    {
        var items = Items();
        if (items.Count == 0)
        {
            var text = Ui.Column(3,
                Ui.Icon(Glyphs.Folder, 22, NotchTheme.TextDim),
                Ui.Text("Solte arquivos aqui", 13, FontWeights.SemiBold),
                Ui.Text("Arraste para fora quando quiser", 11, null, NotchTheme.TextDim));
            foreach (FrameworkElement c in text.Children) c.HorizontalAlignment = HorizontalAlignment.Center;
            text.VerticalAlignment = VerticalAlignment.Center;
            var drop = new Grid { Background = Brushes.Transparent };
            drop.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Stroke = NotchTheme.SurfaceStrong,
                StrokeThickness = 1.5,
                StrokeDashArray = [4, 3],
                RadiusX = 14,
                RadiusY = 14,
            });
            drop.Children.Add(text);
            return drop;
        }

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var path in items) panel.Children.Add(Tile(path));
        var scroll = new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        scroll.PreviewMouseWheel += (_, e) =>
        {
            scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset - e.Delta / 2.0);
            e.Handled = true;
        };

        var header = new Grid();
        header.Children.Add(Ui.Text($"{items.Count} {(items.Count == 1 ? "item" : "itens")} · clique para abrir, arraste para levar", 11, null, NotchTheme.TextDim));
        var buttons = Ui.Row(6,
            Ui.Button(Glyphs.Folder, null, () => Process.Start(new ProcessStartInfo(Paths.Shelf) { UseShellExecute = true })),
            Ui.Button(Glyphs.Delete, "Limpar", Clear));
        buttons.HorizontalAlignment = HorizontalAlignment.Right;
        header.Children.Add(buttons);

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.Children.Add(header);
        Grid.SetRow(scroll, 1);
        scroll.Margin = new Thickness(0, 6, 0, 0);
        grid.Children.Add(scroll);
        return grid;
    }

    private FrameworkElement Tile(string path)
    {
        var name = Path.GetFileName(path);
        var image = new Image { Width = 38, Height = 38, Source = Thumb(path), Stretch = Stretch.Uniform };
        var label = new TextBlock
        {
            Text = name,
            FontFamily = NotchTheme.Font,
            FontSize = 10,
            Foreground = NotchTheme.Text,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxHeight = 26,
            Width = 64,
            Margin = new Thickness(0, 3, 0, 0),
        };
        var remove = Ui.IconButton(Glyphs.Close, () =>
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
                else File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            Changed?.Invoke();
        }, 16, NotchTheme.SurfaceStrong, null, 7);
        remove.HorizontalAlignment = HorizontalAlignment.Right;
        remove.VerticalAlignment = VerticalAlignment.Top;
        remove.Visibility = Visibility.Hidden;

        var content = new StackPanel { Margin = new Thickness(4) };
        content.Children.Add(image);
        content.Children.Add(label);
        var g = new Grid();
        g.Children.Add(content);
        g.Children.Add(remove);
        var tile = new Border
        {
            Child = g,
            CornerRadius = new CornerRadius(10),
            Background = Brushes.Transparent,
            Margin = new Thickness(0, 0, 4, 0),
            ToolTip = name,
            Cursor = Cursors.Hand,
        };

        Point? down = null;
        tile.MouseEnter += (_, _) => { tile.Background = NotchTheme.Surface; remove.Visibility = Visibility.Visible; };
        tile.MouseLeave += (_, _) => { tile.Background = Brushes.Transparent; remove.Visibility = Visibility.Hidden; };
        tile.MouseLeftButtonDown += (_, e) => { down = e.GetPosition(tile); e.Handled = true; };
        tile.MouseMove += (_, e) =>
        {
            if (down is not { } start || e.LeftButton != MouseButtonState.Pressed) return;
            var p = e.GetPosition(tile);
            if (Math.Abs(p.X - start.X) < 4 && Math.Abs(p.Y - start.Y) < 4) return;
            down = null;
            var data = new DataObject(DataFormats.FileDrop, new[] { path });
            DragDrop.DoDragDrop(tile, data, DragDropEffects.Copy | DragDropEffects.Move);
            Changed?.Invoke();
        };
        tile.MouseLeftButtonUp += (_, e) =>
        {
            if (down != null)
            {
                down = null;
                try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
            }
            e.Handled = true;
        };
        return tile;
    }

    private static ImageSource? Thumb(string path)
    {
        try
        {
            if (ImageExt.Contains(Path.GetExtension(path)) && new FileInfo(path).Length < 30_000_000)
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 76;
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            if (Directory.Exists(path)) path = Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\explorer.exe";
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon == null) return null;
            var src = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        catch { return null; }
    }
}
