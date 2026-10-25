using System.Windows.Media;

namespace Notch.Sdk;

/// <summary>Botão exibido à direita de um alerta.</summary>
public sealed record NotchAlertAction(string Label, Action OnClick);

/// <summary>Alerta temporário exibido no notch (bateria, água, novidades do site etc.).</summary>
public sealed record NotchAlert(string Title, string Message)
{
    /// <summary>Glifo da fonte Segoe Fluent Icons.</summary>
    public string Glyph { get; init; } = Glyphs.Info;

    /// <summary>Cor do ícone e da barra de progresso.</summary>
    public Brush? Accent { get; init; }

    /// <summary>Imagem no lugar do ícone (ex.: capa do álbum, logo do app).</summary>
    public ImageSource? Image { get; init; }

    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(4);

    /// <summary>0..1 para mostrar uma barra (volume, brilho). Nulo = sem barra.</summary>
    public double? Progress { get; init; }

    /// <summary>Alertas com a mesma chave substituem o atual em vez de entrar na fila.</summary>
    public string? Key { get; init; }

    public Action? OnClick { get; init; }

    public IReadOnlyList<NotchAlertAction> Actions { get; init; } = [];
}
