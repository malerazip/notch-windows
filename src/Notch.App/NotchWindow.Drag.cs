using System.Windows;
using System.Windows.Input;
using Notch.App.Core;

namespace Notch.App;

/// <summary>
/// Arrastar o notch com o mouse para os lados, ao longo do topo da tela.
/// Um clique simples continua abrindo o painel: só vira arrasto depois que o mouse anda alguns pixels
/// com o botão apertado. A posição fica salva (settings.txt, chave "offset", em pontos a partir do centro).
/// </summary>
internal sealed partial class NotchWindow
{
    private const double DragThreshold = 6;     // pixels andados com o botão apertado até virar arrasto
    private const double HeaderHeight = 34;     // com o painel aberto, só arrasta pelo cabeçalho

    private double _homeOffset;                 // posição escolhida pelo usuário (0 = centro da tela)
    private bool _pressed;
    private bool _moving;
    private Point _pressCursor;
    private double _pressLeft;

    /// <summary>Posição "de casa" da janela: o centro da tela mais o deslocamento salvo.</summary>
    private double HomeLeft => CenterLeft + _homeOffset;

    /// <summary>Sendo arrastado agora (o desvio das abas fica pausado).</summary>
    private bool IsBeingDragged => _moving;

    private void WireDrag()
    {
        _homeOffset = _settings.GetInt("offset", 0);
        ClampHomeOffset();

        _shell.PreviewMouseLeftButtonDown += (_, e) =>
        {
            // painel aberto: arrasta só pelo cabeçalho, para não atrapalhar botões, listas e campos de texto
            if (_state == State.Expanded && e.GetPosition(_expandedLayer).Y > HeaderHeight) return;
            if (!TryCursorDip(out var c)) return;
            _pressed = true;
            _pressCursor = c;
            _pressLeft = Left;
        };

        _shell.PreviewMouseMove += (_, e) =>
        {
            if (!_pressed) return;
            if (e.LeftButton != MouseButtonState.Pressed) { _pressed = false; return; }
            if (!TryCursorDip(out var c)) return;
            var dx = c.X - _pressCursor.X;
            if (!_moving)
            {
                if (Math.Abs(dx) < DragThreshold && Math.Abs(c.Y - _pressCursor.Y) < DragThreshold) return;
                _moving = true;
                _hoverTimer.Stop();
                _alertTimer.Stop();                // alerta não some no meio do arrasto
                // se estava desviado das abas, o lugar onde for solto vira a nova casa
                BeginAnimation(LeftProperty, null);
                _dodgeOffset = 0;
                _returnAt = null;
                _shell.CaptureMouse();
                _shell.Cursor = Cursors.SizeWE;
            }
            Left = CenterLeft + ClampOffset(_pressLeft + dx - CenterLeft);
        };

        _shell.PreviewMouseLeftButtonUp += (_, e) =>
        {
            _pressed = false;
            if (!_moving) return;              // foi só um clique: abre o painel normalmente
            FinishDrag();
            e.Handled = true;                  // soltar depois de arrastar não conta como clique
        };

        _shell.LostMouseCapture += (_, _) => { if (_moving) FinishDrag(); };
    }

    private void FinishDrag()
    {
        _moving = false;
        _pressed = false;
        _shell.Cursor = null;
        if (_shell.IsMouseCaptured) _shell.ReleaseMouseCapture();
        _homeOffset = Math.Round(Left - CenterLeft);
        SaveHomeOffset();
        if (_state == State.Alert && !_shell.IsMouseOver) _alertTimer.Start();
    }

    /// <summary>Menu "Voltar o notch para o centro".</summary>
    private void ResetHomeOffset()
    {
        _homeOffset = 0;
        SaveHomeOffset();
        MoveTo(0);
    }

    private void SaveHomeOffset()
    {
        _settings.Set("offset", (int)_homeOffset);
        _settings.Save();
    }

    /// <summary>Limita o deslocamento para o notch (até aberto, que é o mais largo) caber inteiro na tela.</summary>
    private static double ClampOffset(double offset)
    {
        var limit = Math.Max(0, SystemParameters.PrimaryScreenWidth / 2 - ExpandedW / 2 - 8);
        return Math.Clamp(offset, -limit, limit);
    }

    /// <summary>A tela mudou de tamanho (outro monitor, outra resolução): mantém o notch dentro dela.</summary>
    private void ClampHomeOffset() => _homeOffset = ClampOffset(_homeOffset);

    /// <summary>Posição do mouse em pontos do WPF (a mesma unidade de Left/Width).</summary>
    private bool TryCursorDip(out Point point)
    {
        point = default;
        if (!Native.GetCursorPos(out var p)) return false;
        var target = PresentationSource.FromVisual(this)?.CompositionTarget;
        if (target == null) return false;
        point = target.TransformFromDevice.Transform(new Point(p.X, p.Y));
        return true;
    }
}
