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
    private static void Log(string message) => AppLog.Write("crash.log", "App", message);

    internal static void LogStartup(string message) => AppLog.Write("startup.log", "App", message);

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

        // 初始化缩略图生成器（加载索引、检测 ffmpeg、清理残留）
        Task.Run(() =>
        {
            try
            {
                var thumbSw = Stopwatch.StartNew();
                ThumbnailGenerator.Instance.Initialize();
                LogStartup($"后台 ThumbnailGenerator 初始化完成，耗时 {thumbSw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                Log($"ThumbnailGenerator 初始化失败: {ex.Message}");
                LogStartup($"后台 ThumbnailGenerator 初始化失败: {ex.Message}");
            }
        });

        Exit += (s, args) =>
        {
            Log("Exit 事件触发，正在关闭缩略图生成器...");
            ThumbnailGenerator.Instance.Shutdown();
        };

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
