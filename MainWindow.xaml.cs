using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using LocalPlayer.Views;

namespace LocalPlayer;

public partial class MainWindow : Window
{
    private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "player.log");

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [MainWindow] {message}{Environment.NewLine}");
        }
        catch { }
    }

    private MainPage? mainPage;
    private PlayerPage? playerPage;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        KeyDown += MainWindow_KeyDown;
        GotKeyboardFocus += MainWindow_GotKeyboardFocus;
        LostKeyboardFocus += MainWindow_LostKeyboardFocus;
    }

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        nint hwnd = new WindowInteropHelper(this).Handle;

        int darkMode = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

        int captionColor = 0x00000000;
        DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));

        ShowMainPage();
    }

    private void ShowMainPage()
    {
        mainPage = new MainPage();
        mainPage.FolderSelected += MainPage_FolderSelected;
        PageHost.Content = mainPage;
        playerPage = null;
    }

    private Task FadeMaskToBlackAsync(int durationMs = 600)
    {
        TransitionMask.Visibility = Visibility.Visible;
        TransitionMask.BeginAnimation(OpacityProperty, null);
        TransitionMask.Opacity = 0;

        var tcs = new TaskCompletionSource<bool>();
        var duration = TimeSpan.FromMilliseconds(durationMs);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var anim = new DoubleAnimation(0, 1, duration) { EasingFunction = ease };
        anim.Completed += (_, _) => tcs.TrySetResult(true);
        TransitionMask.BeginAnimation(OpacityProperty, anim);
        return tcs.Task;
    }

    private async Task FadeMaskFromBlackAsync(int durationMs = 800)
    {
        TransitionMask.Visibility = Visibility.Visible;
        TransitionMask.BeginAnimation(OpacityProperty, null);
        TransitionMask.Opacity = 1;

        var tcs = new TaskCompletionSource<bool>();
        var duration = TimeSpan.FromMilliseconds(durationMs);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var anim = new DoubleAnimation(1, 0, duration) { EasingFunction = ease };
        anim.Completed += (_, _) =>
        {
            TransitionMask.BeginAnimation(OpacityProperty, null);
            TransitionMask.Visibility = Visibility.Collapsed;
            tcs.TrySetResult(true);
        };
        TransitionMask.BeginAnimation(OpacityProperty, anim);
        await tcs.Task;
    }

    private async void MainPage_FolderSelected(object? sender, string folderPath, string folderName)
    {
        await FadeMaskToBlackAsync(600);

        playerPage = new PlayerPage();
        playerPage.BackRequested += PlayerPage_BackRequested;
        playerPage.LoadFolder(folderPath, folderName);
        PageHost.Content = playerPage;

        await Dispatcher.InvokeAsync(() =>
        {
            TransitionMask.Visibility = Visibility.Collapsed;
        }, System.Windows.Threading.DispatcherPriority.Loaded).Task;
    }

    private async void PlayerPage_BackRequested(object? sender, System.EventArgs e)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Log("BackRequested 开始");

        if (playerPage != null)
        {
            await playerPage.FadeToBlackAsync();
        }

        TransitionMask.Visibility = Visibility.Visible;
        TransitionMask.BeginAnimation(OpacityProperty, null);
        TransitionMask.Opacity = 1;

        playerPage?.Dispose();
        Log($"playerPage.Dispose 完成，耗时 {sw.ElapsedMilliseconds}ms");
        ShowMainPage();
        Log($"ShowMainPage 完成，总耗时 {sw.ElapsedMilliseconds}ms");

        await FadeMaskFromBlackAsync(800);
    }

    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        Log($"PreviewKeyDown: Key={e.Key}, OriginalSource={e.OriginalSource?.GetType().Name}, FocusedElement={FocusManager.GetFocusedElement(this)?.GetType().Name}");

        if (playerPage != null && PageHost.Content == playerPage)
        {
            playerPage.HandlePreviewKeyDown(e);
        }
    }

    private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        Log($"KeyDown (冒泡): Key={e.Key}, OriginalSource={e.OriginalSource?.GetType().Name}, FocusedElement={FocusManager.GetFocusedElement(this)?.GetType().Name}");

        if (playerPage != null && PageHost.Content == playerPage)
        {
            playerPage.HandleKeyDown(e);
        }
    }

    private void MainWindow_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        Log($"GotKeyboardFocus: NewFocus={e.NewFocus?.GetType().Name}");
    }

    private void MainWindow_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        Log($"LostKeyboardFocus: NewFocus={e.NewFocus?.GetType().Name}");
    }
}
