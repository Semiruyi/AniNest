using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using LocalPlayer.Media;
using LocalPlayer.Controls;
using LocalPlayer.Model;

namespace LocalPlayer;

public partial class App : System.Windows.Application
{
    private static void Log(string message) => AppLog.Info("App", message);
    private static void LogError(string message, Exception? ex = null) => AppLog.Error("App", message, ex);

    internal static void LogStartup(string message) => AppLog.Info("App", message);

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
                LogError("LibVLC 预热失败", ex);
                LogStartup($"[ERROR] 后台 LibVLC 预热失败: {ex.Message}");
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
                LogError("ThumbnailGenerator 初始化失败", ex);
                LogStartup($"[ERROR] 后台 ThumbnailGenerator 初始化失败: {ex.Message}");
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
            LogError("未处理异常", ex);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            LogError("Dispatcher未处理异常", args.Exception);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            if (args.Exception != null)
                LogError("未观察到的Task异常", args.Exception);
            args.SetObserved();
        };
    }
}
