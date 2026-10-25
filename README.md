# Notch Windows

Um "notch" vivo para o Windows, no estilo Dynamic Island, todo em português. Ele fica no topo
da tela como uma pílula preta. Passe o mouse para abrir o painel com as ferramentas.

Ferramentas incluídas:

| Página | O que faz |
|---|---|
| **Tocando Agora** | Música de qualquer player (Spotify, YouTube, Deezer...): capa, play/pause, avançar, voltar e barra de progresso (clique para pular) |
| **Letra da Música** | Letra sincronizada com a música, via [LRCLIB](https://lrclib.net) |
| **Timer Pomodoro** | Foco de 25 min e pausas de 5/15 min (tempos ajustáveis); a contagem aparece na pílula |
| **Lembrete de Água** | Meta diária, garrafa, dose, horário, lembretes e histórico por hora |
| **Lista de Tarefas** | Tarefas com horário opcional e aviso na hora marcada |
| **Prateleira de Arquivos** | Arraste arquivos, links ou texto para o notch e arraste de volta quando quiser |
| **Novidades do Site** | Avisa quando algo novo é publicado no seu site (veja abaixo) |

Alertas automáticos (cada um pode ser desligado no menu): bateria e carregador, volume, brilho,
Caps Lock, idioma do teclado, internet, Bluetooth e, se você ligar, as notificações do Windows.

## Como usar

- **Passar o mouse** sobre a pílula abre o painel. **Clique direito** abre o menu.
- Para trocar de página, use as **setas**, os **pontinhos** ou a **roda do mouse**.
- Ao **arrastar arquivos** para o notch, ele abre direto na Prateleira.
- O menu (clique direito no notch ou no ícone perto do relógio do Windows) tem: ferramentas
  ligadas/desligadas, alertas, modo minimalista, mascote, iniciar com o Windows e as pastas de plugins e de dados.
- Em tela cheia (filmes, jogos, apresentações) o notch se esconde sozinho.

Os dados ficam em `%APPDATA%\ProjetoNotch` (arquivos `.txt` simples) e a prateleira fica em
`%LOCALAPPDATA%\ProjetoNotch\Shelf`.

## Compilar

Precisa do [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
.\publicar.ps1          # compila e gera dist\ProjetoNotch.exe
```

Ou para testar durante o desenvolvimento:

```powershell
dotnet run --project src\Notch.App
```

## Conectar o seu site

Na página **Novidades do Site**, informe um endereço público. O notch consulta esse endereço
a cada 5, 15, 30 ou 60 minutos e mostra um alerta para cada item novo. Na primeira consulta ele
só memoriza o que já existe, para não disparar avisos antigos.

Formatos aceitos:

**1. JSON simples (recomendado).** Crie, por exemplo, `https://seusite.com.br/novidades.json`:

```json
{
  "items": [
    {
      "id": "lancamento-42",
      "titulo": "Nova taxa para consignado INSS",
      "descricao": "A partir de hoje, taxa de 1,66% a.m.",
      "url": "https://seusite.com.br/noticias/42",
      "data": "2026-10-25T10:00:00-03:00"
    }
  ]
}
```

- `id` precisa ser único e não mudar (se faltar, é usado o `url` ou o `titulo`).
- Também são aceitos os nomes em inglês: `title`, `description`, `link`, `date`.
- A lista pode estar em `items`, `itens`, `novidades`, `data` ou `posts`, ou ser o próprio JSON (`[ ... ]`).

**2. RSS ou Atom.** Se o seu site já é WordPress, Blogger, Wix etc., use o feed dele
(ex.: `https://seusite.com.br/feed`).

**3. JSON Feed** ([jsonfeed.org](https://www.jsonfeed.org)).

Se o endereço exigir login, preencha o campo **Token**: ele é enviado como
`Authorization: Bearer <token>`.

> Para avisos instantâneos (sem esperar o intervalo), uma evolução futura é trocar a consulta
> periódica por WebSocket ou Server-Sent Events. A estrutura do módulo `SiteModule` já separa a
> busca (`CheckAsync`) da leitura (`Parse`), então dá para fazer essa troca sem mexer na interface.

## Criar uma ferramenta nova (plugin)

Cada ferramenta é uma classe que implementa `INotchModule` (em `src\Notch.Sdk`). Veja o exemplo
completo em `plugins\Exemplo.Cotacao` (cotação do dólar, euro e bitcoin).

```csharp
public sealed class MinhaFerramenta : INotchModule
{
    public string Id => "minha-ferramenta";
    public string Title => "Minha";          // branco
    public string TitleAccent => "Ferramenta"; // azul
    public int Order => 90;                   // posição no carrossel

    private INotchHost _host = null!;

    public void Start(INotchHost host) => _host = host;
    public void Stop() { }

    public FrameworkElement CreateView() =>
        Ui.Column(8,
            Ui.Text("Olá!", 16, FontWeights.Bold),
            Ui.Button(Glyphs.Bell, "Testar alerta", () =>
                _host.ShowAlert(new NotchAlert("Funcionou", "Meu primeiro plugin"))));
}
```

O que o `INotchHost` oferece:

- `ShowAlert(...)`: alerta no notch, com ícone, barra de progresso e botões de ação.
- `OpenStore("nome")`: arquivo chave=valor para salvar configurações.
- `Http`: `HttpClient` pronto para chamar APIs.
- `ShowPage(id)`: abre o notch numa página.
- `RequestKeyboardFocus()`: necessário antes de digitar numa caixa de texto.

`Ui` e `NotchTheme` dão botões, chips, interruptores, campos e cores no mesmo visual do notch.

**Instalar um plugin:**

```powershell
dotnet build plugins\Exemplo.Cotacao -c Release
# copie a pasta bin\...\net10.0-windows10.0.19041.0 para:
#   %APPDATA%\ProjetoNotch\plugins\Cotacao\
# e reinicie o notch (menu > Sair, depois abra de novo)
```

`.\publicar.ps1 -ComExemplo` já faz isso com o plugin de exemplo.

Se um plugin der erro, o notch continua funcionando e registra o problema em `%APPDATA%\ProjetoNotch\log.txt`.

## Estrutura

```
src/Notch.Sdk/        contrato para ferramentas e plugins (INotchModule, Ui, tema)
src/Notch.App/        o aplicativo
  NotchWindow.cs      a pílula, animações, alertas e páginas
  Modules/            as ferramentas internas
  Services/           música, letras, áudio, bateria/rede/Bluetooth, notificações
plugins/              exemplos de plugins
```

Créditos: letras por [LRCLIB](https://lrclib.net). Inspirado no app Nusa.
