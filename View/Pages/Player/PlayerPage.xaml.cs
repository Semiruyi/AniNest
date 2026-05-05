using LocalPlayer.View.Diagnostics;
using LocalPlayer.View.Primitives;
using LocalPlayer.ViewModel.Player;
using System.Windows.Media;

namespace LocalPlayer.View.Pages.Player;

public partial class PlayerPage : System.Windows.Controls.UserControl
{
    private PerfSpan? _loadToFirstRenderSpan;
    private bool _renderedOnce;

    public PlayerPage(PlayerViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        using var loadedSetupSpan = PerfSpan.Begin("PlayerPage.LoadedSetup");
        _loadToFirstRenderSpan?.Dispose();
        _loadToFirstRenderSpan = PerfSpan.Begin("PlayerPage.LoadToFirstRender");
        var coordinator = PopupInputCoordinator.Instance;
        coordinator.RegisterRegion(VideoContainer, PopupHitKind.VideoSurface);
        coordinator.RegisterRegion(PlaylistPanel, PopupHitKind.ControlBarInteractive);
        coordinator.RegisterRegion(PageRoot, PopupHitKind.DismissBackground);

        CompositionTarget.Rendering += OnRendering;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        var coordinator = PopupInputCoordinator.Instance;
        coordinator.UnregisterRegion(VideoContainer, PopupHitKind.VideoSurface);
        coordinator.UnregisterRegion(PlaylistPanel, PopupHitKind.ControlBarInteractive);
        coordinator.UnregisterRegion(PageRoot, PopupHitKind.DismissBackground);
        _loadToFirstRenderSpan?.Dispose();
        _loadToFirstRenderSpan = null;
        _renderedOnce = false;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_renderedOnce)
            return;

        _renderedOnce = true;
        _loadToFirstRenderSpan?.Dispose();
        _loadToFirstRenderSpan = null;
    }
}
