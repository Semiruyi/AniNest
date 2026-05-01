using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using LocalPlayer.Model;
using LocalPlayer.View;
using LocalPlayer.View.Library;
using LocalPlayer.View.Player;
using LocalPlayer.ViewModel;

namespace LocalPlayer;

public partial class App : System.Windows.Application
{
    private static void Log(string message) => AppLog.Info("App", message);
    private static void LogError(string message, Exception? ex = null) => AppLog.Error("App", message, ex);

    internal static void LogStartup(string message) => AppLog.Info("App", message);

    protected override void OnStartup(StartupEventArgs e)
    {
        var sw = Stopwatch.StartNew();
        LogStartup("=== OnStartup 开始 ===");
        base.OnStartup(e);
        LogStartup($"base.OnStartup 完成，耗时 {sw.ElapsedMilliseconds}ms");
        Log("=== Application Startup ===");

        var services = new ServiceCollection();

        // 单例服务
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IThumbnailGenerator, ThumbnailGenerator>();

        // 每个播放页需要独立的 MediaPlayerController
        services.AddTransient<IMediaPlayerController, MediaPlayerController>();

        // ViewModels
        services.AddTransient<MainPageViewModel>();
        services.AddTransient<PlayerViewModel>();

        // 页面
        services.AddTransient<MainPage>();
        services.AddTransient<PlayerPage>();

        // ShellViewModel + 主窗口（单例）
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<MainWindow>();

        var provider = services.BuildServiceProvider();

        // 后台预热 LibVLC
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

        // 后台初始化缩略图生成器
        var thumbGen = (ThumbnailGenerator)provider.GetRequiredService<IThumbnailGenerator>();
        var settings = provider.GetRequiredService<ISettingsService>();
        Task.Run(() =>
        {
            try
            {
                var thumbSw = Stopwatch.StartNew();
                thumbGen.Initialize(() => settings.GetThumbnailExpiryDays());
                LogStartup($"后台 ThumbnailGenerator 初始化完成，耗时 {thumbSw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                LogError("ThumbnailGenerator 初始化失败", ex);
                LogStartup($"[ERROR] 后台 ThumbnailGenerator 初始化失败: {ex.Message}");
            }
        });

        Exit += (_, _) =>
        {
            Log("Exit 事件触发，正在关闭缩略图生成器...");
            thumbGen.Shutdown();
        };

        LogStartup($"OnStartup DI 配置完成，耗时 {sw.ElapsedMilliseconds}ms");

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

        var mainWindow = provider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}
