using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using LocalPlayer.Features.Shell;
using LocalPlayer.Presentation.Diagnostics;
using LocalPlayer.Presentation.Primitives;

namespace LocalPlayer.View;

public partial class MainWindow : Window
{
    private readonly FpsMonitor _fps;
    private bool _isTrueFullscreen;
    private WindowStyle _savedWindowStyle;
    private WindowState _savedWindowState;
    private bool _savedTopmost;
    private double _savedLeft, _savedTop, _savedWidth, _savedHeight;

    public MainWindow(ShellViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        PopupInputCoordinator.Instance.Attach(this);
        RegisterPopupRegions();
        _fps = new FpsMonitor(this);
        _fps.Attach();

        PageTransition.TransitionCompleted += (_, _) =>
        {
            vm.OnPageTransitionCompleted();
        };

        vm.ToggleFullscreenRequested += OnToggleFullscreenRequested;
    }

    private void OnToggleFullscreenRequested()
    {
        if (!_isTrueFullscreen && TitleBarRow.Height.Value > 0)
        {
            EnterFullscreen();
            ((ShellViewModel)DataContext).SetPlayerFullscreen(true);
        }
        else
        {
            ExitFullscreen();
            ((ShellViewModel)DataContext).SetPlayerFullscreen(false);
        }
    }

    private void RegisterPopupRegions()
    {
        var coordinator = PopupInputCoordinator.Instance;
        coordinator.RegisterRegion(FileButton, PopupHitKind.TitleBarInteractive);
        coordinator.RegisterRegion(SettingsButton, PopupHitKind.TitleBarInteractive);
        coordinator.RegisterRegion(MinimizeButton, PopupHitKind.TitleBarInteractive);
        coordinator.RegisterRegion(MaximizeButton, PopupHitKind.TitleBarInteractive);
        coordinator.RegisterRegion(CloseButton, PopupHitKind.TitleBarInteractive);
        coordinator.RegisterRegion(TitleBarDragZone, PopupHitKind.TitleBarDragZone);
    }

    private void EnterFullscreen()
    {
        var vm = (ShellViewModel)DataContext;
        if (vm.CurrentAnimationCode == "none")
        {
            _savedWindowStyle = WindowStyle;
            _savedWindowState = WindowState;
            _savedTopmost = Topmost;
            _savedLeft = Left;
            _savedTop = Top;
            _savedWidth = Width;
            _savedHeight = Height;
            TitleBarRow.Height = new GridLength(0);
            WindowStyle = WindowStyle.None;
            Topmost = true;

            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
            GetMonitorInfo(monitor, ref mi);
            SetWindowPos(hwnd, HWND_TOPMOST,
                mi.rcMonitor.Left, mi.rcMonitor.Top,
                mi.rcMonitor.Right - mi.rcMonitor.Left, mi.rcMonitor.Bottom - mi.rcMonitor.Top,
                SWP_FRAMECHANGED);
            _isTrueFullscreen = true;
        }
        else
        {
            TitleBarRow.Height = new GridLength(0);
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            ShowWindow(hwnd, SW_MAXIMIZE);
        }
    }

    private void ExitFullscreen()
    {
        if (_isTrueFullscreen)
        {
            Left = _savedLeft;
            Top = _savedTop;
            Width = _savedWidth;
            Height = _savedHeight;
            Topmost = _savedTopmost;
            WindowStyle = _savedWindowStyle;
            WindowState = _savedWindowState;
            TitleBarRow.Height = (GridLength)FindResource("TitleBarRowHeight");
            _isTrueFullscreen = false;
        }
        else
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            ShowWindow(hwnd, SW_RESTORE);
            TitleBarRow.Height = (GridLength)FindResource("TitleBarRowHeight");
        }
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

    // ========== Win32 ==========

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    private static readonly nint HWND_TOPMOST = new(-1);
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const uint SWP_FRAMECHANGED = 0x0020;

    private const int SW_MAXIMIZE = 3;
    private const int SW_MINIMIZE = 6;
    private const int SW_RESTORE = 9;
}



