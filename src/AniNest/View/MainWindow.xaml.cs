using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using AniNest.Features.Shell;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Persistence;
using AniNest.Presentation.Animations;
using AniNest.Presentation.Diagnostics;
using AniNest.Presentation.Overlays;
using AniNest.Presentation.Primitives;

namespace AniNest.View;

public partial class MainWindow : Window
{
    private readonly FpsMonitor _fps;
    private bool _isTrueFullscreen;
    private WindowStyle _savedWindowStyle;
    private WindowState _savedWindowState;
    private bool _savedTopmost;
    private double _savedLeft, _savedTop, _savedWidth, _savedHeight;
    private CornerRadius _savedCornerRadius;

    private readonly ISettingsService _settingsService;
    private MainWindowTitleBar TitleBar => TitleBarView;

    public MainWindow(ShellViewModel vm, ISettingsService settingsService)
    {
        _settingsService = settingsService;
        DataContext = vm;
        InitializeComponent();
        WireTitleBarEvents();
        PopupInputCoordinator.Instance.Attach(this);
        RegisterPopupRegions();
        FileOverlay.Closed += OnFileOverlayClosed;
        SettingsOverlay.Closed += OnSettingsOverlayClosed;
        LanguageOverlay.Closed += OnLanguageOverlayClosed;
        LanguageOverlay.Opened += OnSelectableOverlayOpened;
        FullscreenAnimationOverlay.Closed += OnFullscreenAnimationOverlayClosed;
        FullscreenAnimationOverlay.Opened += OnSelectableOverlayOpened;
        ThumbnailSettingsOverlay.Closed += OnThumbnailSettingsOverlayClosed;
        ThumbnailPerformanceOverlay.Closed += OnThumbnailPerformanceOverlayClosed;
        ThumbnailPerformanceOverlay.Opened += OnSelectableOverlayOpened;
        ThumbnailAccelerationOverlay.Closed += OnThumbnailAccelerationOverlayClosed;
        ThumbnailAccelerationOverlay.Opened += OnSelectableOverlayOpened;
        PlayerInputOverlay.Closed += OnPlayerInputOverlayClosed;
        _fps = new FpsMonitor(this);
        _fps.Attach();

        PageTransition.TransitionCompleted += (_, _) =>
        {
            vm.OnPageTransitionCompleted();
        };

        vm.ToggleFullscreenRequested += OnToggleFullscreenRequested;
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewMouseDown += OnPreviewMouseDown;
        PreviewMouseWheel += OnPreviewMouseWheel;

        Closing += (_, _) =>
        {
            var settings = _settingsService.Load();
            settings.Window.X = Left;
            settings.Window.Y = Top;
            settings.Window.Width = Width;
            settings.Window.Height = Height;
            settings.Window.Maximized = WindowState == WindowState.Maximized;
        };
    }

    private void WireTitleBarEvents()
    {
        FileButton.Click += FileButton_Click;
        SettingsButton.Click += SettingsButton_Click;
        MinimizeButton.Click += MinimizeButton_Click;
        MaximizeButton.Click += MaximizeButton_Click;
        CloseButton.Click += CloseButton_Click;
        TitleBarFileNameHost.PreviewMouseLeftButtonDown += TitleBarFileNameHost_PreviewMouseLeftButtonDown;
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
        coordinator.RegisterRegion(BackButton, PopupHitKind.TitleBarInteractive);
        coordinator.RegisterRegion(FileButton, PopupHitKind.TitleBarInteractive);
        coordinator.RegisterRegion(SettingsButton, PopupHitKind.TitleBarInteractive);
        coordinator.RegisterRegion(MinimizeButton, PopupHitKind.TitleBarInteractive);
        coordinator.RegisterRegion(MaximizeButton, PopupHitKind.TitleBarInteractive);
        coordinator.RegisterRegion(CloseButton, PopupHitKind.TitleBarInteractive);
        coordinator.RegisterRegion(TitleBarDragZone, PopupHitKind.TitleBarDragZone);

        var overlayCoordinator = OverlayCoordinator.Instance;
        overlayCoordinator.RegisterRegion(BackButton, OverlayOutsideHitKind.TitleBarInteractive);
        overlayCoordinator.RegisterRegion(FileButton, OverlayOutsideHitKind.TitleBarInteractive);
        overlayCoordinator.RegisterRegion(SettingsButton, OverlayOutsideHitKind.TitleBarInteractive);
        overlayCoordinator.RegisterRegion(MinimizeButton, OverlayOutsideHitKind.TitleBarInteractive);
        overlayCoordinator.RegisterRegion(MaximizeButton, OverlayOutsideHitKind.TitleBarInteractive);
        overlayCoordinator.RegisterRegion(CloseButton, OverlayOutsideHitKind.TitleBarInteractive);
        overlayCoordinator.RegisterRegion(TitleBarDragZone, OverlayOutsideHitKind.TitleBarDragZone);
        overlayCoordinator.RegisterRegion(PageTransition, OverlayOutsideHitKind.ContentBackground);
    }

