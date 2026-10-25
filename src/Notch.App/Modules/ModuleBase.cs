using System.Windows;
using System.Windows.Threading;
using Notch.Sdk;

namespace Notch.App.Modules;

/// <summary>Base dos módulos internos: guarda o host e oferece um timer que só roda com a página visível.</summary>
internal abstract class ModuleBase : INotchModule
{
    protected INotchHost Host { get; private set; } = null!;

    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract string TitleAccent { get; }
    public abstract int Order { get; }

    public virtual void Start(INotchHost host) => Host = host;
    public abstract FrameworkElement CreateView();
    public virtual void Stop() { }

    /// <summary>Executa <paramref name="tick"/> periodicamente enquanto <paramref name="view"/> estiver visível.</summary>
    protected static void WhileVisible(FrameworkElement view, TimeSpan interval, Action tick)
    {
        var timer = new DispatcherTimer { Interval = interval };
        timer.Tick += (_, _) => tick();
        view.IsVisibleChanged += (_, _) =>
        {
            if (view.IsVisible) { tick(); timer.Start(); }
            else timer.Stop();
        };
    }

    protected static string Clock(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{(int)t.TotalMinutes}:{t.Seconds:00}";
}
