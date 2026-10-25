using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using Notch.App.Core;

namespace Notch.App;

public partial class App : Application
{
    private Mutex? _single;

    protected override void OnStartup(StartupEventArgs e)
    {
        _single = new Mutex(true, @"Local\ProjetoNotch.SingleInstance", out var first);
        if (!first)
        {
            Shutdown();
            return;
        }

        var br = new CultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentCulture = br;
        CultureInfo.DefaultThreadCurrentUICulture = br;
        Thread.CurrentThread.CurrentCulture = br;
        Thread.CurrentThread.CurrentUICulture = br;

        Paths.Ensure();
        DispatcherUnhandledException += OnUnhandled;
        AppDomain.CurrentDomain.UnhandledException += (_, ev) => Paths.WriteLog("Erro fatal: " + ev.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, ev) => { Paths.WriteLog("Tarefa: " + ev.Exception.Message); ev.SetObserved(); };

        base.OnStartup(e);
        var window = new NotchWindow();
        window.Show();
    }

    private static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Um erro numa ferramenta (ou plugin) não deve derrubar o notch inteiro.
        Paths.WriteLog("Erro: " + e.Exception);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _single?.Dispose();
        base.OnExit(e);
    }
}
