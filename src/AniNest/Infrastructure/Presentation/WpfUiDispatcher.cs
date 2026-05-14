using System;
using System.Windows;

namespace AniNest.Infrastructure.Presentation;

public sealed class WpfUiDispatcher : IUiDispatcher
{
    public bool CheckAccess()
        => Application.Current?.Dispatcher?.CheckAccess() ?? true;

    public void Invoke(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    public void BeginInvoke(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }
}
