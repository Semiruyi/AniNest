using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using LocalPlayer.Services;

namespace LocalPlayer;

public partial class App : System.Windows.Application
{
    private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
    private static readonly string StartupLogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [App] {message}{Environment.NewLine}");
        }
        catch { }
    }

    internal static void LogStartup(string message)
    {
        try
        {
            File.AppendAllText(StartupLogFile, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        var sw = Stopwatch.StartNew();
        LogStartup($"=== OnStartup 开始 ===");
        base.OnStartup(e);
        LogStartup($"base.OnStartup 完成，耗时 {sw.ElapsedMilliseconds}ms");
        Log("=== Application Startup ===");

        // 后台预热 LibVLC，将耗时的 native 模块加载提前到应用启动阶段，
        // 从而显著缩短第一次进入播放页时的等待时间。
        Task.Run(() =>
        {
            try
            {
                var libSw = Stopwatch.StartNew();
                MediaPlayerController.Preinitialize();
                LogStartup($"后台 LibVLC 预热完成，耗时 {libSw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                Log($"LibVLC 预热失败: {ex.Message}");
                LogStartup($"后台 LibVLC 预热失败: {ex.Message}");
            }
        });
        LogStartup($"OnStartup 总耗时 {sw.ElapsedMilliseconds}ms");

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
