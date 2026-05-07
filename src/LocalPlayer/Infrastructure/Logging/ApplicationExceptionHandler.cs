using System;
using System.Threading.Tasks;
using System.Windows;

namespace AniNest.Infrastructure.Logging;

public static class ApplicationExceptionHandler
{
    private static readonly Logger Log = AppLog.For("App");

    public static void Configure(Application app)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Error("Unhandled exception", args.ExceptionObject as Exception);
        };

        app.DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("Dispatcher unhandled exception", args.Exception);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            if (args.Exception != null)
                Log.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };
    }
}
