using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Notch.Sdk;

namespace Exemplo.Cotacao;

/// <summary>
/// Exemplo de plugin: cotação do dólar, euro e bitcoin (AwesomeAPI, gratuita e sem chave).
/// Serve de modelo para criar suas próprias ferramentas.
/// </summary>
public sealed class CotacaoModule : INotchModule
{
    private static readonly CultureInfo Br = new("pt-BR");
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(5) };
    private INotchHost _host = null!;
    private readonly Dictionary<string, (decimal Valor, decimal Variacao)> _cotacoes = new();
    private string _status = "Carregando…";

    public string Id => "cotacao";
    public string Title => "Cotação";
    public string TitleAccent => "do Dia";
    public int Order => 80;

    private event Action? Atualizou;

    public void Start(INotchHost host)
    {
        _host = host;
        _timer.Tick += async (_, _) => await AtualizarAsync();
        _timer.Start();
        _ = AtualizarAsync();
    }

    public void Stop() => _timer.Stop();

    private async Task AtualizarAsync()
    {
        try
        {
            var json = await _host.Http.GetStringAsync("https://economia.awesomeapi.com.br/json/last/USD-BRL,EUR-BRL,BTC-BRL");
            using var doc = JsonDocument.Parse(json);
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                var valor = decimal.Parse(p.Value.GetProperty("bid").GetString()!, CultureInfo.InvariantCulture);
                var variacao = decimal.Parse(p.Value.GetProperty("pctChange").GetString()!, CultureInfo.InvariantCulture);
                _cotacoes[p.Name] = (valor, variacao);
            }
            _status = $"Atualizado às {DateTime.Now:HH:mm}";
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException or FormatException or KeyNotFoundException)
        {
            _status = "Sem conexão com a AwesomeAPI";
        }
        Atualizou?.Invoke();
    }

    public FrameworkElement CreateView()
    {
        var linhas = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var status = Ui.Text("", 10, null, NotchTheme.TextDim);

        void Render()
        {
            linhas.Children.Clear();
            foreach (var (codigo, nome) in new[] { ("USDBRL", "Dólar"), ("EURBRL", "Euro"), ("BTCBRL", "Bitcoin") })
            {
                var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
                g.Children.Add(Ui.Text(nome, 13, FontWeights.SemiBold));
                if (_cotacoes.TryGetValue(codigo, out var c))
                {
                    var valor = Ui.Text(c.Valor.ToString(codigo == "BTCBRL" ? "C0" : "C3", Br), 13, FontWeights.Bold);
                    Grid.SetColumn(valor, 1);
                    g.Children.Add(valor);
                    var variacao = Ui.Text($"{(c.Variacao >= 0 ? "▲" : "▼")} {Math.Abs(c.Variacao).ToString("0.00", Br)}%", 11, FontWeights.SemiBold,
                        c.Variacao >= 0 ? NotchTheme.Green : NotchTheme.Red);
                    variacao.HorizontalAlignment = HorizontalAlignment.Right;
                    Grid.SetColumn(variacao, 2);
                    g.Children.Add(variacao);
                }
                linhas.Children.Add(g);
            }
            status.Text = _status;
        }

        var botao = Ui.Button(Glyphs.Refresh, "Atualizar", () => _ = AtualizarAsync());
        var rodape = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        rodape.Children.Add(status);
        botao.HorizontalAlignment = HorizontalAlignment.Right;
        rodape.Children.Add(botao);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(linhas);
        Grid.SetRow(rodape, 1);
        root.Children.Add(rodape);

        Atualizou += Render;
        Render();
        return root;
    }
}
