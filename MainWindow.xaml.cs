using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
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

    private void MainPage_FolderSelected(object? sender, string folderPath, string folderName)
    {
        playerPage = new PlayerPage();
        playerPage.BackRequested += PlayerPage_BackRequested;
        playerPage.LoadFolder(folderPath, folderName);
        PageHost.Content = playerPage;
    }

    private void PlayerPage_BackRequested(object? sender, System.EventArgs e)
    {
        playerPage?.Dispose();
        ShowMainPage();
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
