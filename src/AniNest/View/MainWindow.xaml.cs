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
    private enum SettingsOverlayId
    {
        Language,
        FullscreenAnimation,
        ThumbnailSettings,
        ThumbnailPerformance,
        ThumbnailAcceleration,
        PlayerInput
    }

    private sealed class SettingsOverlayRegistration
    {
        public required SettingsOverlayId Id { get; init; }
        public required AnimatedOverlay Overlay { get; init; }
        public required Func<FrameworkElement> Anchor { get; init; }
        public required Action<ShellViewModel, bool> SyncState { get; init; }
        public required string LogName { get; init; }
        public SettingsOverlayId? ParentId { get; init; }
        public SelectableOptionGroup? HighlightGroup { get; init; }
        public Action<ShellViewModel>? OnOpened { get; init; }
        public Action<ShellViewModel>? OnToggleClosed { get; init; }
        public Action<ShellViewModel>? OnCloseRequested { get; init; }
        public Action<ShellViewModel>? OnClosed { get; init; }
    }

    private readonly FpsMonitor _fps;
    private readonly Dictionary<SettingsOverlayId, SettingsOverlayRegistration> _settingsOverlays = [];
    private readonly Dictionary<AnimatedOverlay, SelectableOptionGroup> _selectableOverlayHighlights = [];
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
        InitializeSettingsOverlays();
        WireTitleBarEvents();
        PopupInputCoordinator.Instance.Attach(this);
        RegisterPopupRegions();
        FileOverlay.Closed += OnFileOverlayClosed;
        SettingsOverlay.Closed += OnSettingsOverlayClosed;
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

    private void InitializeSettingsOverlays()
    {
        RegisterSettingsOverlay(new SettingsOverlayRegistration
        {
            Id = SettingsOverlayId.Language,
            Overlay = LanguageOverlay,
            Anchor = () => LanguageMenuButton,
            SyncState = static (shell, opened) => shell.IsLanguageSubmenuOpen = opened,
            LogName = nameof(LanguageMenuButton_Click),
            HighlightGroup = LanguageOptionGroup
        });

        RegisterSettingsOverlay(new SettingsOverlayRegistration
        {
            Id = SettingsOverlayId.FullscreenAnimation,
            Overlay = FullscreenAnimationOverlay,
            Anchor = () => FullscreenAnimationMenuButton,
            SyncState = static (shell, opened) => shell.IsFullscreenAnimationSubmenuOpen = opened,
            LogName = nameof(FullscreenAnimationMenuButton_Click),
            HighlightGroup = FullscreenAnimationOptionGroup
        });

        RegisterSettingsOverlay(new SettingsOverlayRegistration
        {
            Id = SettingsOverlayId.ThumbnailSettings,
            Overlay = ThumbnailSettingsOverlay,
            Anchor = () => ThumbnailSettingsMenuButton,
            SyncState = static (shell, opened) => shell.IsThumbnailSettingsSubmenuOpen = opened,
            LogName = nameof(ThumbnailSettingsMenuButton_Click)
        });

        RegisterSettingsOverlay(new SettingsOverlayRegistration
        {
            Id = SettingsOverlayId.ThumbnailPerformance,
            Overlay = ThumbnailPerformanceOverlay,
            Anchor = () => ThumbnailPerformanceMenuButton,
            SyncState = static (shell, opened) => shell.IsThumbnailPerformanceSubmenuOpen = opened,
            LogName = nameof(ThumbnailPerformanceMenuButton_Click),
            ParentId = SettingsOverlayId.ThumbnailSettings,
            HighlightGroup = ThumbnailPerformanceOptionGroup
        });

        RegisterSettingsOverlay(new SettingsOverlayRegistration
        {
            Id = SettingsOverlayId.ThumbnailAcceleration,
            Overlay = ThumbnailAccelerationOverlay,
            Anchor = () => ThumbnailAccelerationMenuButton,
            SyncState = static (shell, opened) => shell.IsThumbnailAccelerationSubmenuOpen = opened,
            LogName = nameof(ThumbnailAccelerationMenuButton_Click),
            ParentId = SettingsOverlayId.ThumbnailSettings,
            HighlightGroup = ThumbnailAccelerationOptionGroup
        });

        RegisterSettingsOverlay(new SettingsOverlayRegistration
        {
            Id = SettingsOverlayId.PlayerInput,
            Overlay = PlayerInputOverlay,
            Anchor = () => PlayerInputMenuButton,
            SyncState = static (shell, opened) => shell.IsPlayerInputSubmenuOpen = opened,
            LogName = nameof(PlayerInputMenuButton_Click),
            OnOpened = shell => shell.PlayerInputSettings.RefreshFromService(),
            OnToggleClosed = shell => shell.PlayerInputSettings.CancelCapture(),
            OnCloseRequested = shell => shell.PlayerInputSettings.CancelCapture(),
            OnClosed = shell => shell.PlayerInputSettings.CancelCapture()
        });
    }

    private void RegisterSettingsOverlay(SettingsOverlayRegistration registration)
    {
        _settingsOverlays.Add(registration.Id, registration);
        registration.Overlay.Closed += (_, e) => OnSettingsChildOverlayClosed(registration, e);

        if (registration.HighlightGroup == null)
            return;

        registration.HighlightGroup.IsSelectionHighlightActive = false;
        _selectableOverlayHighlights.Add(registration.Overlay, registration.HighlightGroup);
        registration.Overlay.Opening += OnSelectableOverlayOpening;
        registration.Overlay.Opened += OnSelectableOverlayOpened;
        registration.Overlay.Closing += OnSelectableOverlayClosing;
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

        var overlayCoordinator = OverlayCoordinator.Instance;
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
        SetSelectableHighlightActivation(overlay, false);
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
        foreach (var registration in _settingsOverlays.Values)
            CloseSettingsOverlay(registration.Id, reason);
    }

    private void CloseSettingsOverlay(SettingsOverlayId id, OverlayCloseReason reason)
    {
        if (!TryGetSettingsOverlayRegistration(id, out var registration))
            return;

        CloseOverlay(
            registration.Overlay,
            shell => registration.SyncState(shell, false),
            reason,
            registration.OnCloseRequested);
    }

    private void CloseSettingsOverlayBranchesExcept(SettingsOverlayId id, OverlayCloseReason reason)
    {
        var branch = GetSettingsOverlayBranch(id);
        foreach (var registration in _settingsOverlays.Values)
        {
            if (branch.Contains(registration.Id))
                continue;

            CloseSettingsOverlay(registration.Id, reason);
        }
    }

    private HashSet<SettingsOverlayId> GetSettingsOverlayBranch(SettingsOverlayId id)
    {
        var branch = new HashSet<SettingsOverlayId>();
        SettingsOverlayId? current = id;
        while (current.HasValue)
        {
            branch.Add(current.Value);
            if (!TryGetSettingsOverlayRegistration(current.Value, out var registration))
                break;

            current = registration.ParentId;
        }

        return branch;
    }

    private void ToggleSettingsOverlay(SettingsOverlayId id)
    {
        CloseSettingsOverlayBranchesExcept(id, OverlayCloseReason.ChainSwitch);

        if (!TryGetSettingsOverlayRegistration(id, out var registration))
            return;

        ToggleAnchoredOverlay(
            registration.Overlay,
            registration.Anchor(),
            registration.SyncState,
            registration.LogName,
            onOpened: registration.OnOpened,
            onClosed: registration.OnToggleClosed);
    }

    private void OnSettingsChildOverlayClosed(
        SettingsOverlayRegistration registration,
        AnimatedOverlay.OverlayClosedEventArgs e)
    {
        registration.SyncState(Shell, false);
        registration.OnClosed?.Invoke(Shell);

        foreach (var child in _settingsOverlays.Values)
        {
            if (child.ParentId != registration.Id)
                continue;

            CloseSettingsOverlay(child.Id, OverlayCloseReason.ParentClosed);
        }

        MainWindowLog.Debug($"On{registration.Overlay.Name}Closed: reason={e.Reason}");
    }

    private void FileButton_Click(object sender, RoutedEventArgs e)
    {
        MainWindowLog.Debug("FileButton_Click");
        var shell = (ShellViewModel)DataContext;
        if (shell.IsOnPlayerPage)
        {
            shell.GoBackFromPlayerTitleBarCommand.Execute(null);
            return;
        }

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
        => ToggleSettingsOverlay(SettingsOverlayId.Language);

    private void FullscreenAnimationMenuButton_Click(object sender, RoutedEventArgs e)
        => ToggleSettingsOverlay(SettingsOverlayId.FullscreenAnimation);

    private void ThumbnailSettingsMenuButton_Click(object sender, RoutedEventArgs e)
        => ToggleSettingsOverlay(SettingsOverlayId.ThumbnailSettings);

    private void ThumbnailPerformanceMenuButton_Click(object sender, RoutedEventArgs e)
        => ToggleSettingsOverlay(SettingsOverlayId.ThumbnailPerformance);

    private void ThumbnailAccelerationMenuButton_Click(object sender, RoutedEventArgs e)
        => ToggleSettingsOverlay(SettingsOverlayId.ThumbnailAcceleration);

    private void PlayerInputMenuButton_Click(object sender, RoutedEventArgs e)
        => ToggleSettingsOverlay(SettingsOverlayId.PlayerInput);

    private void OnSelectableOverlayOpened(object? sender, EventArgs e)
    {
        if (sender is not AnimatedOverlay overlay ||
            !_selectableOverlayHighlights.TryGetValue(overlay, out var highlightGroup))
            return;

        highlightGroup.IsSelectionHighlightActive = true;
        SelectionHighlightAnimation.InvalidateDescendants(overlay);
        Dispatcher.BeginInvoke(
            new Action(() => SelectionHighlightAnimation.InvalidateDescendants(overlay)),
            DispatcherPriority.Loaded);
    }

    private void OnSelectableOverlayOpening(object? sender, EventArgs e)
    {
        SetSelectableHighlightActivation(sender, false);
    }

    private void OnSelectableOverlayClosing(object? sender, EventArgs e)
    {
        SetSelectableHighlightActivation(sender, false);
    }

    private void SetSelectableHighlightActivation(object? sender, bool isActive)
    {
        if (sender is AnimatedOverlay overlay &&
            _selectableOverlayHighlights.TryGetValue(overlay, out var highlightGroup))
        {
            highlightGroup.IsSelectionHighlightActive = isActive;
        }
    }

    private bool TryGetSettingsOverlayRegistration(
        SettingsOverlayId id,
        out SettingsOverlayRegistration registration)
    {
        if (_settingsOverlays.TryGetValue(id, out registration!))
            return true;

        MainWindowLog.Warning($"Missing settings overlay registration: id={id}");
        return false;
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



