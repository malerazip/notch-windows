"""
Gera docs/documentacao.html: a documentação completa do Projeto Notch,
com explicações, capturas de tela e todo o código-fonte embutido.

Uso (na pasta do projeto):
    python docs\\gerar_documentacao.py

Rode de novo sempre que o código mudar: o código-fonte da página é lido dos arquivos reais.
"""
import base64
import datetime
import html
import pathlib

RAIZ = pathlib.Path(__file__).resolve().parent.parent
DOCS = RAIZ / "docs"
SAIDA = DOCS / "documentacao.html"

# Ordem e descrição dos arquivos na seção "Código-fonte"
ARQUIVOS = [
    ("Configuração do projeto", [
        ("Directory.Build.props", "Configurações comuns a todos os projetos: .NET 10, WPF, versão e pasta de compilação fora do OneDrive."),
        ("ProjetoNotch.slnx", "Solução que agrupa o app, o SDK e o plugin de exemplo."),
        ("publicar.ps1", "Script que compila e gera dist\\ProjetoNotch.exe (e opcionalmente instala o plugin de exemplo)."),
        ("src/Notch.App/Notch.App.csproj", "Projeto do aplicativo: dependências (NAudio, System.Management), ícone e manifesto."),
        ("src/Notch.App/app.manifest", "Manifesto do Windows: execução sem administrador e DPI por monitor (nitidez em telas 4K)."),
    ]),
    ("SDK de ferramentas (Notch.Sdk)", [
        ("src/Notch.Sdk/Notch.Sdk.csproj", "Biblioteca compartilhada entre o app e os plugins."),
        ("src/Notch.Sdk/INotchModule.cs", "Contratos: INotchModule (uma ferramenta) e INotchHost (serviços que o notch oferece)."),
        ("src/Notch.Sdk/NotchAlert.cs", "Modelo dos alertas: título, mensagem, ícone, barra de progresso, botões de ação."),
        ("src/Notch.Sdk/KeyValueFile.cs", "Armazenamento simples em arquivos .txt no formato chave=valor."),
        ("src/Notch.Sdk/NotchTheme.cs", "Cores, fontes e glifos (ícones) do visual do notch."),
        ("src/Notch.Sdk/Ui.cs", "Componentes prontos: textos, botões em pílula, chips, interruptor, barra de progresso, campo de texto."),
    ]),
    ("Aplicativo: entrada e janela", [
        ("src/Notch.App/App.xaml", "Definição do aplicativo WPF e estilo das dicas (tooltips)."),
        ("src/Notch.App/App.xaml.cs", "Início do app: instância única, idioma pt-BR, tratamento global de erros."),
        ("src/Notch.App/NotchWindow.cs", "O coração do app: a pílula, os três estados, animações, alertas, páginas e arrastar-e-soltar."),
        ("src/Notch.App/NotchWindow.Tray.cs", "Ícone da bandeja e menu escuro de configurações."),
    ]),
    ("Aplicativo: núcleo", [
        ("src/Notch.App/Core/Paths.cs", "Pastas de dados, plugins e prateleira; arquivo de log."),
        ("src/Notch.App/Core/Autostart.cs", "Liga/desliga o \"Iniciar com o Windows\" (registro do usuário)."),
        ("src/Notch.App/Core/Native.cs", "Chamadas diretas à API do Windows (janela em primeiro plano, Caps Lock, idioma, sempre no topo)."),
        ("src/Notch.App/Core/PluginLoader.cs", "Carregador de plugins: lê as DLLs da pasta de plugins em contextos isolados."),
    ]),
    ("Aplicativo: controles visuais", [
        ("src/Notch.App/Controls/RingProgress.cs", "Anel de progresso (água, pomodoro)."),
        ("src/Notch.App/Controls/AudioBars.cs", "Barrinhas de áudio que reagem ao volume real do som."),
        ("src/Notch.App/Controls/BatteryIcon.cs", "Anel de bateria com raio ao carregar."),
        ("src/Notch.App/Controls/Mascot.cs", "Mascote: olhinhos que piscam e olham para os lados."),
    ]),
    ("Aplicativo: serviços do sistema", [
        ("src/Notch.App/Services/MediaService.cs", "Música tocando em qualquer player, via controles de mídia do Windows."),
        ("src/Notch.App/Services/LyricsService.cs", "Busca de letras sincronizadas no LRCLIB."),
        ("src/Notch.App/Services/AudioService.cs", "Volume do Windows e nível do som (Core Audio, via NAudio)."),
        ("src/Notch.App/Services/SystemStatusService.cs", "Bateria, carregador, rede, Bluetooth, Caps Lock, idioma do teclado e brilho."),
        ("src/Notch.App/Services/WindowsNotificationService.cs", "Espelho das notificações do Windows (opcional)."),
    ]),
    ("Aplicativo: ferramentas internas", [
        ("src/Notch.App/Modules/ModuleBase.cs", "Base das ferramentas internas (timer que só roda com a página visível)."),
        ("src/Notch.App/Modules/MusicModule.cs", "Página \"Tocando Agora\"."),
        ("src/Notch.App/Modules/LyricsModule.cs", "Página \"Letra da Música\"."),
        ("src/Notch.App/Modules/PomodoroModule.cs", "Página \"Timer Pomodoro\"."),
        ("src/Notch.App/Modules/WaterModule.cs", "Página \"Lembrete de Água\"."),
        ("src/Notch.App/Modules/TodoModule.cs", "Página \"Lista de Tarefas\"."),
        ("src/Notch.App/Modules/ShelfModule.cs", "Página \"Prateleira de Arquivos\"."),
        ("src/Notch.App/Modules/SiteModule.cs", "Página \"Novidades do Site\" (conexão com o seu site)."),
    ]),
    ("Plugin de exemplo", [
        ("plugins/Exemplo.Cotacao/Exemplo.Cotacao.csproj", "Projeto do plugin: referencia o Notch.Sdk sem copiá-lo."),
        ("plugins/Exemplo.Cotacao/CotacaoModule.cs", "Página \"Cotação do Dia\": dólar, euro e bitcoin pela AwesomeAPI."),
    ]),
]

LINGUAGEM = {".cs": "csharp", ".csproj": "xml", ".props": "xml", ".slnx": "xml", ".xaml": "xml", ".manifest": "xml", ".ps1": "powershell", ".py": "python"}


def imagem(nome: str) -> str:
    dados = (DOCS / "img" / nome).read_bytes()
    return "data:image/png;base64," + base64.b64encode(dados).decode("ascii")


def figura(nome: str, legenda: str, classe: str = "") -> str:
    return (f'<figure class="shot {classe}"><img src="{imagem(nome)}" alt="{html.escape(legenda)}" loading="lazy">'
            f"<figcaption>{legenda}</figcaption></figure>")


def secao_codigo():
    blocos, indice, total_linhas, total_arquivos = [], [], 0, 0
    for grupo, arquivos in ARQUIVOS:
        blocos.append(f'<h3 class="grupo">{html.escape(grupo)}</h3>')
        for caminho, descricao in arquivos:
            arq = RAIZ / caminho
            if not arq.exists():
                continue
            texto = arq.read_text(encoding="utf-8-sig")
            linhas = texto.count("\n") + 1
            total_linhas += linhas
            total_arquivos += 1
            ancora = "arq-" + caminho.replace("/", "-").replace(".", "-").lower()
            lang = LINGUAGEM.get(arq.suffix.lower(), "plaintext")
            indice.append(f'<tr><td><a href="#{ancora}"><code>{html.escape(caminho)}</code></a></td>'
                          f'<td class="num">{linhas}</td><td>{html.escape(descricao)}</td></tr>')
            blocos.append(
                f'<details class="arquivo" id="{ancora}"><summary><span class="caminho">{html.escape(caminho)}</span>'
                f'<span class="meta">{linhas} linhas</span><span class="desc">{html.escape(descricao)}</span></summary>'
                f'<div class="codigo"><button class="copiar" type="button">Copiar</button>'
                f'<pre><code class="language-{lang}">{html.escape(texto)}</code></pre></div></details>')
    tabela = ('<div class="tabela"><table><thead><tr><th>Arquivo</th><th class="num">Linhas</th><th>O que faz</th></tr></thead><tbody>'
              + "".join(indice) + "</tbody></table></div>")
    return tabela, "\n".join(blocos), total_linhas, total_arquivos


def main():
    tabela, codigo, linhas, arquivos = secao_codigo()
    pagina = MODELO
    trocas = {
        "{{ICONE}}": imagem("icone.png"),
        "{{DATA}}": datetime.date.today().strftime("%d/%m/%Y"),
        "{{LINHAS}}": f"{linhas:,}".replace(",", "."),
        "{{ARQUIVOS}}": str(arquivos),
        "{{TABELA_ARQUIVOS}}": tabela,
        "{{CODIGO}}": codigo,
        "{{FIG_NUSA}}": figura("nusa-original.png", "O Nusa original (inglês, código fechado) que serviu de referência visual."),
        "{{FIG_PILULA_MUSICA}}": figura("pilula-musica.png", "Pílula com música tocando: capa, hora e barrinhas de áudio.", "pequena"),
        "{{FIG_PILULA_POMODORO}}": figura("pilula-pomodoro.png", "Pílula com o Pomodoro rodando: contagem em laranja.", "pequena"),
        "{{FIG_BOAS_VINDAS}}": figura("alerta-boas-vindas.png", "Alerta de boas-vindas na primeira execução."),
        "{{FIG_ALERTA_AGUA}}": figura("alerta-agua.png", "Alerta com botões de ação (lembrete de água)."),
        "{{FIG_ALERTA_SITE}}": figura("alerta-site.png", "Alerta de novidade publicada no site."),
        "{{FIG_MENU}}": figura("menu.png", "Menu de configurações (clique direito no notch ou no ícone da bandeja).", "menu"),
    }
    for nome, legenda in [
        ("pagina-musica.png", "Tocando Agora"), ("pagina-letra.png", "Letra da Música"),
        ("pagina-pomodoro.png", "Timer Pomodoro"), ("pagina-agua.png", "Lembrete de Água"),
        ("pagina-tarefas.png", "Lista de Tarefas"), ("pagina-prateleira.png", "Prateleira de Arquivos"),
        ("pagina-site.png", "Novidades do Site"), ("pagina-cotacao-plugin.png", "Cotação do Dia (plugin de exemplo)"),
    ]:
        trocas["{{FIG_" + nome.split(".")[0].upper().replace("-", "_") + "}}"] = figura(nome, legenda)
    for chave, valor in trocas.items():
        pagina = pagina.replace(chave, valor)
    SAIDA.write_text(pagina, encoding="utf-8")
    print(f"Gerado: {SAIDA}  ({arquivos} arquivos, {linhas} linhas de código, {SAIDA.stat().st_size // 1024} KB)")


