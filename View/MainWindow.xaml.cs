using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using LocalPlayer.Messages;
using LocalPlayer.Model;
using LocalPlayer.View.Animations;
using LocalPlayer.View.Library;
using LocalPlayer.View.Player;
using LocalPlayer.ViewModel;

namespace LocalPlayer.View;

public partial class MainWindow : Window
{
    private static void Log(string message) => AppLog.Info(nameof(MainWindow), message);
    private static void LogError(string message, Exception? ex = null) => AppLog.Error(nameof(MainWindow), message, ex);

    private readonly IServiceProvider _services;
    private MainPage? mainPage;
    private PlayerPage? playerPage;

    public MainWindow(IServiceProvider services)
    {
        _services = services;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        App.LogStartup("MainWindow 构造函数开始");
        InitializeComponent();
        App.LogStartup($"MainWindow.InitializeComponent 完成，耗时 {sw.ElapsedMilliseconds}ms");

        WeakReferenceMessenger.Default.Register<FolderSelectedMessage>(this, (_, m) =>
            _ = MainPage_FolderSelected(m));

        Loaded += MainWindow_Loaded;
        App.LogStartup($"MainWindow 构造函数完成，总耗时 {sw.ElapsedMilliseconds}ms");
    }

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        App.LogStartup("MainWindow.Loaded 事件触发");
        nint hwnd = new WindowInteropHelper(this).Handle;

        int darkMode = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

        int captionColor = 0x00000000;
        DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
        App.LogStartup($"DWM 属性设置完成，耗时 {sw.ElapsedMilliseconds}ms");

        ShowMainPage();
        App.LogStartup($"MainWindow.Loaded 总耗时 {sw.ElapsedMilliseconds}ms");
    }

    private void ShowMainPage()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        App.LogStartup("ShowMainPage 开始");
        mainPage = _services.GetRequiredService<MainPage>();
        App.LogStartup($"MainPage 构造函数完成，耗时 {sw.ElapsedMilliseconds}ms");
        PageHost.Content = mainPage;
        playerPage = null;
        App.LogStartup($"ShowMainPage 完成，总耗时 {sw.ElapsedMilliseconds}ms");
    }

    private Task FadeMaskToBlackAsync(int durationMs = 300)
        => AnimationHelper.FadeInAsync(TransitionMask, durationMs);

    private async Task FadeMaskFromBlackAsync(int durationMs = 350)
    {
        TransitionMask.Visibility = Visibility.Visible;
        TransitionMask.Opacity = 1;
        await AnimationHelper.FadeOutAsync(TransitionMask, durationMs,
            onCompleted: () => TransitionMask.Visibility = Visibility.Collapsed);
    }

    private async Task MainPage_FolderSelected(FolderSelectedMessage m)
    {
        await FadeMaskToBlackAsync(300);

        playerPage = _services.GetRequiredService<PlayerPage>();
        var playerVm = (PlayerViewModel)playerPage.DataContext;
        playerVm.BackRequested += PlayerPage_BackRequested;
        playerPage.LoadFolder(m.Path, m.Name);
        PageHost.Content = playerPage;

        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);

        await FadeMaskFromBlackAsync(350);
    }

    private async void PlayerPage_BackRequested()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Log("BackRequested 开始");

        if (playerPage != null)
        {
            await playerPage.FadeOutUIAsync();
        }

        TransitionMask.Visibility = Visibility.Visible;
        TransitionMask.BeginAnimation(OpacityProperty, null);
        TransitionMask.Opacity = 1;

        playerPage?.Cleanup();
        Log($"playerPage.Dispose 完成，耗时 {sw.ElapsedMilliseconds}ms");
        ShowMainPage();
        Log($"ShowMainPage 完成，总耗时 {sw.ElapsedMilliseconds}ms");

        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);

        await FadeMaskFromBlackAsync(350);
    }
}
