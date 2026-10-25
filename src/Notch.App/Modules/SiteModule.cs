using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Linq;
using Notch.Sdk;

namespace Notch.App.Modules;

internal sealed record SiteItem(string Id, string Title, string Summary, string Url, DateTimeOffset? Date);

/// <summary>
/// Novidades do seu site: consulta periodicamente um endereço (JSON, JSON Feed, RSS ou Atom)
/// e mostra um alerta no notch quando aparece algo novo. Veja o formato no README.
/// </summary>
internal sealed class SiteModule : ModuleBase
{
    private static readonly CultureInfo Br = new("pt-BR");
    private readonly DispatcherTimer _timer = new();
    private KeyValueFile _s = null!;
    private List<SiteItem> _items = [];
    private bool _editing;
    private bool _busy;
    private string _status = "";

    public override string Id => "site";
    public override string Title => "Novidades";
    public override string TitleAccent => "do Site";
    public override int Order => 70;

    private event Action? Changed;

    private string Url => _s.Get("url");
    private bool Enabled => _s.GetBool("on", true) && Url.Length > 0;
    private int IntervalMin => Math.Max(1, _s.GetInt("interval", 15));

    public override void Start(INotchHost host)
    {
        base.Start(host);
        _s = host.OpenStore("site");
        _status = _s.Get("status");
        _timer.Tick += async (_, _) => await CheckAsync(false);
        Reschedule();
        if (Enabled) _ = CheckAsync(false);
    }

    private void Reschedule()
    {
        _timer.Stop();
        _timer.Interval = TimeSpan.FromMinutes(IntervalMin);
        if (Enabled) _timer.Start();
    }

    private async Task CheckAsync(bool manual)
    {
        if (_busy || Url.Length == 0) return;
        _busy = true;
        Changed?.Invoke();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, Url);
            var token = _s.Get("token");
            if (token.Length > 0) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Headers.Accept.ParseAdd("application/json, application/feed+json, application/rss+xml, application/atom+xml, text/xml;q=0.9, */*;q=0.5");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var resp = await Host.Http.SendAsync(req, cts.Token);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadAsStringAsync(cts.Token);
            var items = Parse(body);
            _items = items.OrderByDescending(i => i.Date ?? DateTimeOffset.MinValue).ToList();

