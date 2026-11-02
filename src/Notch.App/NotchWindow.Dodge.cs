using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Notch.App.Core;

namespace Notch.App;

/// <summary>
/// "Desviar das abas do navegador": com o Chrome (ou outro navegador) aberto embaixo do notch,
/// quando o mouse chega perto o notch desliza para o lado oposto e libera as abas.
/// Ele fica nesse lugar (e continua clicável lá) até o mouse sair do topo da tela; aí volta ao centro.
/// </summary>
internal sealed partial class NotchWindow
{
    // processos tratados como navegador (nome do .exe, sem extensão)
    private static readonly HashSet<string> Browsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "opera_gx", "vivaldi", "arc", "zen", "chromium",
    };

    private const double DodgeMargin = 24;      // distância extra entre a posição original e o notch desviado
    private const double DodgeZonePad = 20;     // o desvio começa um pouco antes de o mouse encostar no notch
    private const double TopBand = 70;          // altura (em pontos da tela) da faixa das abas / barra de título
    private static readonly TimeSpan ReturnDelay = TimeSpan.FromMilliseconds(800);

    private readonly DispatcherTimer _dodgeTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private double _dodgeOffset;               // 0 = centro; positivo = direita; negativo = esquerda
    private DateTime? _returnAt;
    private uint _lastPid;
    private bool _lastPidIsBrowser;

    private bool Dodged => _dodgeOffset != 0;

    /// <summary>Opção do menu "Desviar das abas do navegador" (ligada por padrão).</summary>
    private bool DodgeEnabled => Setting("dodge", true);

    private void StartDodge()
    {
        _dodgeTimer.Tick += (_, _) => DodgeTick();
        _dodgeTimer.Start();
    }

    private double CenterLeft => SystemParameters.PrimaryScreenWidth / 2 - Width / 2;

    private void DodgeTick()
    {
        if (_hwnd == 0 || !Native.GetCursorPos(out var p)) return;
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget == null) return;
        var cursor = source.CompositionTarget.TransformFromDevice.Transform(new Point(p.X, p.Y));

        if (!Dodged)
        {
            // desvia só com o notch fechado ou mostrando um alerta (o painel aberto é para usar)
            if (!DodgeEnabled || IsHidden || _dragging || _state == State.Expanded) return;
            var w = _shell.ActualWidth;
            var h = _shell.ActualHeight;
            var shellLeft = Left + (Width - w) / 2;
            var inZone = cursor.X >= shellLeft - DodgeZonePad && cursor.X <= shellLeft + w + DodgeZonePad
                         && cursor.Y <= h + DodgeZonePad / 2;
            if (!inZone || !BrowserUnderNotch(source, shellLeft, w)) return;

            // vai para o lado contrário ao do mouse; se não couber na tela, vai para o outro
            var direction = cursor.X < shellLeft + w / 2 ? 1 : -1;
            var distance = w + DodgeMargin;
            var screenW = SystemParameters.PrimaryScreenWidth;
            if (shellLeft + direction * distance < 0 || shellLeft + w + direction * distance > screenW) direction = -direction;
            _hoverTimer.Stop(); // não abre o painel por estar passando o mouse: o mouse queria as abas
            MoveTo(direction * distance);
            return;
        }

        // já desviado: volta ao centro quando o mouse sai do topo da tela por um instante
        var bandHeight = Math.Max(TopBand, _shell.ActualHeight + 10);
        if (cursor.Y <= bandHeight && DodgeEnabled && !IsHidden)
        {
            _returnAt = null;
            return;
        }
        if (_state == State.Expanded || _dragging) return; // não mexe no painel aberto
        _returnAt ??= DateTime.Now + ReturnDelay;
        if (DateTime.Now >= _returnAt) ReturnToCenter();
    }

    /// <summary>O navegador em primeiro plano está com a parte de cima (abas) embaixo do notch?</summary>
    private bool BrowserUnderNotch(PresentationSource source, double shellLeft, double shellWidth)
    {
        var fg = Native.GetForegroundWindow();
        if (fg == 0 || fg == _hwnd || !IsBrowser(fg) || !Native.GetWindowRect(fg, out var r)) return false;
        var m = source.CompositionTarget!.TransformFromDevice;
        var topLeft = m.Transform(new Point(r.Left, r.Top));
        var bottomRight = m.Transform(new Point(r.Right, r.Bottom));
        // janela maximizada fica alguns pixels acima de 0; uma janela solta no topo também conta
        var touchesTop = topLeft.Y <= 12;
        var coversNotch = topLeft.X < shellLeft + shellWidth && bottomRight.X > shellLeft;
        return touchesTop && coversNotch;
    }

    private bool IsBrowser(nint hwnd)
    {
        Native.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0) return false;
        if (pid == _lastPid) return _lastPidIsBrowser;
        bool browser;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            browser = Browsers.Contains(process.ProcessName);
        }
        catch { browser = false; }
        (_lastPid, _lastPidIsBrowser) = (pid, browser);
        return browser;
    }

    private void MoveTo(double offset, bool animate = true)
    {
        _dodgeOffset = offset;
        _returnAt = null;
        var target = CenterLeft + offset;
        if (!animate)
        {
            BeginAnimation(LeftProperty, null);
            Left = target;
            return;
        }
        var from = Left; // posição atual (inclusive no meio de outra animação)
        var anim = new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop,
        };
        // ao terminar, grava a posição final de verdade (sem deixar a animação "segurando" o valor)
        anim.Completed += (_, _) => { if (_dodgeOffset == offset) Left = target; };
        Left = target;
        BeginAnimation(LeftProperty, anim);
    }

    private void ReturnToCenter()
    {
        MoveTo(0);
        // alertas que chegaram enquanto o notch estava de lado aparecem agora
        if (_state == State.Compact && _alerts.Count > 0 && !IsHidden)
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
            t.Tick += (_, _) => { t.Stop(); if (_state == State.Compact && !IsHidden && !Dodged) ShowNextAlert(); };
            t.Start();
        }
    }
}
