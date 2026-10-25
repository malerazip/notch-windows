using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Notch.Sdk;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace Notch.App.Services;

/// <summary>
/// Espelha no notch as notificações do Windows (WhatsApp, Teams, e-mail...).
/// Desligado por padrão: o Windows pede permissão ao ligar.
/// </summary>
internal sealed class WindowsNotificationService(Dispatcher dispatcher, Action<NotchAlert> alert)
{
    private readonly HashSet<uint> _seen = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private UserNotificationListener? _listener;
    private bool _polling;

    public bool Running { get; private set; }

    /// <summary>Retorna uma mensagem de erro, ou null se começou a funcionar.</summary>
    public async Task<string?> StartAsync()
    {
        try
        {
            _listener = UserNotificationListener.Current;
            var access = await _listener.RequestAccessAsync();
            if (access != UserNotificationListenerAccessStatus.Allowed)
                return "Permissão negada. Libere em Configurações > Privacidade > Notificações.";

            foreach (var n in await _listener.GetNotificationsAsync(NotificationKinds.Toast)) _seen.Add(n.Id);
            _timer.Tick -= OnTick;
            _timer.Tick += OnTick;
            _timer.Start();
            Running = true;
            return null;
        }
        catch (Exception ex)
        {
            Core.Paths.WriteLog("Notificações do Windows indisponíveis: " + ex.Message);
            return "O Windows não liberou o acesso às notificações para este app.";
        }
    }

    public void Stop()
    {
        _timer.Stop();
        Running = false;
    }

    // Consulta periódica: o evento NotificationChanged só funciona em apps empacotados (MSIX).
    private async void OnTick(object? sender, EventArgs e)
    {
        if (_polling || _listener == null) return;
        _polling = true;
        try
        {
            var list = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
            foreach (var n in list)
            {
                if (!_seen.Add(n.Id)) continue;
                var (title, body) = Texts(n);
                var app = n.AppInfo?.DisplayInfo?.DisplayName ?? "Notificação";
                var logo = await LogoAsync(n);
                await dispatcher.InvokeAsync(() => alert(new NotchAlert(title.Length > 0 ? title : app, body.Length > 0 ? body : app)
                {
                    Glyph = Glyphs.Message,
                    Image = logo,
                    Accent = NotchTheme.Accent,
                    Duration = TimeSpan.FromSeconds(5),
                }));
            }
            if (_seen.Count > 500) _seen.IntersectWith(list.Select(n => n.Id));
        }
        catch (Exception ex)
        {
            Core.Paths.WriteLog("Leitura de notificações: " + ex.Message);
        }
        finally { _polling = false; }
    }

    private static (string title, string body) Texts(UserNotification n)
    {
        var binding = n.Notification?.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric);
        var texts = binding?.GetTextElements().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? [];
        return (texts.FirstOrDefault() ?? "", string.Join(" · ", texts.Skip(1)));
    }

    private static async Task<ImageSource?> LogoAsync(UserNotification n)
    {
        try
        {
            var stream = await n.AppInfo.DisplayInfo.GetLogo(new Windows.Foundation.Size(48, 48)).OpenReadAsync();
            using var s = stream.AsStreamForRead();
            var ms = new MemoryStream();
            await s.CopyToAsync(ms);
            ms.Position = 0;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }
}
