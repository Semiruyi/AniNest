using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Messaging;
using LocalPlayer.Messages;
using LocalPlayer.View.Diagnostics;
using LocalPlayer.ViewModel;

namespace LocalPlayer.View;

public partial class MainWindow : Window
{
    private readonly FpsMonitor _fps;

    public MainWindow(ShellViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        _fps = new FpsMonitor(this);
        _fps.Attach();

        WeakReferenceMessenger.Default.Register<ToggleFullscreenMessage>(this, (_, _) =>
        {
            TitleBarRow.Height = TitleBarRow.Height.Value > 0
                ? new System.Windows.GridLength(0)
                : LayoutConstants.TitleBarRowHeight;
        });
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var preference = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE,
            ref preference, sizeof(uint));
    }

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode)]
    private static extern int DwmSetWindowAttribute(
        nint hwnd, int attr, ref int attrValue, int attrSize);

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        ShowWindow(hwnd, SW_MINIMIZE);
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (WindowState == WindowState.Maximized)
            ShowWindow(hwnd, SW_RESTORE);
        else
            ShowWindow(hwnd, SW_MAXIMIZE);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => SystemCommands.CloseWindow(this);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    private const int SW_MAXIMIZE = 3;
    private const int SW_MINIMIZE = 6;
    private const int SW_RESTORE = 9;
}
