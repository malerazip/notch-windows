using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Notch.App.Services;
using Notch.Sdk;

namespace Notch.App.Modules;

internal sealed class MusicModule(MediaService media) : ModuleBase
{
    public override string Id => "musica";
    public override string Title => "Tocando";
    public override string TitleAccent => "Agora";
    public override int Order => 10;

    public override FrameworkElement CreateView()
    {
        var art = new Border
        {
            Width = 84,
            Height = 84,
            CornerRadius = new CornerRadius(14),
            Background = NotchTheme.Surface,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var artIcon = Ui.Icon(Glyphs.Music, 28, NotchTheme.TextDim);
        art.Child = artIcon;

        var title = Ui.Text("", 15, FontWeights.Bold);
        var artist = Ui.Text("", 12, null, NotchTheme.TextDim);
        var elapsed = Ui.Text("0:00", 10, null, NotchTheme.TextDim);
        var total = Ui.Text("0:00", 10, null, NotchTheme.TextDim);
        var bar = Ui.ProgressBar(0, NotchTheme.Text, 4);
        bar.Cursor = Cursors.Hand;
        bar.Margin = new Thickness(8, 0, 8, 0);
        bar.MouseLeftButtonUp += (_, e) =>
        {
            if (media.Duration <= TimeSpan.Zero) return;
            var ratio = e.GetPosition(bar).X / Math.Max(1, bar.ActualWidth);
            media.Seek(TimeSpan.FromSeconds(media.Duration.TotalSeconds * ratio));
        };

        var progressRow = new Grid();
        progressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        progressRow.ColumnDefinitions.Add(new ColumnDefinition());
        progressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        progressRow.Children.Add(elapsed);
        Grid.SetColumn(bar, 1);
        progressRow.Children.Add(bar);
        Grid.SetColumn(total, 2);
        progressRow.Children.Add(total);

        var playIcon = Ui.Icon(Glyphs.Play, 14, NotchTheme.Background);
        var play = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(17),
            Background = NotchTheme.Text,
            Child = playIcon,
        };
        Ui.Clickable(play, media.PlayPause, NotchTheme.Solid(0xE5, 0xE5, 0xEA));
        var controls = Ui.Row(14,
            Ui.IconButton(Glyphs.Previous, media.Previous, 30, Brushes.Transparent, iconSize: 14),
            play,
            Ui.IconButton(Glyphs.Next, media.Next, 30, Brushes.Transparent, iconSize: 14));
        controls.HorizontalAlignment = HorizontalAlignment.Center;

        var info = Ui.Column(2, title, artist);
        var right = Ui.Column(8, info, progressRow, controls);
        right.VerticalAlignment = VerticalAlignment.Center;
        right.Margin = new Thickness(14, 0, 0, 0);

        var empty = Ui.Column(4,
            Ui.Text("Nada tocando", 15, FontWeights.Bold),
            Ui.Text("Abra o Spotify, YouTube ou outro player", 12, null, NotchTheme.TextDim));
        empty.VerticalAlignment = VerticalAlignment.Center;
        empty.Margin = new Thickness(14, 0, 0, 0);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(art);
        Grid.SetColumn(right, 1);
        Grid.SetColumn(empty, 1);
        grid.Children.Add(right);
        grid.Children.Add(empty);

        void Refresh()
        {
            var has = media.HasSession;
            right.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            empty.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
            title.Text = media.Title;
            artist.Text = media.Artist;
            playIcon.Text = media.IsPlaying ? Glyphs.Pause : Glyphs.Play;
            if (media.Art != null)
            {
                art.Background = new ImageBrush(media.Art) { Stretch = Stretch.UniformToFill };
                art.Child = null;
            }
            else
            {
                art.Background = NotchTheme.Surface;
                art.Child = artIcon;
            }
            UpdateProgress();
        }

        void UpdateProgress()
        {
            var d = media.Duration;
            var p = media.Position;
            elapsed.Text = Clock(p);
            total.Text = d > TimeSpan.Zero ? Clock(d) : "--:--";
            Ui.SetProgress(bar, d > TimeSpan.Zero ? p.TotalSeconds / d.TotalSeconds : 0);
        }

        media.Changed += Refresh;
        Refresh();
        WhileVisible(grid, TimeSpan.FromMilliseconds(500), UpdateProgress);
        return grid;
    }
}
