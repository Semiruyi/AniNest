using System;
using System.Collections.Generic;
using System.Windows;

namespace AniNest.Infrastructure.Presentation;

public sealed class WpfApplicationLifecycle : IApplicationLifecycle
{
    private readonly object _gate = new();
    private readonly Dictionary<EventHandler, ExitEventHandler> _handlerMap = new();

    public event EventHandler? ExitRequested
    {
        add
        {
            if (value == null || Application.Current == null)
                return;

            lock (_gate)
            {
                if (_handlerMap.ContainsKey(value))
                    return;

                ExitEventHandler adapter = (_, _) => value(this, EventArgs.Empty);
                _handlerMap[value] = adapter;
                Application.Current.Exit += adapter;
            }
        }
        remove
        {
            if (value == null || Application.Current == null)
                return;

            lock (_gate)
            {
                if (!_handlerMap.Remove(value, out var adapter))
                    return;

                Application.Current.Exit -= adapter;
            }
        }
    }
}