            var seen = _s.Get("seen").Split('\u001f', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var firstRun = !_s.GetBool("initialized", false);
            var fresh = _items.Where(i => !seen.Contains(i.Id)).ToList();
            if (!firstRun)
            {
                // até 3 alertas; se vier muita coisa de uma vez, um resumo
                if (fresh.Count > 3)
                    Host.ShowAlert(new NotchAlert("Novidades no site", $"{fresh.Count} novos itens publicados")
                    {
                        Glyph = Glyphs.Megaphone,
                        Accent = NotchTheme.Purple,
                        Duration = TimeSpan.FromSeconds(10),
                        Actions = [new NotchAlertAction("Ver", () => Host.ShowPage(Id))],
                    });
                else
                    foreach (var item in fresh)
                        Host.ShowAlert(new NotchAlert(item.Title, item.Summary.Length > 0 ? item.Summary : "Novidade no site")
                        {
                            Glyph = Glyphs.Megaphone,
                            Accent = NotchTheme.Purple,
                            Duration = TimeSpan.FromSeconds(10),
                            Actions = item.Url.Length > 0 ? [new NotchAlertAction("Abrir", () => Open(item.Url))] : [],
                        });
            }
            foreach (var i in _items) seen.Add(i.Id);
            _s.Set("seen", string.Join("\u001f", seen.TakeLast(300)));
            _s.Set("initialized", true);
            _status = $"Verificado às {DateTime.Now:HH:mm} · {_items.Count} itens";
            if (manual && fresh.Count == 0 && !firstRun) _status += " · nada novo";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or System.Xml.XmlException or InvalidOperationException or UriFormatException)
        {
            _status = $"Erro às {DateTime.Now:HH:mm}: " + (ex is TaskCanceledException ? "tempo esgotado" : ex.Message);
            Core.Paths.WriteLog("Site: " + ex.Message);
        }
        finally
        {
            _busy = false;
            _s.Set("status", _status);
            _s.Save();
            Changed?.Invoke();
        }
    }

    // ---------- leitura do feed ----------

    internal static List<SiteItem> Parse(string body)
    {
        var t = body.TrimStart('﻿', ' ', '\r', '\n', '\t');
        return t.StartsWith('<') ? ParseXml(t) : ParseJson(t);
    }

    private static List<SiteItem> ParseJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        JsonElement array = default;
        if (root.ValueKind == JsonValueKind.Array) array = root;
        else
        {
            foreach (var key in new[] { "items", "itens", "novidades", "data", "posts", "lancamentos", "lançamentos" })
                if (TryProp(root, key, out var a) && a.ValueKind == JsonValueKind.Array) { array = a; break; }
        }
        if (array.ValueKind != JsonValueKind.Array) throw new JsonException("JSON sem lista de itens (use \"items\": [...])");

        var list = new List<SiteItem>();
        foreach (var e in array.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.Object) continue;
            var title = Str(e, "titulo", "título", "title", "nome", "name");
            var url = Str(e, "url", "link", "href");
            var id = Str(e, "id", "guid", "slug");
            if (id.Length == 0) id = url.Length > 0 ? url : title;
            if (id.Length == 0) continue;
            var summary = Str(e, "descricao", "descrição", "description", "resumo", "summary", "content_text", "mensagem", "message");
            var dateStr = Str(e, "data", "date", "date_published", "published_at", "publicado_em", "created_at", "updated_at");
            list.Add(new SiteItem(id, title.Length > 0 ? title : "Novidade", Clean(summary), url, ParseDate(dateStr)));
        }
        return list;
    }

    private static List<SiteItem> ParseXml(string xml)
    {
        var doc = XDocument.Parse(xml);
        var list = new List<SiteItem>();
        foreach (var e in doc.Descendants().Where(x => x.Name.LocalName is "item" or "entry"))
        {
            string El(params string[] names) => e.Elements().FirstOrDefault(x => names.Contains(x.Name.LocalName))?.Value.Trim() ?? "";
            var link = e.Elements().FirstOrDefault(x => x.Name.LocalName == "link");
            var url = link?.Attribute("href")?.Value ?? link?.Value.Trim() ?? "";
            var title = El("title");
            var id = El("guid", "id");
            if (id.Length == 0) id = url.Length > 0 ? url : title;
            if (id.Length == 0) continue;
            list.Add(new SiteItem(id, title.Length > 0 ? title : "Novidade", Clean(El("description", "summary", "content")), url, ParseDate(El("pubDate", "published", "updated", "date"))));
        }
        return list;
    }

    private static bool TryProp(JsonElement e, string name, out JsonElement value)
    {
        foreach (var p in e.EnumerateObject())
            if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) { value = p.Value; return true; }
        value = default;
        return false;
    }

    private static string Str(JsonElement e, params string[] names)
    {
        foreach (var n in names)
            if (TryProp(e, n, out var v))
                return v.ValueKind switch
                {
                    JsonValueKind.String => v.GetString() ?? "",
                    JsonValueKind.Number => v.GetRawText(),
                    _ => "",
                };
        return "";
    }

    private static string Clean(string s)
    {
        s = System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", " ");
        s = System.Net.WebUtility.HtmlDecode(s);
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
        return s.Length > 140 ? s[..140] + "…" : s;
    }

    private static DateTimeOffset? ParseDate(string s)
    {
        if (s.Length == 0) return null;
        if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d)) return d;
        if (DateTimeOffset.TryParse(s, Br, DateTimeStyles.AssumeLocal, out d)) return d;
        if (long.TryParse(s, out var unix)) return unix > 10_000_000_000 ? DateTimeOffset.FromUnixTimeMilliseconds(unix) : DateTimeOffset.FromUnixTimeSeconds(unix);
        return null;
    }

    private static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    // ---------- interface ----------

    public override FrameworkElement CreateView()
    {
        var root = new ContentControl();
        void Render() => root.Content = _editing || Url.Length == 0 ? SetupView(Render) : FeedView(Render);
        Changed += Render;
        Render();
        return root;
    }

    private FrameworkElement FeedView(Action render)
    {
        var list = new StackPanel();
        foreach (var item in _items.Take(3))
        {
            var row = new Border { Background = System.Windows.Media.Brushes.Transparent, CornerRadius = new CornerRadius(8), Padding = new Thickness(6, 3, 6, 3) };
            var title = Ui.Text(item.Title, 12, FontWeights.SemiBold);
            var when = Ui.Text(item.Date is { } d ? d.ToLocalTime().ToString("dd/MM HH:mm", Br) : "", 10, null, NotchTheme.TextDim);
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(Ui.Row(6, Ui.Icon(Glyphs.Megaphone, 10, NotchTheme.Purple), title));
            Grid.SetColumn(when, 1);
            g.Children.Add(when);
            row.Child = g;
            if (item.Url.Length > 0) Ui.Clickable(row, () => Open(item.Url), NotchTheme.Surface);
            list.Children.Add(row);
        }
        if (_items.Count == 0)
            list.Children.Add(Ui.Text(_busy ? "Verificando…" : "Nenhum item encontrado ainda.", 12, null, NotchTheme.TextDim));

        var status = Ui.Text(_busy ? "Verificando…" : _status, 10.5, null, _status.StartsWith("Erro") ? NotchTheme.Red : NotchTheme.TextDim);
        var bottom = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        bottom.Children.Add(status);
        status.MaxWidth = 210;
        status.HorizontalAlignment = HorizontalAlignment.Left;
        status.Margin = new Thickness(6, 0, 0, 0);
        var buttons = Ui.Row(6,
            Ui.Button(Glyphs.Refresh, "Verificar", () => _ = CheckAsync(true)),
            Ui.Button(Glyphs.Settings, null, () => { _editing = true; render(); }));
        buttons.HorizontalAlignment = HorizontalAlignment.Right;
        bottom.Children.Add(buttons);

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(list);
        Grid.SetRow(bottom, 1);
        grid.Children.Add(bottom);
        if (!Enabled)
        {
            status.Text = "Verificação automática desligada";
        }
        return grid;
    }

    private FrameworkElement SetupView(Action render)
    {
        var urlBox = Ui.Input("https://seusite.com.br/novidades.json", out var urlInput, Url);
        var tokenBox = Ui.Input("Token (opcional)", out var tokenInput, _s.Get("token"));
        tokenBox.Width = 140;
        var interval = IntervalMin;
        var chips = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        void Chips()
        {
            chips.Children.Clear();
            foreach (var m in new[] { 5, 15, 30, 60 })
            {
                var c = Ui.Chip(m < 60 ? $"{m} min" : "1 h", interval == m, () => { interval = m; Chips(); });
                c.Margin = new Thickness(0, 0, 4, 0);
                chips.Children.Add(c);
            }
        }
        Chips();
        var on = _s.GetBool("on", true);
        var toggle = Ui.Toggle(on, v => on = v);
        var error = Ui.Text("", 10.5, null, NotchTheme.Red);

        void Save()
        {
            var url = urlInput.Text.Trim();
            if (url.Length > 0 && (!Uri.TryCreate(url, UriKind.Absolute, out var u) || (u.Scheme != "https" && u.Scheme != "http")))
            {
                error.Text = "Endereço inválido";
                return;
            }
            if (url != Url)
            {
                _s.Set("seen", "");
                _s.Set("initialized", false);
                _items = [];
                _status = "";
            }
            _s.Set("url", url);
            _s.Set("token", tokenInput.Text.Trim());
            _s.Set("interval", interval);
            _s.Set("on", on);
            _s.Save();
            _editing = false;
            Reschedule();
            render();
            if (url.Length > 0) _ = CheckAsync(true);
        }

        toggle.ToolTip = "Verificação automática";
        var buttons = Ui.Row(8,
            Ui.Button(Glyphs.Check, "Salvar e testar", Save, NotchTheme.AccentDeep),
            Ui.Button(null, "Cancelar", () => { _editing = false; render(); }),
            error);
        if (Url.Length == 0) buttons.Children.RemoveAt(1);
        var bottom = new Grid();
        bottom.Children.Add(buttons);
        var auto = Ui.Row(6, Ui.Text("Automático", 11, FontWeights.SemiBold, NotchTheme.TextDim), toggle);
        auto.HorizontalAlignment = HorizontalAlignment.Right;
        bottom.Children.Add(auto);

        var col = Ui.Column(6,
            Ui.Text(Url.Length == 0 ? "Conecte seu site para receber avisos de lançamentos" : "Ajustes do site", 12, FontWeights.SemiBold),
            urlBox,
            Ui.Row(8, tokenBox, chips),
            bottom);
        col.Loaded += (_, _) => { Host.RequestKeyboardFocus(); urlInput.Focus(); };
        return col;
    }
}
