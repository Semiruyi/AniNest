using AniNest.Infrastructure.Diagnostics;
using AniNest.Presentation.Primitives;
using AniNest.Features.Player.Models;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Paths;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Thumbnails;
using AniNest.Infrastructure.Interop;
using AniNest.Presentation.Overlays;
namespace AniNest.Features.Player;

public partial class ControlBarView : System.Windows.Controls.UserControl
{
    private PerfSpan? _loadedSpan;
    private static readonly Logger Log = AppLog.For(nameof(ControlBarView));

    public ControlBarView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _loadedSpan?.Dispose();
        _loadedSpan = PerfSpan.Begin("ControlBar.Loaded");

        var coordinator = PopupInputCoordinator.Instance;
        coordinator.RegisterRegion(PreviousBtn, PopupHitKind.ControlBarInteractive);
        coordinator.RegisterRegion(PlayPauseBtn, PopupHitKind.ControlBarInteractive);
        coordinator.RegisterRegion(NextBtn, PopupHitKind.ControlBarInteractive);
        coordinator.RegisterRegion(StopBtn, PopupHitKind.ControlBarInteractive);
        coordinator.RegisterRegion(SpeedBtn, PopupHitKind.ControlBarInteractive);
        coordinator.RegisterRegion(PlaylistToggleBtn, PopupHitKind.ControlBarInteractive);
        coordinator.RegisterRegion(FullscreenBtn, PopupHitKind.ControlBarInteractive);
        coordinator.RegisterRegion(SeekBar, PopupHitKind.ControlBarGesture);
        coordinator.RegisterRegion(RootGrid, PopupHitKind.DismissBackground);

        var overlayCoordinator = OverlayCoordinator.Instance;
        overlayCoordinator.RegisterRegion(PreviousBtn, OverlayOutsideHitKind.ContentInteractive);
        overlayCoordinator.RegisterRegion(PlayPauseBtn, OverlayOutsideHitKind.ContentInteractive);
        overlayCoordinator.RegisterRegion(NextBtn, OverlayOutsideHitKind.ContentInteractive);
        overlayCoordinator.RegisterRegion(StopBtn, OverlayOutsideHitKind.ContentInteractive);
        overlayCoordinator.RegisterRegion(SpeedBtn, OverlayOutsideHitKind.ContentInteractive);
        overlayCoordinator.RegisterRegion(PlaylistToggleBtn, OverlayOutsideHitKind.ContentInteractive);
        overlayCoordinator.RegisterRegion(FullscreenBtn, OverlayOutsideHitKind.ContentInteractive);
        overlayCoordinator.RegisterRegion(SeekBar, OverlayOutsideHitKind.ContentInteractive);
        overlayCoordinator.RegisterRegion(RootGrid, OverlayOutsideHitKind.ContentBackground);

        SpeedOverlay.Closed += OnSpeedOverlayClosed;
        _loadedSpan?.Dispose();
        _loadedSpan = null;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _loadedSpan?.Dispose();
        _loadedSpan = null;

        var coordinator = PopupInputCoordinator.Instance;
        coordinator.UnregisterRegion(PreviousBtn, PopupHitKind.ControlBarInteractive);
        coordinator.UnregisterRegion(PlayPauseBtn, PopupHitKind.ControlBarInteractive);
        coordinator.UnregisterRegion(NextBtn, PopupHitKind.ControlBarInteractive);
        coordinator.UnregisterRegion(StopBtn, PopupHitKind.ControlBarInteractive);
        coordinator.UnregisterRegion(SpeedBtn, PopupHitKind.ControlBarInteractive);
        coordinator.UnregisterRegion(PlaylistToggleBtn, PopupHitKind.ControlBarInteractive);
        coordinator.UnregisterRegion(FullscreenBtn, PopupHitKind.ControlBarInteractive);
        coordinator.UnregisterRegion(SeekBar, PopupHitKind.ControlBarGesture);
        coordinator.UnregisterRegion(RootGrid, PopupHitKind.DismissBackground);

        SpeedOverlay.Closed -= OnSpeedOverlayClosed;
        SpeedOverlay.Close(OverlayCloseReason.ViewChanged);
    }

    private ControlBarViewModel? ViewModel => DataContext as ControlBarViewModel;

    private void SpeedBtn_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var opened = SpeedOverlay.ToggleForAnchor(SpeedBtn);
        if (ViewModel != null)
            ViewModel.IsSpeedPopupOpen = opened;

        Log.Debug($"SpeedBtn_Click: opened={opened}");
    }

    private void SpeedOption_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        Log.Debug("SpeedOption_Click");
        Dispatcher.BeginInvoke(new Action(() =>
        {
            SpeedOverlay.Close(OverlayCloseReason.Programmatic);
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnSpeedOverlayClosed(object? sender, AnimatedOverlay.OverlayClosedEventArgs e)
    {
        if (ViewModel != null)
            ViewModel.IsSpeedPopupOpen = false;

        Log.Debug($"OnSpeedOverlayClosed: reason={e.Reason}");
    }
}




