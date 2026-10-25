using System.Net.Http;
using System.Windows;

namespace Notch.Sdk;

/// <summary>
/// Uma ferramenta (página) do notch. Os módulos internos e os plugins implementam esta interface.
/// Para criar um plugin: gere uma DLL que referencie Notch.Sdk, implemente esta interface numa
/// classe pública com construtor sem parâmetros e copie a DLL para
/// %APPDATA%\ProjetoNotch\plugins\&lt;NomeDoPlugin&gt;\.
/// </summary>
public interface INotchModule
{
    /// <summary>Identificador único e estável (ex.: "agua", "meu-plugin").</summary>
    string Id { get; }

    /// <summary>Primeira parte do título, em branco (ex.: "Lembrete").</summary>
    string Title { get; }

    /// <summary>Segunda parte do título, na cor de destaque (ex.: "de Água"). Pode ser vazio.</summary>
    string TitleAccent { get; }

    /// <summary>Posição padrão da página no carrossel (menor aparece primeiro).</summary>
    int Order { get; }

    /// <summary>Chamado uma vez, na thread da interface, antes de <see cref="CreateView"/>.</summary>
    void Start(INotchHost host);

    /// <summary>Cria o conteúdo da página (área de ~380 x 100 px).</summary>
    FrameworkElement CreateView();

    /// <summary>Chamado ao sair do aplicativo.</summary>
    void Stop();
}

/// <summary>Serviços que o notch oferece aos módulos.</summary>
public interface INotchHost
{
    /// <summary>Mostra um alerta no notch (fica na fila se outro estiver visível).</summary>
    void ShowAlert(NotchAlert alert);

    /// <summary>Abre um arquivo chave=valor em %APPDATA%\ProjetoNotch\&lt;name&gt;.txt.</summary>
    KeyValueFile OpenStore(string name);

    /// <summary>%APPDATA%\ProjetoNotch</summary>
    string DataDirectory { get; }

    /// <summary>HttpClient compartilhado (com User-Agent do app).</summary>
    HttpClient Http { get; }

    /// <summary>Abre o notch na página do módulo indicado.</summary>
    void ShowPage(string moduleId);

    /// <summary>Ativa a janela para que caixas de texto recebam o teclado.</summary>
    void RequestKeyboardFocus();
}
