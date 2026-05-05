using LocalPlayer.View.Diagnostics;
using LocalPlayer.View.Primitives;

namespace LocalPlayer.View.Pages.Player;

public partial class ControlBarView : System.Windows.Controls.UserControl
{
    private PerfSpan? _loadedSpan;

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
    }
}