    private void TitleBarFileNameHost_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
            return;

        DragMove();
        e.Handled = true;
    }

    private void EnterFullscreen()
    {
        var vm = (ShellViewModel)DataContext;
        if (vm.CurrentAnimationCode == "none")
        {
            _savedCornerRadius = RootBorder.CornerRadius;
            _savedWindowStyle = WindowStyle;
            _savedWindowState = WindowState;
            _savedTopmost = Topmost;
            _savedLeft = Left;
            _savedTop = Top;
            _savedWidth = Width;
            _savedHeight = Height;
            RootBorder.CornerRadius = default;
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
            ApplyWindowCornerPreference(hwnd, DWMWCP_DONOTROUND);
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
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            Left = _savedLeft;
            Top = _savedTop;
            Width = _savedWidth;
            Height = _savedHeight;
            Topmost = _savedTopmost;
            WindowStyle = _savedWindowStyle;
            WindowState = _savedWindowState;
            RootBorder.CornerRadius = _savedCornerRadius;
            TitleBarRow.Height = (GridLength)FindResource("TitleBarRowHeight");
            ApplyWindowCornerPreference(hwnd, DWMWCP_ROUND);
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
        var hwnd = new WindowInteropHelper(this).Handle;
        var preference = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE,
            ref preference, sizeof(uint));

        RestoreWindowGeometry();
    }

    private void RestoreWindowGeometry()
    {
        var w = _settingsService.Load().Window;
        if (w.Width <= 0)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        if (!IsOnScreen(w.X, w.Y, w.Width, w.Height))
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        Left = w.X;
        Top = w.Y;
        Width = w.Width;
        Height = w.Height;

        if (w.Maximized)
            WindowState = WindowState.Maximized;
    }

    private static bool IsOnScreen(double x, double y, double width, double height)
    {
        var vsLeft = SystemParameters.VirtualScreenLeft;
        var vsTop = SystemParameters.VirtualScreenTop;
        var vsRight = vsLeft + SystemParameters.VirtualScreenWidth;
        var vsBottom = vsTop + SystemParameters.VirtualScreenHeight;

        var windowRight = x + width;
        var windowBottom = y + height;

        return x < vsRight && windowRight > vsLeft &&
               y < vsBottom && windowBottom > vsTop;
    }

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_DEFAULT = 0;
    private const int DWMWCP_ROUND = 2;
    private const int DWMWCP_DONOTROUND = 1;

    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode)]
    private static extern int DwmSetWindowAttribute(
        nint hwnd, int attr, ref int attrValue, int attrSize);

    private static void ApplyWindowCornerPreference(nint hwnd, int preference)
        => DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));

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

    private static readonly Logger MainWindowLog = AppLog.For("MainWindow");
    private ShellViewModel Shell => (ShellViewModel)DataContext;
    private Border TitleBarRoot => TitleBar.TitleBarRootElement;
    private Button FileButton => TitleBar.FileButtonElement;
    private Button BackButton => TitleBar.BackButtonElement;
    private Button SettingsButton => TitleBar.SettingsButtonElement;
    private Grid TitleBarDragZone => TitleBar.TitleBarDragZoneElement;
    private Grid TitleBarFileNameHost => TitleBar.TitleBarFileNameHostElement;
    private Button MinimizeButton => TitleBar.MinimizeButtonElement;
    private Button MaximizeButton => TitleBar.MaximizeButtonElement;
    private Button CloseButton => TitleBar.CloseButtonElement;

    private bool ToggleAnchoredOverlay(
        AnimatedOverlay overlay,
        FrameworkElement anchor,
        Action<ShellViewModel, bool> syncState,
        string logName,
        bool deferReposition = false,
        Action<ShellViewModel>? onOpened = null,
        Action<ShellViewModel>? onClosed = null)
    {
        var opened = overlay.ToggleForAnchor(anchor);
        syncState(Shell, opened);

        if (opened)
            onOpened?.Invoke(Shell);
        else
            onClosed?.Invoke(Shell);

        MainWindowLog.Debug($"{logName}: opened={opened}");

        if (opened && deferReposition)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                overlay.Reposition();
                MainWindowLog.Debug(
                    $"{overlay.Name}.Reposition deferred: anchorWidth={anchor.ActualWidth:F1} " +
                    $"anchorHeight={anchor.ActualHeight:F1} overlayState={overlay.CurrentState}");
            }), DispatcherPriority.Loaded);
        }

        return opened;
    }

    private void CloseOverlay(
        AnimatedOverlay overlay,
        Action<ShellViewModel> syncState,
        OverlayCloseReason reason = OverlayCloseReason.ChainSwitch,
        Action<ShellViewModel>? afterClose = null)
    {
        overlay.Close(reason);
        syncState(Shell);
        afterClose?.Invoke(Shell);
    }

    private void CloseSettingsChildOverlays(OverlayCloseReason reason)
    {
        CloseOverlay(LanguageOverlay, shell => shell.IsLanguageSubmenuOpen = false, reason);
        CloseOverlay(FullscreenAnimationOverlay, shell => shell.IsFullscreenAnimationSubmenuOpen = false, reason);
        CloseOverlay(ThumbnailSettingsOverlay, shell => shell.IsThumbnailSettingsSubmenuOpen = false, reason);
        CloseOverlay(ThumbnailPerformanceOverlay, shell => shell.IsThumbnailPerformanceSubmenuOpen = false, reason);
        CloseOverlay(ThumbnailAccelerationOverlay, shell => shell.IsThumbnailAccelerationSubmenuOpen = false, reason);
        CloseOverlay(
            PlayerInputOverlay,
            shell => shell.IsPlayerInputSubmenuOpen = false,
            reason,
            shell => shell.PlayerInputSettings.CancelCapture());
    }

    private void FileButton_Click(object sender, RoutedEventArgs e)
    {
        MainWindowLog.Debug("FileButton_Click");
        CloseOverlay(SettingsOverlay, shell => shell.IsSettingsPopupOpen = false);
        ToggleAnchoredOverlay(
            FileOverlay,
            FileButton,
            static (shell, opened) => shell.IsFilePopupOpen = opened,
            nameof(FileButton_Click),
            deferReposition: true);
    }

    private void OnFileOverlayActionClick(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            FileOverlay.Close(OverlayCloseReason.Programmatic);
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnFileOverlayClosed(object? sender, AnimatedOverlay.OverlayClosedEventArgs e)
    {
        Shell.IsFilePopupOpen = false;
        MainWindowLog.Debug($"OnFileOverlayClosed: reason={e.Reason}");
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        MainWindowLog.Debug("SettingsButton_Click");
        CloseOverlay(FileOverlay, shell => shell.IsFilePopupOpen = false);
        ToggleAnchoredOverlay(
            SettingsOverlay,
            SettingsButton,
            static (shell, opened) => shell.IsSettingsPopupOpen = opened,
            nameof(SettingsButton_Click),
            deferReposition: true);
    }

    private void OnSettingsOverlayClosed(object? sender, AnimatedOverlay.OverlayClosedEventArgs e)
    {
        Shell.IsSettingsPopupOpen = false;
        CloseSettingsChildOverlays(OverlayCloseReason.ParentClosed);
        MainWindowLog.Debug($"OnSettingsOverlayClosed: reason={e.Reason}");
    }

    private void LanguageMenuButton_Click(object sender, RoutedEventArgs e)
    {
        CloseOverlay(FullscreenAnimationOverlay, shell => shell.IsFullscreenAnimationSubmenuOpen = false);
        CloseOverlay(
            PlayerInputOverlay,
            shell => shell.IsPlayerInputSubmenuOpen = false,
            afterClose: shell => shell.PlayerInputSettings.CancelCapture());
        CloseOverlay(ThumbnailSettingsOverlay, shell => shell.IsThumbnailSettingsSubmenuOpen = false);
        CloseOverlay(ThumbnailPerformanceOverlay, shell => shell.IsThumbnailPerformanceSubmenuOpen = false);
        CloseOverlay(ThumbnailAccelerationOverlay, shell => shell.IsThumbnailAccelerationSubmenuOpen = false);
        ToggleAnchoredOverlay(
            LanguageOverlay,
            LanguageMenuButton,
            static (shell, opened) => shell.IsLanguageSubmenuOpen = opened,
            nameof(LanguageMenuButton_Click));
    }

    private void FullscreenAnimationMenuButton_Click(object sender, RoutedEventArgs e)
    {
        CloseOverlay(LanguageOverlay, shell => shell.IsLanguageSubmenuOpen = false);
        CloseOverlay(ThumbnailSettingsOverlay, shell => shell.IsThumbnailSettingsSubmenuOpen = false);
        CloseOverlay(ThumbnailPerformanceOverlay, shell => shell.IsThumbnailPerformanceSubmenuOpen = false);
        CloseOverlay(ThumbnailAccelerationOverlay, shell => shell.IsThumbnailAccelerationSubmenuOpen = false);
        CloseOverlay(
            PlayerInputOverlay,
            shell => shell.IsPlayerInputSubmenuOpen = false,
            afterClose: shell => shell.PlayerInputSettings.CancelCapture());
        ToggleAnchoredOverlay(
            FullscreenAnimationOverlay,
            FullscreenAnimationMenuButton,
            static (shell, opened) => shell.IsFullscreenAnimationSubmenuOpen = opened,
            nameof(FullscreenAnimationMenuButton_Click));
    }

    private void ThumbnailSettingsMenuButton_Click(object sender, RoutedEventArgs e)
    {
        CloseOverlay(LanguageOverlay, shell => shell.IsLanguageSubmenuOpen = false);
        CloseOverlay(FullscreenAnimationOverlay, shell => shell.IsFullscreenAnimationSubmenuOpen = false);
        CloseOverlay(
            PlayerInputOverlay,
            shell => shell.IsPlayerInputSubmenuOpen = false,
            afterClose: shell => shell.PlayerInputSettings.CancelCapture());
        ToggleAnchoredOverlay(
            ThumbnailSettingsOverlay,
            ThumbnailSettingsMenuButton,
            static (shell, opened) => shell.IsThumbnailSettingsSubmenuOpen = opened,
            nameof(ThumbnailSettingsMenuButton_Click));
    }

    private void ThumbnailPerformanceMenuButton_Click(object sender, RoutedEventArgs e)
    {
        CloseOverlay(LanguageOverlay, shell => shell.IsLanguageSubmenuOpen = false);
        CloseOverlay(FullscreenAnimationOverlay, shell => shell.IsFullscreenAnimationSubmenuOpen = false);
        CloseOverlay(ThumbnailAccelerationOverlay, shell => shell.IsThumbnailAccelerationSubmenuOpen = false);
        CloseOverlay(
            PlayerInputOverlay,
            shell => shell.IsPlayerInputSubmenuOpen = false,
            afterClose: shell => shell.PlayerInputSettings.CancelCapture());
        ToggleAnchoredOverlay(
            ThumbnailPerformanceOverlay,
            ThumbnailPerformanceMenuButton,
            static (shell, opened) => shell.IsThumbnailPerformanceSubmenuOpen = opened,
            nameof(ThumbnailPerformanceMenuButton_Click));
    }

    private void ThumbnailAccelerationMenuButton_Click(object sender, RoutedEventArgs e)
    {
        CloseOverlay(LanguageOverlay, shell => shell.IsLanguageSubmenuOpen = false);
        CloseOverlay(FullscreenAnimationOverlay, shell => shell.IsFullscreenAnimationSubmenuOpen = false);
        CloseOverlay(ThumbnailPerformanceOverlay, shell => shell.IsThumbnailPerformanceSubmenuOpen = false);
        CloseOverlay(
            PlayerInputOverlay,
            shell => shell.IsPlayerInputSubmenuOpen = false,
            afterClose: shell => shell.PlayerInputSettings.CancelCapture());
        ToggleAnchoredOverlay(
            ThumbnailAccelerationOverlay,
            ThumbnailAccelerationMenuButton,
            static (shell, opened) => shell.IsThumbnailAccelerationSubmenuOpen = opened,
            nameof(ThumbnailAccelerationMenuButton_Click));
    }

    private void PlayerInputMenuButton_Click(object sender, RoutedEventArgs e)
    {
        CloseOverlay(LanguageOverlay, shell => shell.IsLanguageSubmenuOpen = false);
        CloseOverlay(FullscreenAnimationOverlay, shell => shell.IsFullscreenAnimationSubmenuOpen = false);
        CloseOverlay(ThumbnailSettingsOverlay, shell => shell.IsThumbnailSettingsSubmenuOpen = false);
        CloseOverlay(ThumbnailPerformanceOverlay, shell => shell.IsThumbnailPerformanceSubmenuOpen = false);
        CloseOverlay(ThumbnailAccelerationOverlay, shell => shell.IsThumbnailAccelerationSubmenuOpen = false);
        ToggleAnchoredOverlay(
            PlayerInputOverlay,
            PlayerInputMenuButton,
            static (shell, opened) => shell.IsPlayerInputSubmenuOpen = opened,
            nameof(PlayerInputMenuButton_Click),
            onOpened: shell => shell.PlayerInputSettings.RefreshFromService(),
            onClosed: shell => shell.PlayerInputSettings.CancelCapture());
    }

    private void OnLanguageOverlayActionClick(object sender, RoutedEventArgs e)
    {
        MainWindowLog.Debug("OnLanguageOverlayActionClick");
    }

    private void OnFullscreenAnimationOverlayActionClick(object sender, RoutedEventArgs e)
    {
        MainWindowLog.Debug("OnFullscreenAnimationOverlayActionClick");
    }

    private void OnLanguageOverlayClosed(object? sender, AnimatedOverlay.OverlayClosedEventArgs e)
    {
        Shell.IsLanguageSubmenuOpen = false;
        MainWindowLog.Debug($"OnLanguageOverlayClosed: reason={e.Reason}");
    }

    private void OnFullscreenAnimationOverlayClosed(object? sender, AnimatedOverlay.OverlayClosedEventArgs e)
    {
        Shell.IsFullscreenAnimationSubmenuOpen = false;
        MainWindowLog.Debug($"OnFullscreenAnimationOverlayClosed: reason={e.Reason}");
    }

    private void OnThumbnailSettingsOverlayClosed(object? sender, AnimatedOverlay.OverlayClosedEventArgs e)
    {
        Shell.IsThumbnailSettingsSubmenuOpen = false;
        CloseOverlay(ThumbnailPerformanceOverlay, shell => shell.IsThumbnailPerformanceSubmenuOpen = false, OverlayCloseReason.ParentClosed);
        CloseOverlay(ThumbnailAccelerationOverlay, shell => shell.IsThumbnailAccelerationSubmenuOpen = false, OverlayCloseReason.ParentClosed);
        MainWindowLog.Debug($"OnThumbnailSettingsOverlayClosed: reason={e.Reason}");
    }

    private void OnThumbnailPerformanceOverlayActionClick(object sender, RoutedEventArgs e)
    {
        MainWindowLog.Debug("OnThumbnailPerformanceOverlayActionClick");
    }

    private void OnThumbnailPerformanceOverlayClosed(object? sender, AnimatedOverlay.OverlayClosedEventArgs e)
    {
        Shell.IsThumbnailPerformanceSubmenuOpen = false;
        MainWindowLog.Debug($"OnThumbnailPerformanceOverlayClosed: reason={e.Reason}");
    }

    private void OnThumbnailAccelerationOverlayActionClick(object sender, RoutedEventArgs e)
    {
        MainWindowLog.Debug("OnThumbnailAccelerationOverlayActionClick");
    }

    private void OnThumbnailAccelerationOverlayClosed(object? sender, AnimatedOverlay.OverlayClosedEventArgs e)
    {
        Shell.IsThumbnailAccelerationSubmenuOpen = false;
        MainWindowLog.Debug($"OnThumbnailAccelerationOverlayClosed: reason={e.Reason}");
    }

    private void OnSelectableOverlayOpened(object? sender, EventArgs e)
    {
        if (sender is not AnimatedOverlay overlay)
            return;

        SelectionHighlightAnimation.InvalidateDescendants(overlay);
        Dispatcher.BeginInvoke(
            new Action(() => SelectionHighlightAnimation.InvalidateDescendants(overlay)),
            DispatcherPriority.Loaded);
        MainWindowLog.Debug($"OnSelectableOverlayOpened: overlay={overlay.Name}");
    }

    private void OnPlayerInputOverlayClosed(object? sender, AnimatedOverlay.OverlayClosedEventArgs e)
    {
        Shell.IsPlayerInputSubmenuOpen = false;
        Shell.PlayerInputSettings.CancelCapture();
        MainWindowLog.Debug($"OnPlayerInputOverlayClosed: reason={e.Reason}");
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        MainWindowLog.Debug($"PreviewKeyDown: Key={e.Key} SystemKey={e.SystemKey} IsRepeat={e.IsRepeat} Handled={e.Handled}");
        if (Shell.TryCaptureSettingsKey(e))
        {
            MainWindowLog.Info($"PreviewKeyDown captured by settings: Key={e.Key}");
            e.Handled = true;
            return;
        }

        Shell.TryHandlePlayerKeyDown(e);
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Shell.TryCaptureSettingsMouseDown(e))
            e.Handled = true;
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Shell.TryCaptureSettingsMouseWheel(e))
            e.Handled = true;
    }

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



