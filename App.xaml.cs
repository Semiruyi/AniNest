using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace LocalPlayer;

public partial class App : System.Windows.Application
{
    private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [App] {message}{Environment.NewLine}");
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Log("=== Application Startup ===");

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            Log($"[FATAL] UnhandledException: {ex?.GetType().Name}: {ex?.Message}");
            Log($"[FATAL] StackTrace: {ex?.StackTrace}");
        };

        DispatcherUnhandledException += (s, args) =>
        {
            Log($"[FATAL] DispatcherUnhandledException: {args.Exception.GetType().Name}: {args.Exception.Message}");
            Log($"[FATAL] StackTrace: {args.Exception.StackTrace}");
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            Log($"[FATAL] UnobservedTaskException: {args.Exception?.Message}");
            args.SetObserved();
        };
    }
}
