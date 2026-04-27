using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using LocalPlayer.Services;

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

        // 后台预热 LibVLC，将耗时的 native 模块加载提前到应用启动阶段，
        // 从而显著缩短第一次进入播放页时的等待时间。
        Task.Run(() =>
        {
            try
            {
                MediaPlayerController.Preinitialize();
            }
            catch (Exception ex)
            {
                Log($"LibVLC 预热失败: {ex.Message}");
            }
        });

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
