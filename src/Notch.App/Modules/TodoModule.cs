using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Notch.Sdk;

namespace Notch.App.Modules;

internal sealed class TodoItem
{
    public string Title { get; set; } = "";
    public bool Done { get; set; }
    public string DoneDay { get; set; } = "";
    /// <summary>Horário opcional "HH:mm" para lembrar.</summary>
    public string Time { get; set; } = "";
    public string NotifiedDay { get; set; } = "";
}

internal sealed class TodoModule : ModuleBase
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(15) };
    private readonly List<TodoItem> _items = new();
    private string _file = "";
    private TodoItem? _editing;
    private bool _adding;

    public override string Id => "tarefas";
    public override string Title => "Lista de";
    public override string TitleAccent => "Tarefas";
    public override int Order => 50;

    private event Action? Changed;

    private static string Today => DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    public override void Start(INotchHost host)
    {
        base.Start(host);
        _file = Path.Combine(host.DataDirectory, "todo.txt");
        Load();
        _timer.Tick += (_, _) => CheckTimes();
        _timer.Start();
    }

    // Formato: feito|diaFeito|hora|diaAvisado|título
    private void Load()
    {
        _items.Clear();
        if (!File.Exists(_file)) return;
        foreach (var line in File.ReadAllLines(_file))
        {
            var p = line.Split('|', 5);
            if (p.Length < 5) continue;
            var item = new TodoItem { Done = p[0] == "1", DoneDay = p[1], Time = p[2], NotifiedDay = p[3], Title = p[4] };
            if (item.Done && item.DoneDay != Today) continue; // concluídas em dias anteriores saem da lista
            _items.Add(item);
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllLines(_file, _items.Select(i => $"{(i.Done ? 1 : 0)}|{i.DoneDay}|{i.Time}|{i.NotifiedDay}|{i.Title.Replace('\n', ' ')}"));
        }
        catch (IOException) { }
        Changed?.Invoke();
    }

    private void CheckTimes()
    {
        var now = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        foreach (var item in _items.Where(i => !i.Done && i.Time.Length == 5 && string.CompareOrdinal(i.Time, now) <= 0 && i.NotifiedDay != Today).ToList())
        {
            item.NotifiedDay = Today;
            Save();
            Host.ShowAlert(new NotchAlert("Hora da tarefa!", item.Title)
            {
                Glyph = Glyphs.Clock,
                Accent = NotchTheme.Purple,
                Duration = TimeSpan.FromSeconds(12),
                Actions =
                [
                    new NotchAlertAction("Concluir", () => { item.Done = true; item.DoneDay = Today; Save(); }),
                    new NotchAlertAction("+10 min", () =>
                    {
                        item.Time = DateTime.Now.AddMinutes(10).ToString("HH:mm", CultureInfo.InvariantCulture);
                        item.NotifiedDay = "";
                        Save();
                    }),
                ],
            });
            System.Media.SystemSounds.Asterisk.Play();
        }
    }

    public override FrameworkElement CreateView()
    {
        var root = new ContentControl();
        void Render() => root.Content = _adding || _editing != null ? EditView(Render) : ListView(Render);
        Changed += Render;
        Render();
        return root;
    }

    private FrameworkElement ListView(Action render)
    {
        var pending = _items.Count(i => !i.Done);
        var doneToday = _items.Count(i => i.Done);
        var header = new Grid();
        header.Children.Add(Ui.Row(6,
            Ui.Text(pending == 0 ? "Nada pendente" : $"{pending} pendente{(pending == 1 ? "" : "s")}", 12, FontWeights.SemiBold),
            Ui.Text($"· {doneToday} concluída{(doneToday == 1 ? "" : "s")} hoje", 11, null, NotchTheme.TextDim)));
        var add = Ui.Button(Glyphs.Add, "Adicionar", () => { _adding = true; render(); }, NotchTheme.AccentDeep);
        add.HorizontalAlignment = HorizontalAlignment.Right;
        header.Children.Add(add);

        var list = new StackPanel();
        if (_items.Count == 0)
        {
            var empty = Ui.Text("Sem tarefas. Adicione a primeira!", 12, null, NotchTheme.TextDim);
            empty.Margin = new Thickness(0, 18, 0, 0);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            list.Children.Add(empty);
        }
        foreach (var item in _items.OrderBy(i => i.Done).ThenBy(i => i.Time.Length == 0).ThenBy(i => i.Time))
            list.Children.Add(Row(item, render));

        var scroll = new ScrollViewer
        {
            Content = list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Margin = new Thickness(0, 6, 0, 0),
        };
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.Children.Add(header);
        Grid.SetRow(scroll, 1);
        grid.Children.Add(scroll);
        return grid;
    }

    private FrameworkElement Row(TodoItem item, Action render)
    {
        var check = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1.5),
            BorderBrush = item.Done ? NotchTheme.Green : NotchTheme.TextDim,
            Background = item.Done ? NotchTheme.Green : Brushes.Transparent,
            Child = item.Done ? Ui.Icon(Glyphs.Check, 9, NotchTheme.Background) : null,
            Cursor = Cursors.Hand,
        };
        check.MouseLeftButtonUp += (_, e) =>
        {
            item.Done = !item.Done;
            item.DoneDay = item.Done ? Today : "";
            Save();
            e.Handled = true;
        };

        var title = Ui.Text(item.Title, 12.5, FontWeights.SemiBold, item.Done ? NotchTheme.TextDim : NotchTheme.Text);
        if (item.Done) title.TextDecorations = TextDecorations.Strikethrough;
        title.Margin = new Thickness(10, 0, 0, 0);

        var time = item.Time.Length > 0
            ? new Border
            {
                Background = NotchTheme.Surface,
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(6, 0, 0, 0),
                Child = Ui.Row(3, Ui.Icon(Glyphs.Clock, 9, NotchTheme.Purple), Ui.Text(item.Time, 10, FontWeights.SemiBold, NotchTheme.TextDim)),
            }
            : null;

        var actions = Ui.Row(4,
            Ui.IconButton(Glyphs.Edit, () => { _editing = item; render(); }, 22, null, null, 10),
            Ui.IconButton(Glyphs.Delete, () => { _items.Remove(item); Save(); }, 22, null, NotchTheme.Red, 10));
        actions.Visibility = Visibility.Hidden;

        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3), Background = Brushes.Transparent };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(check);
        Grid.SetColumn(title, 1);
        grid.Children.Add(title);
        if (time != null) { Grid.SetColumn(time, 2); grid.Children.Add(time); }
        Grid.SetColumn(actions, 4);
        grid.Children.Add(actions);
        title.MaxWidth = 250;
        grid.MouseEnter += (_, _) => actions.Visibility = Visibility.Visible;
        grid.MouseLeave += (_, _) => actions.Visibility = Visibility.Hidden;
        return grid;
    }

    private FrameworkElement EditView(Action render)
    {
        var editing = _editing;
        var titleBox = Ui.Input("Título da tarefa", out var titleInput, editing?.Title ?? "");
        var timeBox = Ui.Input("Hora (ex.: 14:30)", out var timeInput, editing?.Time ?? "");
        timeBox.Width = 130;
        var error = Ui.Text("", 10.5, null, NotchTheme.Red);

        void Close()
        {
            _adding = false;
            _editing = null;
            render();
        }

        void Commit()
        {
            var title = titleInput.Text.Trim();
            if (title.Length == 0) { error.Text = "Digite um título"; return; }
            var time = timeInput.Text.Trim().Replace('h', ':').Replace('.', ':');
            if (time.Length > 0)
            {
                if (!TimeSpan.TryParseExact(time, ["h\\:mm", "hh\\:mm", "%h"], CultureInfo.InvariantCulture, out var ts) || ts.TotalHours >= 24)
                { error.Text = "Hora inválida (use HH:mm)"; return; }
                time = $"{ts.Hours:00}:{ts.Minutes:00}";
            }
            var item = editing ?? new TodoItem();
            if (editing == null) _items.Add(item);
            if (item.Time != time) item.NotifiedDay = "";
            item.Title = title;
            item.Time = time;
            _adding = false;
            _editing = null;
            Save();
        }

        titleInput.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(); if (e.Key == Key.Escape) Close(); };
        timeInput.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(); if (e.Key == Key.Escape) Close(); };

        Border Quick(string label, int minutes) => Ui.Chip(label, false, () =>
            timeInput.Text = DateTime.Now.AddMinutes(minutes).ToString("HH:mm", CultureInfo.InvariantCulture));

        var col = Ui.Column(7,
            Ui.Text(editing == null ? "Nova tarefa" : "Editar tarefa", 13, FontWeights.Bold),
            titleBox,
            Ui.Row(6, timeBox, Quick("+30 min", 30), Quick("+1 h", 60), Ui.Chip("Sem hora", false, () => timeInput.Text = "")),
            Ui.Row(8,
                Ui.Button(Glyphs.Check, "Salvar", Commit, NotchTheme.AccentDeep),
                Ui.Button(null, "Cancelar", Close),
                error));

        col.Loaded += (_, _) =>
        {
            Host.RequestKeyboardFocus();
            titleInput.Focus();
            Keyboard.Focus(titleInput);
            titleInput.CaretIndex = titleInput.Text.Length;
        };
        return col;
    }
}