MODELO = r"""<!doctype html>
<html lang="pt-BR">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Projeto Notch · Documentação</title>
<link rel="icon" href="{{ICONE}}">
<style>
:root {
  --bg: #f6f7f9; --panel: #ffffff; --surface: #eef0f3; --line: #dde1e6;
  --text: #16181d; --dim: #5b6270; --faint: #8a909c;
  --accent: #0a74d6; --accent-soft: #e3f0fc; --green: #1f9d4c; --orange: #d97b00; --red: #d63a30; --purple: #8a3fd1;
  --code-bg: #0f1115; --code-text: #e6e8ee;
  --radius: 14px;
  --font: "Segoe UI Variable Text", "Segoe UI", system-ui, -apple-system, Roboto, sans-serif;
  --mono: "Cascadia Code", "Cascadia Mono", Consolas, ui-monospace, monospace;
}
@media (prefers-color-scheme: dark) {
  :root:not([data-theme="light"]) {
    --bg: #0b0c0f; --panel: #131519; --surface: #1c1e24; --line: #2a2d35;
    --text: #eceef2; --dim: #a1a7b3; --faint: #6c7280;
    --accent: #3ea6ff; --accent-soft: #10263b; --green: #30d158; --orange: #ff9f0a; --red: #ff453a; --purple: #bf5af2;
  }
}
:root[data-theme="dark"] {
  --bg: #0b0c0f; --panel: #131519; --surface: #1c1e24; --line: #2a2d35;
  --text: #eceef2; --dim: #a1a7b3; --faint: #6c7280;
  --accent: #3ea6ff; --accent-soft: #10263b; --green: #30d158; --orange: #ff9f0a; --red: #ff453a; --purple: #bf5af2;
}
* { box-sizing: border-box; }
html { scroll-behavior: smooth; scroll-padding-top: 16px; }
body { margin: 0; background: var(--bg); color: var(--text); font: 15.5px/1.65 var(--font); -webkit-font-smoothing: antialiased; }
a { color: var(--accent); text-decoration: none; }
a:hover { text-decoration: underline; }
code { font-family: var(--mono); font-size: .88em; background: var(--surface); padding: .1em .38em; border-radius: 6px; }
pre code { background: none; padding: 0; font-size: 12.8px; }
h1, h2, h3, h4 { line-height: 1.25; letter-spacing: -.01em; }
h2 { font-size: 1.65rem; margin: 0 0 .4rem; }
h3 { font-size: 1.18rem; margin: 2rem 0 .5rem; }
h4 { font-size: 1rem; margin: 1.4rem 0 .3rem; }
p { margin: .55rem 0; }
ul, ol { padding-left: 1.3rem; }
li { margin: .2rem 0; }

.layout { display: grid; grid-template-columns: 260px minmax(0, 1fr); min-height: 100vh; }
nav.lateral { position: sticky; top: 0; height: 100vh; overflow-y: auto; padding: 22px 16px; border-right: 1px solid var(--line); background: var(--panel); }
nav.lateral .marca { display: flex; align-items: center; gap: 10px; font-weight: 700; margin-bottom: 18px; color: var(--text); }
nav.lateral .marca img { width: 30px; height: 30px; }
nav.lateral a.item { display: block; padding: 5px 10px; border-radius: 8px; color: var(--dim); font-size: 14px; }
nav.lateral a.item:hover { background: var(--surface); color: var(--text); text-decoration: none; }
nav.lateral a.item.ativo { background: var(--accent-soft); color: var(--accent); font-weight: 600; }
nav.lateral .rotulo { font-size: 11px; text-transform: uppercase; letter-spacing: .08em; color: var(--faint); margin: 16px 10px 4px; }
.tema { margin-top: 18px; width: 100%; padding: 7px; border-radius: 8px; border: 1px solid var(--line); background: var(--surface); color: var(--text); font: inherit; font-size: 13px; cursor: pointer; }

main { padding: 0 48px 80px; max-width: 1040px; width: 100%; }
section { padding: 46px 0 8px; border-bottom: 1px solid var(--line); }
section:last-of-type { border-bottom: 0; }
.sub { color: var(--dim); font-size: 1.02rem; margin-top: 0; }

.hero { padding: 54px 0 34px; display: grid; grid-template-columns: auto 1fr; gap: 26px; align-items: center; }
.hero img { width: 96px; height: 96px; filter: drop-shadow(0 8px 20px rgba(10, 80, 160, .35)); }
.hero h1 { font-size: 2.5rem; margin: 0; }
.hero h1 span { color: var(--accent); }
.hero p { margin: .3rem 0 0; color: var(--dim); font-size: 1.08rem; }
.numeros { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 12px; margin: 6px 0 10px; }
.numero { background: var(--panel); border: 1px solid var(--line); border-radius: var(--radius); padding: 14px 16px; }
.numero b { display: block; font-size: 1.5rem; }
.numero span { color: var(--dim); font-size: 13px; }

.cartoes { display: grid; grid-template-columns: repeat(auto-fill, minmax(230px, 1fr)); gap: 12px; margin: 14px 0; }
.cartao { background: var(--panel); border: 1px solid var(--line); border-radius: var(--radius); padding: 16px 18px; }
.cartao h4 { margin: 0 0 4px; display: flex; gap: 8px; align-items: center; }
.cartao p { margin: 0; color: var(--dim); font-size: 14px; }
.ponto { width: 9px; height: 9px; border-radius: 50%; flex: none; }

.galeria { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 14px; margin: 16px 0; }
figure.shot { margin: 0; background: #000; border-radius: var(--radius); overflow: hidden; border: 1px solid var(--line); }
figure.shot img { display: block; width: 100%; height: auto; }
figure.shot figcaption { background: var(--panel); color: var(--dim); font-size: 13px; padding: 8px 12px; border-top: 1px solid var(--line); }
figure.shot.pequena img { width: auto; max-width: 100%; margin: 0 auto; }
figure.shot.menu { max-width: 320px; }
.lado { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 14px; margin: 14px 0; align-items: start; }

.tabela { overflow-x: auto; margin: 12px 0; border: 1px solid var(--line); border-radius: 12px; background: var(--panel); }
table { border-collapse: collapse; width: 100%; font-size: 14px; }
th, td { text-align: left; padding: 9px 12px; border-bottom: 1px solid var(--line); vertical-align: top; }
th { background: var(--surface); font-size: 12.5px; text-transform: uppercase; letter-spacing: .04em; color: var(--dim); }
tr:last-child td { border-bottom: 0; }
td.num, th.num { text-align: right; white-space: nowrap; font-variant-numeric: tabular-nums; }

.nota { border-left: 3px solid var(--accent); background: var(--accent-soft); padding: 10px 14px; border-radius: 0 10px 10px 0; margin: 14px 0; font-size: 14.5px; }
.nota.aviso { border-color: var(--orange); background: color-mix(in srgb, var(--orange) 12%, transparent); }
.passos { counter-reset: passo; list-style: none; padding: 0; }
.passos > li { counter-increment: passo; position: relative; padding: 2px 0 10px 44px; }
.passos > li::before { content: counter(passo); position: absolute; left: 0; top: 0; width: 30px; height: 30px; border-radius: 50%; background: var(--accent); color: #fff; font-weight: 700; display: grid; place-items: center; font-size: 14px; }

.diagrama { background: var(--panel); border: 1px solid var(--line); border-radius: var(--radius); padding: 14px; margin: 16px 0; overflow-x: auto; }
.diagrama svg { display: block; width: 100%; min-width: 640px; height: auto; }
.diagrama .caixa { fill: var(--surface); stroke: var(--line); stroke-width: 1.2; }
.diagrama .caixa.forte { fill: var(--accent-soft); stroke: var(--accent); }
.diagrama .caixa.preta { fill: #000; stroke: #000; }
.diagrama text { fill: var(--text); font: 13px var(--font); }
.diagrama text.pequeno { fill: var(--dim); font-size: 11.5px; }
.diagrama text.branco { fill: #fff; }
.diagrama .seta { stroke: var(--faint); stroke-width: 1.4; fill: none; marker-end: url(#ponta); }
.diagrama .seta.azul { stroke: var(--accent); marker-end: url(#pontaAzul); }
.diagrama .rot { fill: var(--dim); font-size: 11.5px; }

pre.bloco, .codigo pre { background: var(--code-bg); color: var(--code-text); border-radius: 12px; padding: 14px 16px; overflow-x: auto; margin: 10px 0; font-size: 12.8px; line-height: 1.55; }
pre.bloco code { color: inherit; }

details.arquivo { border: 1px solid var(--line); border-radius: 12px; background: var(--panel); margin: 8px 0; }
details.arquivo > summary { cursor: pointer; padding: 10px 14px; display: grid; grid-template-columns: 1fr auto; gap: 2px 12px; list-style: none; }
details.arquivo > summary::-webkit-details-marker { display: none; }
details.arquivo .caminho::before { content: "▸ "; color: var(--faint); }
details.arquivo[open] .caminho::before { content: "▾ "; }
details.arquivo[open] > summary { border-bottom: 1px solid var(--line); }
details.arquivo .caminho { font-family: var(--mono); font-size: 13.5px; font-weight: 600; }
details.arquivo .meta { color: var(--faint); font-size: 12.5px; }
details.arquivo .desc { grid-column: 1 / -1; color: var(--dim); font-size: 13.5px; }
.codigo { position: relative; padding: 0 10px 4px; }
.copiar { position: absolute; top: 18px; right: 22px; border: 1px solid #333842; background: #1c1f26; color: #cfd3dc; border-radius: 7px; font: 12px var(--font); padding: 4px 10px; cursor: pointer; }
.copiar:hover { background: #262a33; }
h3.grupo { font-size: 1rem; color: var(--dim); text-transform: uppercase; letter-spacing: .05em; margin-top: 1.8rem; }
.botoes-codigo { display: flex; gap: 8px; flex-wrap: wrap; margin: 10px 0; }
.botoes-codigo button { border: 1px solid var(--line); background: var(--panel); color: var(--text); border-radius: 8px; padding: 6px 12px; font: 13px var(--font); cursor: pointer; }

/* destaque de sintaxe (cores próprias, sem CSS externo) */
.hljs-keyword, .hljs-built_in, .hljs-type { color: #ff7ab2; }
.hljs-string, .hljs-attr { color: #ff8170; }
.hljs-attribute { color: #b281eb; }
.hljs-number, .hljs-literal { color: #d9c97c; }
.hljs-comment, .hljs-doctag, .hljs-meta { color: #7f8c98; font-style: italic; }
.hljs-title, .hljs-title.class_, .hljs-title.function_ { color: #6bdfff; }
.hljs-tag, .hljs-name { color: #78c2b3; }
.hljs-variable, .hljs-params { color: #e6e8ee; }

.voltar { position: fixed; right: 18px; bottom: 18px; width: 40px; height: 40px; border-radius: 50%; background: var(--accent); color: #fff; display: grid; place-items: center; font-size: 18px; box-shadow: 0 6px 18px rgba(0,0,0,.25); }
.voltar:hover { text-decoration: none; }
footer { color: var(--faint); font-size: 13px; padding: 30px 0; }

@media (max-width: 900px) {
  .layout { grid-template-columns: 1fr; }
  nav.lateral { position: static; height: auto; border-right: 0; border-bottom: 1px solid var(--line); display: flex; flex-wrap: wrap; gap: 4px; padding: 14px 16px; }
  nav.lateral .marca { width: 100%; margin-bottom: 6px; }
  nav.lateral .rotulo, .tema { display: none; }
  nav.lateral a.item { padding: 4px 9px; background: var(--surface); font-size: 13px; }
  main { padding: 0 16px 60px; }
  .hero { grid-template-columns: 1fr; padding-top: 30px; }
  .hero h1 { font-size: 1.9rem; }
  .numeros { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .galeria { grid-template-columns: 1fr; }
}
</style>
</head>
<body>
<div class="layout">
<nav class="lateral" aria-label="Seções">
  <div class="marca"><img src="{{ICONE}}" alt="">Projeto Notch</div>
  <div class="rotulo">Conhecer</div>
  <a class="item" href="#visao">Visão geral</a>
  <a class="item" href="#galeria">Galeria</a>
  <a class="item" href="#recursos">O que dá para fazer</a>
  <a class="item" href="#uso">Como usar</a>
  <div class="rotulo">Como foi criado</div>
  <a class="item" href="#criacao">A criação</a>
  <a class="item" href="#arquitetura">Arquitetura</a>
  <a class="item" href="#janela">A janela do notch</a>
  <a class="item" href="#alertas">Alertas</a>
  <a class="item" href="#ferramentas">Ferramentas por dentro</a>
  <a class="item" href="#servicos">Serviços do Windows</a>
  <a class="item" href="#dados">Dados e configurações</a>
  <div class="rotulo">Estender</div>
  <a class="item" href="#site">Conectar seu site</a>
  <a class="item" href="#plugins">Criar ferramentas</a>
  <a class="item" href="#ideias">Ideias de plugins</a>
  <div class="rotulo">Manter</div>
  <a class="item" href="#compilar">Compilar e distribuir</a>
  <a class="item" href="#privacidade">Privacidade</a>
  <a class="item" href="#problemas">Solução de problemas</a>
  <a class="item" href="#codigo">Código-fonte completo</a>
  <button class="tema" type="button" id="tema">Alternar tema claro/escuro</button>
</nav>

<main>
<header class="hero" id="topo">
  <img src="{{ICONE}}" alt="Ícone do Projeto Notch">
  <div>
    <h1>Projeto <span>Notch</span></h1>
    <p>Um notch vivo para o Windows, no estilo Dynamic Island, todo em português. Documentação completa: como funciona, como foi construído, como estender e o código-fonte inteiro.</p>
  </div>
</header>
<div class="numeros">
  <div class="numero"><b>8</b><span>ferramentas (7 internas + 1 plugin)</span></div>
  <div class="numero"><b>{{ARQUIVOS}}</b><span>arquivos de código</span></div>
  <div class="numero"><b>{{LINHAS}}</b><span>linhas de código</span></div>
  <div class="numero"><b>~95 MB</b><span>de memória em uso</span></div>
</div>
<p class="sub">Versão 1.0.0 · documentação gerada em {{DATA}} a partir do código atual.</p>

<!-- =============================================================== -->
<section id="visao">
<h2>Visão geral</h2>
<p>O <b>Projeto Notch</b> é uma pílula preta que fica no centro do topo da tela. Com o <b>notch fechado</b>, ela mostra a hora, a bateria, o microfone e o que está tocando. Ao passar o mouse, o <b>notch abre</b> e vira um painel com páginas de ferramentas: música, letra sincronizada, Pomodoro, lembrete de água, tarefas, prateleira de arquivos e novidades do seu site. Quando algo acontece (bateria fraca, hora de beber água, lançamento no site), ela se estica e mostra um <b>alerta</b> com botões.</p>
<p>Ele nasceu como substituto do <b>Nusa</b>, um app parecido em inglês e de código fechado. O autor do Nusa abandonou o projeto e não dava para acrescentar funções nele. O Projeto Notch refaz tudo o que o Nusa fazia, em português, e foi construído desde o início para <b>aceitar ferramentas novas</b> (plugins) e se <b>conectar ao seu site</b>.</p>
<div class="tabela"><table>
<thead><tr><th>Item</th><th>Escolha</th><th>Por quê</th></tr></thead>
<tbody>
<tr><td>Plataforma</td><td>.NET 10 (LTS) + WPF</td><td>Acesso nativo às APIs do Windows (mídia, notificações, bateria, Bluetooth), visual fluido com aceleração por GPU e nada pesado para instalar.</td></tr>
<tr><td>Linguagem</td><td>C# 14</td><td>Moderna, rápida e com ótimo suporte do Visual Studio e do VS Code.</td></tr>
<tr><td>Dependências</td><td>NAudio.Wasapi, System.Management</td><td>Volume e nível de áudio (Core Audio) e eventos de brilho (WMI). Só isso.</td></tr>
<tr><td>Dados</td><td>Arquivos <code>.txt</code> chave=valor</td><td>Fáceis de ler, editar e copiar. Sem banco de dados.</td></tr>
<tr><td>Extensão</td><td>Plugins (DLL) com o <code>Notch.Sdk</code></td><td>Ferramentas novas sem mexer no app principal.</td></tr>
</tbody></table></div>
</section>

<!-- =============================================================== -->
<section id="galeria">
<h2>Galeria</h2>
<p class="sub">Capturas reais, tiradas durante os testes no seu computador.</p>
<h3>Notch fechado (a pílula)</h3>
<div class="lado">{{FIG_PILULA_MUSICA}}{{FIG_PILULA_POMODORO}}</div>
<h3>Notch aberto (as ferramentas)</h3>
<div class="galeria">
{{FIG_PAGINA_MUSICA}}{{FIG_PAGINA_LETRA}}{{FIG_PAGINA_POMODORO}}{{FIG_PAGINA_AGUA}}
{{FIG_PAGINA_TAREFAS}}{{FIG_PAGINA_PRATELEIRA}}{{FIG_PAGINA_SITE}}{{FIG_PAGINA_COTACAO_PLUGIN}}
</div>
<h3>Alertas</h3>
<div class="galeria">{{FIG_BOAS_VINDAS}}{{FIG_ALERTA_AGUA}}{{FIG_ALERTA_SITE}}</div>
<h3>Menu de configurações</h3>
{{FIG_MENU}}
</section>

<!-- =============================================================== -->
<section id="recursos">
<h2>O que dá para fazer com o notch</h2>
<h3>Ferramentas (páginas)</h3>
<div class="cartoes">
<div class="cartao"><h4><span class="ponto" style="background:var(--text)"></span>Tocando Agora</h4><p>Controla a música de qualquer player (Spotify, YouTube no navegador, Deezer, Apple Music, VLC...): capa, título, artista, play/pause, avançar, voltar e barra de progresso clicável para pular.</p></div>
<div class="cartao"><h4><span class="ponto" style="background:var(--accent)"></span>Letra da Música</h4><p>Letra sincronizada: a linha atual em destaque, a anterior e a próxima. Se não houver versão sincronizada, mostra a letra simples. Fonte: LRCLIB, gratuito e sem cadastro.</p></div>
<div class="cartao"><h4><span class="ponto" style="background:var(--orange)"></span>Timer Pomodoro</h4><p>Ciclos de foco e pausa (25/5/15 min, ajustáveis), pausa longa a cada 4 focos, alerta com som no fim e contagem regressiva visível na pílula.</p></div>
<div class="cartao"><h4><span class="ponto" style="background:var(--accent)"></span>Lembrete de Água</h4><p>Meta diária, tamanho da garrafa, dose por gole e horário de lembretes. Distribui os lembretes ao longo do dia, avisa quando a garrafa deve estar vazia e mostra o histórico por hora.</p></div>
<div class="cartao"><h4><span class="ponto" style="background:var(--purple)"></span>Lista de Tarefas</h4><p>Adicionar, editar, concluir e excluir tarefas, com horário opcional. Na hora marcada aparece um alerta com "Concluir" e "+10 min".</p></div>
<div class="cartao"><h4><span class="ponto" style="background:var(--green)"></span>Prateleira de Arquivos</h4><p>Arraste arquivos, pastas, links ou textos para o notch. Eles ficam guardados até você arrastá-los para outro lugar (e-mail, WhatsApp Web, outra pasta) ou abri-los com um clique.</p></div>
<div class="cartao"><h4><span class="ponto" style="background:var(--purple)"></span>Novidades do Site</h4><p>Consulta seu site de tempos em tempos e avisa quando algo novo é publicado, com um botão para abrir. Aceita JSON, RSS e Atom.</p></div>
<div class="cartao"><h4><span class="ponto" style="background:var(--red)"></span>Botão do microfone</h4><p>Sempre visível no canto direito da pílula: <b>verde</b> = ligado, <b>vermelho cortado</b> = mudo, <b>cinza</b> = sem microfone. Um clique muta/libera o microfone do Windows (vale para Teams, Zoom, Meet, WhatsApp) sem abrir o painel. O estado é conferido a cada segundo, então mudanças feitas em outros apps ou no próprio headset aparecem na hora. Também fica no cabeçalho do painel e no menu.</p></div>
<div class="cartao"><h4><span class="ponto" style="background:var(--green)"></span>Cotação do Dia (plugin)</h4><p>Exemplo de ferramenta externa: dólar, euro e bitcoin com variação do dia. Serve de modelo para criar as suas.</p></div>
</div>
<h3>Alertas automáticos do sistema</h3>
<div class="tabela"><table>
<thead><tr><th>Alerta</th><th>Quando aparece</th></tr></thead>
<tbody>
<tr><td>Bateria</td><td>Carregador conectado/desconectado, bateria fraca (20%), crítica (10%) e totalmente carregada.</td></tr>
<tr><td>Volume</td><td>Ao mudar o volume ou ativar o mudo, com barra de nível.</td></tr>
<tr><td>Microfone</td><td>Ao mutar ou liberar o microfone, pelo notch ou por outro app (Windows, Teams...).</td></tr>
<tr><td>Brilho</td><td>Ao mudar o brilho da tela (notebooks), com barra de nível.</td></tr>
<tr><td>Caps Lock</td><td>Ativado / desativado.</td></tr>
<tr><td>Idioma do teclado</td><td>Ao trocar o layout (ex.: Português → Inglês) na mesma janela.</td></tr>
<tr><td>Internet</td><td>Conectado, sem acesso à internet ou desconectado (com o nome da rede).</td></tr>
<tr><td>Bluetooth</td><td>Fone, mouse ou caixa de som conectado ou desconectado.</td></tr>
<tr><td>Notificações do Windows</td><td>(Opcional, desligado por padrão.) WhatsApp, Teams, Outlook e outros aparecem no notch com o ícone do app.</td></tr>
</tbody></table></div>
<h3>Comportamentos inteligentes</h3>
<ul>
<li><b>Some em tela cheia:</b> filmes, jogos e apresentações em tela cheia escondem o notch automaticamente.</li>
<li><b>Não rouba o foco:</b> clicar no notch não tira o foco da janela onde você está digitando (exceto ao digitar dentro dele).</li>
<li><b>Clique atravessa:</b> fora da pílula, os cliques passam para as janelas de baixo.</li>
<li><b>Barrinhas de áudio de verdade:</b> elas reagem ao nível real do som, não a uma animação falsa.</li>
<li><b>Fila de alertas:</b> vários avisos seguidos entram em fila; avisos repetidos (volume, Caps Lock) só atualizam o atual.</li>
<li><b>Resistente a erros:</b> se uma ferramenta ou plugin falhar, o resto continua funcionando e o erro vai para o log.</li>
<li><b>Instância única:</b> abrir o app duas vezes não cria dois notches.</li>
</ul>
</section>

<!-- =============================================================== -->
<section id="uso">
<h2>Como usar</h2>
<p><b>Notch fechado</b>: a pílula parada no topo (hora, bateria, música, microfone). <b>Notch aberto</b>: o painel com as ferramentas, que aparece ao passar o mouse ou clicar.</p>
<div class="tabela"><table>
<thead><tr><th>Ação</th><th>Resultado</th></tr></thead>
<tbody>
<tr><td>Passar o mouse sobre o notch fechado</td><td>Abre o notch (após ~0,15 s, para evitar aberturas acidentais).</td></tr>
<tr><td>Tirar o mouse do notch aberto</td><td>Fecha após ~0,45 s. Se você estiver digitando, só fecha ao clicar fora.</td></tr>
<tr><td>Clique no notch fechado</td><td>Abre o notch na hora.</td></tr>
<tr><td>Clique no microfone do notch fechado</td><td>Muta/libera o microfone sem abrir o notch (verde = ligado, vermelho = mudo).</td></tr>
<tr><td>Setas laterais, pontinhos ou roda do mouse</td><td>Trocam de página. A última página aberta é lembrada.</td></tr>
<tr><td>Clique direito no notch</td><td>Abre o menu de configurações.</td></tr>
<tr><td>Clique no ícone da bandeja (perto do relógio)</td><td>Esquerdo abre o painel; direito abre o menu.</td></tr>
<tr><td>Arrastar arquivos para o notch</td><td>Abre a Prateleira e guarda os arquivos ao soltar.</td></tr>
<tr><td>Mouse sobre um alerta</td><td>O alerta não some enquanto o mouse está em cima.</td></tr>
<tr><td>Clique num alerta</td><td>Executa a ação do alerta ou abre o painel.</td></tr>
</tbody></table></div>
<h3>Menu de configurações</h3>
<ul>
<li><b>Ferramentas:</b> liga/desliga cada página (inclusive plugins).</li>
<li><b>Alertas do sistema:</b> liga/desliga bateria, volume, brilho, Caps Lock, idioma, internet e Bluetooth.</li>
<li><b>Notificações do Windows no notch:</b> pede permissão ao Windows e passa a espelhar as notificações.</li>
<li><b>Modo minimalista:</b> pílula menor, só com a hora.</li>
<li><b>Mascote:</b> olhinhos que piscam quando nada está tocando.</li>
<li><b>Iniciar com o Windows</b>, <b>Abrir pasta de plugins</b>, <b>Abrir pasta de dados</b>, <b>Sobre</b> e <b>Sair</b>.</li>
</ul>
</section>

<!-- =============================================================== -->
<section id="criacao">
<h2>A criação: do Nusa ao Projeto Notch</h2>
<ol class="passos">
<li><b>Análise do Nusa com a ferramenta REA.</b> O <code>NusaSetup.zip</code> trazia um instalador Inno Setup 6.7 (Delphi), sem assinatura digital, de um autor identificado só como "Rama". Depois de instalado, o programa era um único <code>nusa.exe</code> de 900 KB escrito em <b>Rust</b>, desenhado com Direct2D/DirectComposition.</li>
<li><b>Mapeamento das funções.</b> Lendo os textos e as APIs do Windows usadas pelo executável, apareceram as funções: controles de mídia do Windows (música), LRCLIB (letras), Pomodoro, lembrete de água, tarefas, prateleira (<code>%LOCALAPPDATA%\Nusa\Shelf</code>), bateria, rede, Bluetooth, Caps Lock, leitura das notificações do Windows e inicialização automática pelo registro. Os dados ficavam em <code>.txt</code> chave=valor.</li>
<li><b>Conclusão: não dava para estender.</b> Não havia sistema de plugins, scripts nem código-fonte, e o autor abandonou o projeto. A recomendação foi recriar o app do zero, já pensado para receber ferramentas novas.</li>
<li><b>Referência visual.</b> Com o Nusa aberto, foram tiradas capturas do painel: fundo preto, cantos arredondados, título em duas cores (branco + azul), cabeçalho com bateria, data, hora e barras de áudio, botões em pílula e pontinhos de navegação.
{{FIG_NUSA}}</li>
<li><b>Escolha da tecnologia.</b> O plano inicial era Tauri (Rust + HTML), mas o PC não tinha as ferramentas de compilação de Rust e C++ (de 6 a 8 GB). Para um app só de Windows, <b>.NET 10 + WPF</b> se mostrou melhor: acesso direto às APIs do Windows, leve e com o runtime já instalado. Bastou instalar o .NET 10 SDK (~250 MB) pelo <code>winget</code>.</li>
<li><b>Arquitetura extensível primeiro.</b> Antes das telas, foi criado o <code>Notch.Sdk</code>, o contrato que todas as ferramentas seguem. Assim as ferramentas internas e os plugins usam exatamente o mesmo mecanismo.</li>
<li><b>Construção e testes reais.</b> O app foi compilado sem erros nem avisos e testado no PC: alerta de boas-vindas, lembrete de água (clique em "Adiar"), música detectada no YouTube, todas as páginas, Pomodoro na pílula, menu, plugin carregado e conexão com um feed de verdade (com simulação de item novo). Ajustes visuais foram feitos a partir das capturas.</li>
</ol>
</section>

<!-- =============================================================== -->
<section id="arquitetura">
<h2>Arquitetura</h2>
<p>O código está dividido em três partes:</p>
<ul>
<li><b><code>src/Notch.Sdk</code></b>: biblioteca pequena com o <b>contrato</b> das ferramentas (<code>INotchModule</code>, <code>INotchHost</code>, <code>NotchAlert</code>) e os componentes visuais (<code>Ui</code>, <code>NotchTheme</code>, <code>Glyphs</code>). Não depende do app.</li>
<li><b><code>src/Notch.App</code></b>: o aplicativo. A janela (<code>NotchWindow</code>) implementa <code>INotchHost</code>, ou seja, oferece alertas, armazenamento, internet e navegação para as ferramentas. Os <b>serviços</b> conversam com o Windows; os <b>módulos</b> são as páginas.</li>
<li><b><code>plugins/</code></b>: ferramentas externas. Cada plugin é uma DLL que referencia só o <code>Notch.Sdk</code>.</li>
</ul>
<div class="diagrama">
<svg viewBox="0 0 900 470" role="img" aria-label="Diagrama de arquitetura do Projeto Notch">
<defs>
<marker id="ponta" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0,0 L10,5 L0,10 z" fill="#8a909c"/></marker>
<marker id="pontaAzul" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0,0 L10,5 L0,10 z" fill="#3ea6ff"/></marker>
</defs>
<!-- Windows -->
<rect class="caixa" x="20" y="16" width="860" height="70" rx="12"/>
<text x="36" y="40" font-weight="700">Windows</text>
<text class="pequeno" x="36" y="62">Controles de mídia (GSMTC) · Core Audio · WMI (brilho) · Bluetooth · Rede · Energia · Notificações · Registro</text>
<!-- Internet -->
<rect class="caixa" x="700" y="120" width="180" height="96" rx="12"/>
<text x="716" y="144" font-weight="700">Internet</text>
<text class="pequeno" x="716" y="166">LRCLIB (letras)</text>
<text class="pequeno" x="716" y="184">Seu site (JSON/RSS)</text>
<text class="pequeno" x="716" y="202">APIs dos plugins</text>
<!-- Serviços -->
<rect class="caixa" x="20" y="120" width="650" height="96" rx="12"/>
<text x="36" y="144" font-weight="700">Serviços (Services/)</text>
<text class="pequeno" x="36" y="168">MediaService · AudioService · SystemStatusService</text>
<text class="pequeno" x="36" y="188">LyricsService · WindowsNotificationService</text>
<text class="pequeno" x="36" y="206">traduzem eventos do Windows em dados e alertas</text>
<!-- Janela -->
<rect class="caixa preta" x="20" y="250" width="420" height="96" rx="14"/>
<text class="branco" x="36" y="276" font-weight="700">NotchWindow : INotchHost</text>
<text class="branco" x="36" y="298" font-size="12">estados · animações · fila de alertas</text>
<text class="branco" x="36" y="316" font-size="12">páginas · arrastar e soltar · bandeja · tela cheia</text>
<text class="branco" x="36" y="334" font-size="12">ShowAlert · OpenStore · Http · ShowPage</text>
<!-- Dados -->
<rect class="caixa" x="700" y="250" width="180" height="96" rx="12"/>
<text x="716" y="274" font-weight="700">Dados</text>
<text class="pequeno" x="716" y="296">%APPDATA%\ProjetoNotch</text>
<text class="pequeno" x="716" y="314">*.txt chave=valor</text>
<text class="pequeno" x="716" y="332">plugins\ · log.txt</text>
<!-- Sdk -->
<rect class="caixa forte" x="470" y="250" width="200" height="96" rx="12"/>
<text x="486" y="274" font-weight="700">Notch.Sdk</text>
<text class="pequeno" x="486" y="296">INotchModule · INotchHost</text>
<text class="pequeno" x="486" y="314">NotchAlert · KeyValueFile</text>
<text class="pequeno" x="486" y="332">Ui · NotchTheme · Glyphs</text>
<!-- Módulos -->
<rect class="caixa" x="20" y="380" width="420" height="74" rx="12"/>
<text x="36" y="404" font-weight="700">Ferramentas internas (Modules/)</text>
<text class="pequeno" x="36" y="426">Música · Letra · Pomodoro · Água · Tarefas</text>
<text class="pequeno" x="36" y="444">Prateleira · Site</text>
<rect class="caixa" x="470" y="380" width="410" height="74" rx="12"/>
<text x="486" y="404" font-weight="700">Plugins (DLL em %APPDATA%\ProjetoNotch\plugins)</text>
<text class="pequeno" x="486" y="426">carregados pelo PluginLoader em contexto isolado</text>
<text class="pequeno" x="486" y="444">ex.: Exemplo.Cotacao</text>
<!-- setas -->
<path class="seta" d="M300 86 V118"/>
<path class="seta" d="M230 216 V248"/>
<path class="seta" d="M670 168 H698"/>
<path class="seta azul" d="M230 378 V348"/>
<path class="seta azul" d="M640 378 V348"/>
<path class="seta azul" d="M470 298 H442"/>
<path class="seta" d="M400 250 C 480 230, 690 228, 770 248"/>
<text class="rot" x="560" y="236">salva</text>
<text class="rot" x="306" y="106">eventos</text>
<text class="rot" x="236" y="238">dados e alertas</text>
<text class="rot" x="238" y="370">INotchModule</text>
<text class="rot" x="646" y="370">INotchModule</text>
</svg>
</div>
<h3>Fluxo de inicialização</h3>
<ol>
<li><code>App.OnStartup</code> garante uma única instância (mutex), define o idioma pt-BR, cria as pastas de dados e instala o tratamento global de erros.</li>
<li><code>NotchWindow</code> cria os serviços, monta a pílula e as três camadas (notch fechado, alerta, notch aberto).</li>
<li><code>LoadModules</code> registra as 7 ferramentas internas, carrega os plugins da pasta e chama <code>Start(host)</code> em cada uma. Se uma falhar, ela é removida e o erro vai para o log.</li>
<li>Ao carregar a janela: inicia o monitor do sistema, conecta aos controles de mídia, liga as notificações (se configurado), cria o ícone da bandeja e, na primeira execução, mostra a mensagem de boas-vindas e ativa "Iniciar com o Windows".</li>
</ol>
</section>

<!-- =============================================================== -->
<section id="janela">
<h2>A janela do notch</h2>
<p>A janela é transparente, sem bordas, sempre no topo e invisível na barra de tarefas e no Alt+Tab. Dentro dela há uma única <code>Border</code> preta (a "concha") que muda de tamanho. Como a janela usa transparência por pixel, <b>as áreas transparentes deixam o clique passar</b> para as janelas de baixo: só a pílula é clicável.</p>
<h3>Os três estados</h3>
<p>Nesta documentação: <b>notch fechado</b> (ou compactado) é a pílula pequena parada no topo, funcionando só com os ícones; <b>notch aberto</b> (ou expandido) é quando ele abre o painel com as ferramentas; e <b>alerta</b> é quando a pílula se estica por alguns segundos para avisar algo. No código, eles se chamam <code>State.Compact</code>, <code>State.Expanded</code> e <code>State.Alert</code>.</p>
<div class="diagrama">
<svg viewBox="0 0 900 260" role="img" aria-label="Diagrama de estados: notch fechado, alerta e notch aberto">
<defs>
<marker id="ponta2" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0,0 L10,5 L0,10 z" fill="#3ea6ff"/></marker>
</defs>
<rect class="caixa preta" x="40" y="80" width="190" height="34" rx="17"/>
<text class="branco" x="135" y="102" text-anchor="middle" font-weight="700">Notch fechado · 224×32</text>
<rect class="caixa preta" x="355" y="20" width="200" height="56" rx="22"/>
<text class="branco" x="455" y="45" text-anchor="middle" font-weight="700">Alerta · 370×64</text>
<text class="branco" x="455" y="63" text-anchor="middle" font-size="11">(430 com botões)</text>
<rect class="caixa preta" x="655" y="70" width="210" height="120" rx="28"/>
<text class="branco" x="760" y="126" text-anchor="middle" font-weight="700">Notch aberto · 450×178</text>
<text class="branco" x="760" y="146" text-anchor="middle" font-size="11">cabeçalho · página · pontos</text>
<path d="M150 80 C 200 40, 290 34, 352 40" stroke="#3ea6ff" stroke-width="1.5" fill="none" marker-end="url(#ponta2)"/>
<text class="rot" x="170" y="38">alerta chega</text>
<path d="M360 70 C 320 96, 290 100, 234 98" stroke="#3ea6ff" stroke-width="1.5" fill="none" marker-end="url(#ponta2)"/>
<text class="rot" x="300" y="118">tempo esgota (fila vazia)</text>
<path d="M190 114 C 330 168, 520 166, 652 140" stroke="#3ea6ff" stroke-width="1.5" fill="none" marker-end="url(#ponta2)"/>
<text class="rot" x="345" y="182">mouse em cima / clique / arrastar arquivo</text>
<path d="M700 190 C 600 245, 260 245, 110 118" stroke="#3ea6ff" stroke-width="1.5" fill="none" marker-end="url(#ponta2)"/>
<text class="rot" x="390" y="252">mouse sai / clique fora</text>
<path d="M555 48 C 610 50, 640 70, 660 88" stroke="#3ea6ff" stroke-width="1.5" fill="none" marker-end="url(#ponta2)"/>
<text class="rot" x="575" y="40">clique no alerta</text>
</svg>
</div>
<ul>
<li><b>Animação:</b> largura e altura animam juntas. Para crescer, usa uma curva com leve "quique" (<code>BackEase</code>, 460 ms); para encolher, uma curva suave (<code>CubicEase</code>, 320 ms). O conteúdo troca com um esmaecimento cruzado.</li>
<li><b>Cantos:</b> só os de baixo são arredondados (a pílula "sai" da borda da tela). O raio cresce com o tamanho: ~14 px, 24 px e 30 px.</li>
<li><b>Sem roubar o foco:</b> o estilo <code>WS_EX_NOACTIVATE</code> impede que a janela ganhe foco ao ser clicada. Quando uma caixa de texto precisa do teclado, o app chama <code>RequestKeyboardFocus()</code> para ativá-la de propósito.</li>
<li><b>Tela cheia:</b> a cada 1,5 s, o app verifica se a janela em primeiro plano ocupa a tela toda. Se ocupar, o notch desaparece com um esmaecimento e para de receber cliques.</li>
<li><b>Sempre por cima:</b> no mesmo ciclo, o app reforça a posição "sempre no topo", já que a barra de tarefas às vezes a toma.</li>
<li><b>Páginas:</b> as ferramentas habilitadas são ordenadas pelo campo <code>Order</code>. Cada página é criada só na primeira vez que é aberta e depois reaproveitada. A troca desliza 28 px na direção da navegação.</li>
<li><b>Arrastar e soltar:</b> quando arquivos são arrastados sobre a pílula, o notch abre direto na Prateleira; ao soltar, os arquivos vão para <code>ShelfModule.Accept</code>.</li>
</ul>
</section>

<!-- =============================================================== -->
<section id="alertas">
<h2>Alertas</h2>
<p>Qualquer parte do app (ou um plugin) chama <code>host.ShowAlert(new NotchAlert(...))</code>. O alerta tem:</p>
<div class="tabela"><table>
<thead><tr><th>Campo</th><th>Uso</th></tr></thead>
<tbody>
<tr><td><code>Title</code>, <code>Message</code></td><td>Texto principal (negrito) e secundário.</td></tr>
<tr><td><code>Glyph</code> / <code>Image</code></td><td>Ícone da fonte Segoe Fluent Icons, ou uma imagem (capa, logo do app).</td></tr>
<tr><td><code>Accent</code></td><td>Cor do ícone e da barra.</td></tr>
<tr><td><code>Progress</code></td><td>0 a 1 para mostrar uma barra (volume, brilho, bateria).</td></tr>
<tr><td><code>Duration</code></td><td>Quanto tempo fica visível (padrão: 4 s).</td></tr>
<tr><td><code>Key</code></td><td>Alertas com a mesma chave se substituem (ex.: apertar volume várias vezes atualiza o mesmo alerta).</td></tr>
<tr><td><code>Actions</code></td><td>Até dois botões (ex.: "Bebi" / "Adiar"). O primeiro aparece em azul.</td></tr>
<tr><td><code>OnClick</code></td><td>Ação ao clicar no corpo do alerta. Sem ela, o clique abre o painel.</td></tr>
</tbody></table></div>
<h3>Regras da fila</h3>
<ul>
<li>Os alertas entram numa fila e aparecem um de cada vez.</li>
<li>Alertas com <code>Key</code> (volume, Caps Lock, rede...) atualizam o alerta atual em vez de entrar na fila, e são descartados se o painel estiver aberto.</li>
<li>Os outros (água, tarefa, site) esperam o painel fechar e aparecem em seguida.</li>
<li>Com o mouse sobre o alerta, o tempo para de correr; ao sair, sobra 1,5 s.</li>
<li>Em tela cheia, alertas rápidos são ignorados.</li>
</ul>
<pre class="bloco"><code class="language-csharp">host.ShowAlert(new NotchAlert("Hora de beber água", "Beba 250 ml · 0,5 de 2,0 L hoje")
{
    Glyph = Glyphs.Drop,
    Accent = NotchTheme.Accent,
    Duration = TimeSpan.FromSeconds(12),
    Actions = [new NotchAlertAction("Bebi", Drink), new NotchAlertAction("Adiar", Snooze)],
});</code></pre>
</section>

<!-- =============================================================== -->
<section id="ferramentas">
<h2>As ferramentas por dentro</h2>

<h3>Tocando Agora (<code>MusicModule</code> + <code>MediaService</code>)</h3>
<p>Usa os <b>controles de mídia do Windows</b> (<code>GlobalSystemMediaTransportControlsSessionManager</code>), os mesmos que aparecem no painel de volume do Windows 11. Assim funciona com qualquer player que se integre ao Windows, sem precisar de login no Spotify. O serviço acompanha a sessão atual e reage a três eventos: mudança de faixa (título, artista, capa), de reprodução (tocando/pausado) e de linha do tempo (posição). A posição exibida é calculada como <i>última posição informada + tempo decorrido desde então</i>, porque os players só informam a posição de vez em quando. Clicar na barra chama <code>TryChangePlaybackPositionAsync</code>.</p>

<h3>Letra da Música (<code>LyricsModule</code> + <code>LyricsService</code>)</h3>
<ol>
<li>Ao mudar de faixa, limpa o título (remove "(feat. ...)", "- Remastered 2011", "(Ao Vivo)" etc.).</li>
<li>Tenta a busca exata no LRCLIB (<code>/api/get</code> com artista, título, álbum e duração).</li>
<li>Se não achar, usa a busca aproximada (<code>/api/search</code>) e escolhe o resultado com letra sincronizada e duração mais próxima.</li>
<li>Converte o formato LRC (<code>[01:23.45] texto</code>) numa lista ordenada de linhas com tempo.</li>
<li>A cada 150 ms (só com a página visível), encontra a última linha cujo tempo ≤ posição + 250 ms e destaca essa linha com um esmaecimento.</li>
</ol>
<p>Os resultados ficam em cache durante a sessão. Erros de rede não entram no cache, para tentar de novo depois.</p>

<h3>Timer Pomodoro (<code>PomodoroModule</code>)</h3>
<p>É uma máquina de estados: <b>Foco → Pausa curta → Foco → ... → Pausa longa</b> (a cada 4 focos completos). O tempo final é guardado como um horário absoluto (<code>_endsAt</code>), então a contagem continua precisa mesmo com o painel fechado. Ao terminar: troca o modo, toca um som do sistema e mostra um alerta com o botão "Iniciar". Os tempos ficam em <code>pomodoro.txt</code>. Enquanto roda, a pílula mostra a contagem com o ícone do modo (laranja no foco, verde na pausa).</p>

<h3>Lembrete de Água (<code>WaterModule</code>)</h3>
<p>Distribui a meta ao longo do horário escolhido:</p>
<pre class="bloco"><code>intervalo = (fim − início) ÷ ⌈meta ÷ dose⌉        (mínimo de 15 min)
próximo   = máx(último gole ou lembrete, início) + intervalo</code></pre>
<p>Exemplo: das 08:00 às 18:00 (600 min), meta de 2.000 ml e dose de 250 ml → 8 lembretes, um a cada 75 min. Cada "Beber" soma a dose ao total, desconta da garrafa e registra no histórico por hora. Quando a garrafa deveria estar vazia, o alerta muda para "Hora de reabastecer". "Adiar" empurra o lembrete em 10 min. À meia-noite o dia reinicia, e o primeiro lembrete só vem um intervalo depois de ligar o PC. Clique no anel para alternar com o gráfico por hora.</p>

<h3>Lista de Tarefas (<code>TodoModule</code>)</h3>
<p>Cada tarefa é uma linha de <code>todo.txt</code> no formato <code>feito|diaFeito|hora|diaAvisado|título</code>. A cada 15 s, o módulo procura tarefas pendentes cuja hora já passou e que ainda não foram avisadas hoje. Concluídas de dias anteriores saem da lista ao abrir o app. No formulário, Enter salva e Esc cancela, e há atalhos "+30 min", "+1 h" e "Sem hora". O horário aceita formatos como <code>14:30</code>, <code>9:05</code>, <code>14h30</code> ou <code>14</code>.</p>

<h3>Prateleira de Arquivos (<code>ShelfModule</code>)</h3>
<p>Arquivos e pastas soltos são <b>copiados</b> para <code>%LOCALAPPDATA%\ProjetoNotch\Shelf</code> numa thread separada, sem travar a tela. Nomes repetidos ganham " (2)", " (3)"... Links viram atalhos <code>.url</code> e textos viram <code>.txt</code>. Cada item mostra o ícone do Windows (ou uma miniatura, se for imagem). Clicar abre o item; arrastar inicia um arrastar-e-soltar com o arquivo real; o "×" exclui. "Limpar" esvazia tudo.</p>

<h3>Novidades do Site (<code>SiteModule</code>)</h3>
<p>Veja a seção <a href="#site">Conectar seu site</a>. Internamente, a busca (<code>CheckAsync</code>) é separada da leitura (<code>Parse</code>). Os IDs já vistos ficam em <code>site.txt</code> (até 300), e a primeira consulta apenas memoriza os itens, para não disparar avisos antigos. Até 3 novidades geram alertas individuais; mais que isso gera um resumo ("5 novos itens").</p>
</section>

<!-- =============================================================== -->
<section id="servicos">
<h2>Serviços do Windows utilizados</h2>
<div class="tabela"><table>
<thead><tr><th>Função</th><th>API / técnica</th><th>Arquivo</th></tr></thead>
<tbody>
<tr><td>Música tocando</td><td><code>Windows.Media.Control</code> (GSMTC)</td><td>MediaService.cs</td></tr>
<tr><td>Volume e mudo</td><td>Core Audio <code>IAudioEndpointVolume</code> (via NAudio), com troca automática de dispositivo</td><td>AudioService.cs</td></tr>
<tr><td>Mutar microfone</td><td>Core Audio <code>IAudioEndpointVolume.Mute</code> no microfone padrão de comunicação; eventos de mudança + conferência a cada 1 s (alguns headsets, como o Astro A50, não avisam todas as mudanças por evento)</td><td>AudioService.cs</td></tr>
<tr><td>Nível do som (barrinhas)</td><td>Core Audio <code>IAudioMeterInformation.MasterPeakValue</code>, ~30 fps só quando visível e tocando</td><td>AudioService.cs, AudioBars.cs</td></tr>
<tr><td>Bateria e carregador</td><td><code>SystemInformation.PowerStatus</code>, consultado a cada 3 s</td><td>SystemStatusService.cs</td></tr>
<tr><td>Internet</td><td><code>NetworkChange</code> + <code>NetworkInformation.GetInternetConnectionProfile</code>, com espera de 2,5 s para estabilizar</td><td>SystemStatusService.cs</td></tr>
<tr><td>Bluetooth</td><td><code>DeviceWatcher</code> para dispositivos clássicos e BLE conectados</td><td>SystemStatusService.cs</td></tr>
<tr><td>Caps Lock</td><td><code>GetKeyState(VK_CAPITAL)</code> a cada 200 ms (<b>sem gancho de teclado</b>)</td><td>SystemStatusService.cs</td></tr>
<tr><td>Idioma do teclado</td><td><code>GetKeyboardLayout</code> da janela em primeiro plano</td><td>SystemStatusService.cs</td></tr>
<tr><td>Brilho</td><td>Evento WMI <code>WmiMonitorBrightnessEvent</code> (só notebooks/monitores compatíveis)</td><td>SystemStatusService.cs</td></tr>
<tr><td>Notificações</td><td><code>UserNotificationListener</code>, consultado a cada 2 s (o evento só existe em apps MSIX)</td><td>WindowsNotificationService.cs</td></tr>
<tr><td>Tela cheia / sempre no topo</td><td><code>GetForegroundWindow</code>, <code>GetWindowRect</code>, <code>SetWindowPos(HWND_TOPMOST)</code></td><td>NotchWindow.cs, Native.cs</td></tr>
<tr><td>Iniciar com o Windows</td><td>Registro <code>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</code></td><td>Autostart.cs</td></tr>
</tbody></table></div>
<div class="nota">Diferente do Nusa, que instalava um <b>gancho global de teclado</b> (<code>SetWindowsHookEx</code>), o Projeto Notch não intercepta o que você digita. Caps Lock e idioma são lidos só como <i>estado</i>, e o volume vem do próprio Windows. Isso também evita desconfiança de antivírus.</div>
</section>

<!-- =============================================================== -->
<section id="dados">
<h2>Dados e configurações</h2>
<p>Tudo fica em arquivos de texto, fáceis de copiar para outro PC ou fazer backup.</p>
<div class="tabela"><table>
<thead><tr><th>Arquivo</th><th>Conteúdo</th></tr></thead>
<tbody>
<tr><td><code>%APPDATA%\ProjetoNotch\settings.txt</code></td><td><code>page</code> (última página), <code>minimal</code>, <code>mascot</code>, <code>winnotif</code>, <code>disabled</code> (ferramentas desligadas, separadas por vírgula), <code>alerts.bateria</code>, <code>alerts.volume</code>, <code>alerts.brilho</code>, <code>alerts.caps</code>, <code>alerts.idioma</code>, <code>alerts.rede</code>, <code>alerts.bluetooth</code>, <code>welcomed</code></td></tr>
<tr><td><code>water.txt</code></td><td><code>bottle</code>, <code>goal</code>, <code>dose</code> (ml), <code>start</code>/<code>end</code> (minutos desde 00:00), <code>on</code>, <code>day</code>, <code>taken</code>, <code>inb</code> (na garrafa), <code>last</code>, <code>fired</code>, <code>hours</code> (24 valores)</td></tr>
<tr><td><code>pomodoro.txt</code></td><td><code>foco</code>, <code>curta</code>, <code>longa</code> (min), <code>ciclos</code></td></tr>
<tr><td><code>todo.txt</code></td><td>Uma tarefa por linha: <code>feito|diaFeito|hora|diaAvisado|título</code></td></tr>
<tr><td><code>site.txt</code></td><td><code>url</code>, <code>token</code>, <code>interval</code> (min), <code>on</code>, <code>initialized</code>, <code>seen</code> (IDs vistos), <code>status</code></td></tr>
<tr><td><code>log.txt</code></td><td>Erros e plugins carregados (recomeça ao passar de 512 KB)</td></tr>
<tr><td><code>plugins\&lt;Nome&gt;\*.dll</code></td><td>Plugins instalados</td></tr>
<tr><td><code>%LOCALAPPDATA%\ProjetoNotch\Shelf\</code></td><td>Arquivos da prateleira</td></tr>
</tbody></table></div>
<p>Para restaurar o padrão de uma ferramenta, feche o notch e apague o <code>.txt</code> dela.</p>
</section>

<!-- =============================================================== -->
<section id="site">
<h2>Conectar seu site</h2>
<p>Na página <b>Novidades do Site</b>, cole o endereço público de um arquivo de novidades e escolha o intervalo (5, 15, 30 ou 60 min). Clique em <b>Salvar e testar</b>. Na primeira consulta o notch só memoriza o que já existe; a partir daí, cada item com <code>id</code> novo gera um alerta com o botão <b>Abrir</b>.</p>
<h3>Formato 1: JSON simples (recomendado)</h3>
<pre class="bloco"><code class="language-json">{
  "items": [
    {
      "id": "lancamento-42",
      "titulo": "Nova taxa para consignado INSS",
      "descricao": "A partir de hoje, taxa de 1,66% a.m.",
      "url": "https://seusite.com.br/noticias/42",
      "data": "2026-10-25T10:00:00-03:00"
    }
  ]
}</code></pre>
<div class="tabela"><table>
<thead><tr><th>Campo</th><th>Nomes aceitos</th><th>Obrigatório</th></tr></thead>
<tbody>
<tr><td>Lista</td><td><code>items</code>, <code>itens</code>, <code>novidades</code>, <code>data</code>, <code>posts</code>, <code>lancamentos</code>, ou o JSON ser a própria lista <code>[ ... ]</code></td><td>sim</td></tr>
<tr><td>Identificador</td><td><code>id</code>, <code>guid</code>, <code>slug</code></td><td>recomendado (sem ele, usa a <code>url</code> ou o título)</td></tr>
<tr><td>Título</td><td><code>titulo</code>, <code>título</code>, <code>title</code>, <code>nome</code>, <code>name</code></td><td>recomendado</td></tr>
<tr><td>Descrição</td><td><code>descricao</code>, <code>description</code>, <code>resumo</code>, <code>summary</code>, <code>content_text</code>, <code>mensagem</code></td><td>não (HTML é removido; máx. 140 caracteres)</td></tr>
<tr><td>Link</td><td><code>url</code>, <code>link</code>, <code>href</code></td><td>não (sem ele, o alerta não tem "Abrir")</td></tr>
<tr><td>Data</td><td><code>data</code>, <code>date</code>, <code>date_published</code>, <code>published_at</code>, <code>publicado_em</code>, <code>created_at</code></td><td>não (ISO 8601, dd/mm/aaaa ou Unix)</td></tr>
</tbody></table></div>
<h3>Formato 2: RSS ou Atom</h3>
<p>Se o site é WordPress, Blogger, Wix, Medium ou similar, ele já tem um feed. Use, por exemplo, <code>https://seusite.com.br/feed</code>. São lidos <code>&lt;item&gt;</code> (RSS) e <code>&lt;entry&gt;</code> (Atom), com <code>guid/id</code>, <code>title</code>, <code>link</code>, <code>description/summary</code> e <code>pubDate/published</code>.</p>
<h3>Formato 3: JSON Feed</h3>
<p>O padrão <a href="https://www.jsonfeed.org" rel="noopener">jsonfeed.org</a> funciona direto. Ele foi usado nos testes.</p>
<h3>Como publicar o arquivo no seu site</h3>
<ul>
<li><b>Site estático:</b> suba um <code>novidades.json</code> e atualize-o a cada lançamento, colocando o item novo no topo.</li>
<li><b>Com painel/servidor:</b> crie uma rota que gere o JSON a partir do banco. Exemplo em PHP:</li>
</ul>
<pre class="bloco"><code class="language-php">&lt;?php // novidades.php
header('Content-Type: application/json; charset=utf-8');
$itens = $pdo-&gt;query("SELECT id, titulo, descricao, url, publicado_em AS data
                      FROM lancamentos ORDER BY publicado_em DESC LIMIT 20")-&gt;fetchAll(PDO::FETCH_ASSOC);
echo json_encode(['items' =&gt; $itens], JSON_UNESCAPED_UNICODE);</code></pre>
<h3>Endereço protegido</h3>
<p>Se o endereço exigir autenticação, preencha <b>Token</b>. O notch envia <code>Authorization: Bearer &lt;token&gt;</code>; no servidor, basta comparar esse cabeçalho com o seu segredo.</p>
<h3>Evolução: avisos instantâneos</h3>
<p>Hoje a consulta é periódica (polling), o que é simples, funciona atrás de qualquer firewall e não exige servidor especial. Para avisos no mesmo segundo, o próximo passo é trocar <code>CheckAsync</code> por uma conexão contínua (<b>Server-Sent Events</b> ou <b>WebSocket</b>). Como a leitura (<code>Parse</code>) e a interface são separadas, essa troca não mexe no resto do módulo.</p>
</section>

<!-- =============================================================== -->
<section id="plugins">
<h2>Criar ferramentas novas (plugins)</h2>
<p>Uma ferramenta é uma classe pública que implementa <code>INotchModule</code>. As ferramentas internas usam o mesmo contrato, então um plugin pode fazer tudo o que elas fazem.</p>
<h3>Passo a passo</h3>
<ol class="passos">
<li><b>Crie o projeto</b> copiando <code>plugins\Exemplo.Cotacao</code> (por exemplo, para <code>plugins\MinhaFerramenta</code>) e renomeie o <code>.csproj</code>.</li>
<li><b>Escreva a classe</b> que implementa <code>INotchModule</code> (veja o modelo abaixo).</li>
<li><b>Compile e instale</b> direto na pasta de plugins:
<pre class="bloco"><code class="language-powershell">dotnet build plugins\MinhaFerramenta -c Release -o "$env:APPDATA\ProjetoNotch\plugins\MinhaFerramenta"</code></pre></li>
<li><b>Reinicie o notch</b> (menu → Sair, depois abra de novo). A página nova aparece no carrossel, e no menu "Ferramentas" dá para ligar e desligar.</li>
</ol>
<h3>Modelo mínimo</h3>
<pre class="bloco"><code class="language-csharp">using System.Windows;
using Notch.Sdk;

public sealed class MinhaFerramenta : INotchModule
{
    public string Id => "minha-ferramenta";     // único e estável
    public string Title => "Minha";             // parte branca do título
    public string TitleAccent => "Ferramenta";  // parte azul
    public int Order => 90;                     // posição no carrossel

    private INotchHost _host = null!;
    private KeyValueFile _dados = null!;

    public void Start(INotchHost host)
    {
        _host = host;
        _dados = host.OpenStore("minha-ferramenta");   // %APPDATA%\ProjetoNotch\minha-ferramenta.txt
    }

    public FrameworkElement CreateView()
    {
        var contador = Ui.Text($"Cliques: {_dados.GetInt("cliques", 0)}", 16, FontWeights.Bold);
        return Ui.Column(10,
            contador,
            Ui.Row(8,
                Ui.Button(Glyphs.Add, "Somar", () =>
                {
                    _dados.Set("cliques", _dados.GetInt("cliques", 0) + 1);
                    _dados.Save();
                    contador.Text = $"Cliques: {_dados.GetInt("cliques", 0)}";
                }, NotchTheme.AccentDeep),
                Ui.Button(Glyphs.Bell, "Alerta", () =>
                    _host.ShowAlert(new NotchAlert("Funcionou!", "Meu primeiro plugin") { Accent = NotchTheme.Green }))));
    }

    public void Stop() { }
}</code></pre>
<h3>Referência do SDK</h3>
<h4><code>INotchModule</code> (você implementa)</h4>
<div class="tabela"><table><thead><tr><th>Membro</th><th>Descrição</th></tr></thead><tbody>
<tr><td><code>Id</code></td><td>Identificador único (usado no menu, nas configurações e em <code>ShowPage</code>).</td></tr>
<tr><td><code>Title</code> / <code>TitleAccent</code></td><td>Título em duas cores no cabeçalho.</td></tr>
<tr><td><code>Order</code></td><td>Posição (internas: 10, 20, 30, 40, 50, 60, 70; plugins: 80 ou mais).</td></tr>
<tr><td><code>Start(host)</code></td><td>Chamado uma vez ao abrir o app. Guarde o host e inicie timers.</td></tr>
<tr><td><code>CreateView()</code></td><td>Cria a página (área de ~380×110). Chamado na primeira vez que a página é aberta.</td></tr>
<tr><td><code>Stop()</code></td><td>Chamado ao sair. Pare timers e salve dados.</td></tr>
</tbody></table></div>
<h4><code>INotchHost</code> (o notch oferece)</h4>
<div class="tabela"><table><thead><tr><th>Membro</th><th>Descrição</th></tr></thead><tbody>
<tr><td><code>ShowAlert(NotchAlert)</code></td><td>Mostra um alerta (pode ser chamado de qualquer thread).</td></tr>
<tr><td><code>OpenStore(nome)</code></td><td>Arquivo chave=valor (<code>Get</code>, <code>GetInt</code>, <code>GetBool</code>, <code>Set</code>, <code>Save</code>).</td></tr>
<tr><td><code>DataDirectory</code></td><td>Pasta de dados, para arquivos próprios.</td></tr>
<tr><td><code>Http</code></td><td><code>HttpClient</code> compartilhado (timeout de 25 s).</td></tr>
<tr><td><code>ShowPage(id)</code></td><td>Abre o notch numa página.</td></tr>
<tr><td><code>RequestKeyboardFocus()</code></td><td>Necessário antes de focar uma caixa de texto.</td></tr>
</tbody></table></div>
<h4>Componentes <code>Ui</code></h4>
<div class="tabela"><table><thead><tr><th>Método</th><th>Cria</th></tr></thead><tbody>
<tr><td><code>Ui.Text(texto, tamanho, peso, cor)</code></td><td>Texto com reticências automáticas.</td></tr>
<tr><td><code>Ui.Icon(glifo, tamanho, cor)</code></td><td>Ícone da fonte Segoe Fluent Icons (constantes em <code>Glyphs</code>).</td></tr>
<tr><td><code>Ui.Row(espaço, ...)</code> / <code>Ui.Column(espaço, ...)</code></td><td>Linha / coluna com espaçamento.</td></tr>
<tr><td><code>Ui.Button(glifo, texto, ação, fundo, cor)</code></td><td>Botão em pílula.</td></tr>
<tr><td><code>Ui.IconButton(glifo, ação, tamanho)</code></td><td>Botão redondo.</td></tr>
<tr><td><code>Ui.Chip(texto, selecionado, ação)</code></td><td>Opção selecionável.</td></tr>
<tr><td><code>Ui.Toggle(ligado, aoMudar, glifo)</code></td><td>Interruptor liga/desliga.</td></tr>
<tr><td><code>Ui.ProgressBar(valor)</code> + <code>Ui.SetProgress</code></td><td>Barra de progresso fina.</td></tr>
<tr><td><code>Ui.Input(exemplo, out caixa, inicial)</code></td><td>Campo de texto escuro com texto de exemplo.</td></tr>
<tr><td><code>Ui.Clickable(border, ação)</code></td><td>Transforma qualquer <code>Border</code> em botão.</td></tr>
</tbody></table></div>
<h4>Cores (<code>NotchTheme</code>)</h4>
<p><code>Background</code>, <code>Surface</code>, <code>SurfaceHover</code>, <code>SurfaceStrong</code>, <code>Text</code>, <code>TextDim</code>, <code>TextFaint</code>, <code>Accent</code>, <code>AccentDeep</code>, <code>Green</code>, <code>Orange</code>, <code>Red</code>, <code>Purple</code>, <code>Yellow</code>, mais as fontes <code>Font</code> e <code>Icons</code>.</p>
<div class="nota aviso"><b>Boas práticas:</b> não bloqueie a interface (use <code>async/await</code> para internet e disco); atualize a tela com timers só quando a página estiver visível (<code>IsVisibleChanged</code>); trate exceções de rede. Um plugin com erro é ignorado e registrado em <code>log.txt</code>, mas um plugin mal escrito ainda pode deixar o notch lento.</div>
<div class="nota aviso"><b>Segurança:</b> plugins rodam com as mesmas permissões do notch, ou seja, as suas. Instale apenas DLLs que você mesmo compilou ou de fontes de confiança.</div>
</section>

<!-- =============================================================== -->
<section id="ideias">
<h2>Ideias de ferramentas para o futuro</h2>
<div class="cartoes">
<div class="cartao"><h4>Tabela de comissões</h4><p>Consulta rápida de taxas e comissões por banco, convênio e prazo, lida de uma planilha ou do seu site.</p></div>
<div class="cartao"><h4>Painel de propostas</h4><p>Contador de propostas do dia, puxado da API do seu sistema ou do site, com alerta a cada nova proposta.</p></div>
<div class="cartao"><h4>Agenda do dia</h4><p>Próximos compromissos do Outlook/Google Agenda com aviso 10 min antes.</p></div>
<div class="cartao"><h4>Clima</h4><p>Temperatura e chance de chuva na sua cidade (Open-Meteo, gratuito).</p></div>
<div class="cartao"><h4>Atalhos</h4><p>Botões para abrir sistemas, pastas e sites de trabalho com um clique.</p></div>
<div class="cartao"><h4>Calculadora rápida</h4><p>Simulação de parcelas e juros direto no notch.</p></div>
<div class="cartao"><h4>Área de transferência</h4><p>Histórico dos últimos textos copiados.</p></div>
<div class="cartao"><h4>Status de sites</h4><p>Avisa se o seu site ou sistema ficar fora do ar.</p></div>
<div class="cartao"><h4>Contatos da equipe</h4><p>WhatsApp e endereços das unidades com botão para copiar.</p></div>
</div>
</section>

<!-- =============================================================== -->
<section id="compilar">
<h2>Compilar e distribuir</h2>
<h3>Requisitos</h3>
<ul>
<li>Windows 10 (2004+) ou 11.</li>
<li>Para compilar: <a href="https://dotnet.microsoft.com/download" rel="noopener">.NET 10 SDK</a> (<code>winget install Microsoft.DotNet.SDK.10</code>).</li>
<li>Para rodar: .NET 10 Desktop Runtime (vem junto com o SDK).</li>
</ul>
<h3>Comandos</h3>
<pre class="bloco"><code class="language-powershell"># gerar o executável final em dist\ProjetoNotch.exe
.\publicar.ps1

# o mesmo, instalando também o plugin de exemplo
.\publicar.ps1 -ComExemplo

# rodar direto do código, para desenvolvimento
dotnet run --project src\Notch.App

# regenerar esta documentação
python docs\gerar_documentacao.py</code></pre>
<div class="nota">Para atualizar o app em uso: menu → <b>Sair</b>, rode <code>.\publicar.ps1</code> e abra <code>dist\ProjetoNotch.exe</code> de novo. A compilação grava os arquivos temporários em <code>%LOCALAPPDATA%\ProjetoNotch-build</code>, fora do OneDrive, para não sincronizar lixo.</div>
<h3>Levar para outro computador</h3>
<ol>
<li>Instale o .NET 10 Desktop Runtime no outro PC.</li>
<li>Copie <code>dist\ProjetoNotch.exe</code> (e, se quiser, a pasta <code>%APPDATA%\ProjetoNotch</code> com as configurações e os plugins).</li>
<li>Abra o executável. Na primeira vez ele já se configura para iniciar com o Windows.</li>
</ol>
<div class="nota aviso">O executável não tem assinatura digital, então o Windows SmartScreen pode avisar na primeira execução em outro PC ("Mais informações → Executar assim mesmo"). Para distribuir para mais pessoas, o ideal é assinar com um certificado de assinatura de código.</div>
<h3>Desinstalar</h3>
<p>Menu → desmarque <b>Iniciar com o Windows</b> → <b>Sair</b>. Depois apague <code>dist\ProjetoNotch.exe</code>, <code>%APPDATA%\ProjetoNotch</code> e <code>%LOCALAPPDATA%\ProjetoNotch</code>.</p>
</section>

<!-- =============================================================== -->
<section id="privacidade">
<h2>Privacidade e segurança</h2>
<ul>
<li><b>Nada é enviado para servidores próprios.</b> As únicas conexões são: <b>lrclib.net</b> (título e artista da música, para buscar a letra), o <b>seu site</b> (se configurado) e as APIs que cada plugin usa (o exemplo usa a AwesomeAPI).</li>
<li><b>Sem gancho de teclado:</b> o app não lê o que você digita.</li>
<li><b>Notificações do Windows</b> ficam desligadas por padrão. Ao ligar, o Windows pede sua permissão, e elas são só exibidas no notch, sem gravação.</li>
<li><b>Sem administrador:</b> roda com as permissões do seu usuário; a inicialização automática usa só o registro do seu usuário.</li>
<li><b>Dados locais:</b> todos os arquivos ficam no seu perfil (<code>%APPDATA%</code> e <code>%LOCALAPPDATA%</code>).</li>
<li><b>Token do site:</b> é guardado em texto puro em <code>site.txt</code>. Use um token só de leitura.</li>
</ul>
</section>

<!-- =============================================================== -->
<section id="problemas">
<h2>Solução de problemas</h2>
<div class="tabela"><table>
<thead><tr><th>Problema</th><th>O que fazer</th></tr></thead>
<tbody>
<tr><td>O notch não aparece</td><td>Veja se está rodando (ícone perto do relógio). Se há um app em tela cheia, ele se esconde. Abra <code>dist\ProjetoNotch.exe</code> de novo.</td></tr>
<tr><td>Dois notches sobrepostos</td><td>O Nusa ainda está aberto. Feche-o pelo Gerenciador de Tarefas e desative a inicialização dele.</td></tr>
<tr><td>"Nada tocando" com música tocando</td><td>O player precisa se integrar aos controles de mídia do Windows. Spotify, navegadores, Deezer e Apple Music integram; alguns players antigos não.</td></tr>
<tr><td>"Letra não disponível"</td><td>A música não está no LRCLIB, ou é uma transmissão ao vivo/vídeo. Músicas de catálogo costumam funcionar.</td></tr>
<tr><td>Notificações do Windows não aparecem</td><td>Ative em Configurações do Windows → Privacidade → Notificações → permitir acesso. Se o Windows recusar para apps fora da Microsoft Store, a opção é desligada e um aviso aparece.</td></tr>
<tr><td>Erro no site</td><td>O status mostra a causa (endereço errado, sem internet, JSON inválido). Abra o endereço no navegador para conferir.</td></tr>
<tr><td>Plugin não aparece</td><td>Confira se a DLL está em <code>%APPDATA%\ProjetoNotch\plugins\&lt;Pasta&gt;\</code>, se a classe é <code>public</code> com construtor sem parâmetros, e veja o <code>log.txt</code>.</td></tr>
<tr><td>Algo estranho</td><td>Menu → <b>Abrir pasta de dados</b> → <code>log.txt</code>.</td></tr>
</tbody></table></div>
</section>

<!-- =============================================================== -->
<section id="codigo">
<h2>Código-fonte completo</h2>
<p class="sub">Todos os {{ARQUIVOS}} arquivos ({{LINHAS}} linhas), exatamente como estão no projeto. Clique num arquivo para expandir.</p>
<div class="botoes-codigo"><button type="button" id="abrirTodos">Expandir todos</button><button type="button" id="fecharTodos">Recolher todos</button></div>
<h3>Mapa de arquivos</h3>
{{TABELA_ARQUIVOS}}
{{CODIGO}}
</section>

<footer>Projeto Notch 1.0.0 · Documentação gerada por <code>docs/gerar_documentacao.py</code> em {{DATA}} · Letras por LRCLIB · Inspirado no Nusa.</footer>
</main>
</div>
<a class="voltar" href="#topo" aria-label="Voltar ao topo">↑</a>

<script src="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/highlight.min.js"></script>
<script src="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/languages/powershell.min.js"></script>
<script>
(function () {
  // destaque de sintaxe (se a internet estiver disponível; sem ela o código aparece sem cores)
  function realcar(raiz) {
    if (!window.hljs) return;
    raiz.querySelectorAll('pre code').forEach(function (el) {
      if (!el.dataset.ok) { try { hljs.highlightElement(el); } catch (e) {} el.dataset.ok = '1'; }
    });
  }
  realcar(document.querySelector('main'));
  document.querySelectorAll('details.arquivo').forEach(function (d) {
    d.addEventListener('toggle', function () { if (d.open) realcar(d); });
  });

  // copiar código
  document.querySelectorAll('.copiar').forEach(function (b) {
    b.addEventListener('click', function () {
      var texto = b.parentElement.querySelector('code').innerText;
      var ok = function () { b.textContent = 'Copiado!'; setTimeout(function () { b.textContent = 'Copiar'; }, 1500); };
      if (navigator.clipboard) navigator.clipboard.writeText(texto).then(ok, function () {}); else ok();
    });
  });

  document.getElementById('abrirTodos').addEventListener('click', function () {
    document.querySelectorAll('details.arquivo').forEach(function (d) { d.open = true; });
  });
  document.getElementById('fecharTodos').addEventListener('click', function () {
    document.querySelectorAll('details.arquivo').forEach(function (d) { d.open = false; });
  });

  // abrir o arquivo quando o link do mapa é clicado
  document.querySelectorAll('a[href^="#arq-"]').forEach(function (a) {
    a.addEventListener('click', function () {
      var d = document.querySelector(a.getAttribute('href'));
      if (d) d.open = true;
    });
  });

  // tema claro/escuro (lembra a escolha)
  var raiz = document.documentElement;
  try { var salvo = localStorage.getItem('notch-doc-tema'); if (salvo) raiz.dataset.theme = salvo; } catch (e) {}
  document.getElementById('tema').addEventListener('click', function () {
    var escuro = raiz.dataset.theme ? raiz.dataset.theme === 'dark' : matchMedia('(prefers-color-scheme: dark)').matches;
    raiz.dataset.theme = escuro ? 'light' : 'dark';
    try { localStorage.setItem('notch-doc-tema', raiz.dataset.theme); } catch (e) {}
  });

  // destaca no menu a seção visível
  var links = {};
  document.querySelectorAll('nav.lateral a.item').forEach(function (a) { links[a.getAttribute('href').slice(1)] = a; });
  if ('IntersectionObserver' in window) {
    var obs = new IntersectionObserver(function (entradas) {
      entradas.forEach(function (e) {
        if (e.isIntersecting && links[e.target.id]) {
          Object.values(links).forEach(function (l) { l.classList.remove('ativo'); });
          links[e.target.id].classList.add('ativo');
        }
      });
    }, { rootMargin: '-20% 0px -70% 0px' });
    document.querySelectorAll('main section').forEach(function (s) { obs.observe(s); });
  }
})();
</script>
</body>
</html>
"""

if __name__ == "__main__":
    main()
