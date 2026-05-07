using System.Windows.Media;
using System.Collections.Generic;
using LocalPlayer.Infrastructure.Diagnostics;
using LocalPlayer.Presentation.Primitives;
using LocalPlayer.Features.Player.Models;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Paths;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Thumbnails;
using LocalPlayer.Infrastructure.Interop;
namespace LocalPlayer.Features.Player;

public partial class PlayerPage : System.Windows.Controls.UserControl
{
    private PerfSpan? _loadToFirstRenderSpan;
    private bool _renderedOnce;
    private readonly HashSet<string> _loggedLayoutProbes = new();

    public PlayerPage()
    {
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

        AttachLayoutProbe(PageRoot, "PlayerPage.PageRootLayout");
        AttachLayoutProbe(VideoContainer, "PlayerPage.VideoContainerLayout");
        AttachLayoutProbe(PlaylistPanel, "PlayerPage.PlaylistPanelLayout");
        AttachLayoutProbe(ControlBar, "PlayerPage.ControlBarLayout");
        Dispatcher.BeginInvoke(() =>
        {
            using (PerfSpan.Begin("PlayerPage.PlaylistPanel.UpdateLayout"))
            {
                PlaylistPanel.UpdateLayout();
            }

            using (PerfSpan.Begin("PlayerPage.ControlBar.UpdateLayout"))
            {
                ControlBar.UpdateLayout();
            }
        }, System.Windows.Threading.DispatcherPriority.Loaded);

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
        _loggedLayoutProbes.Clear();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_renderedOnce)
            return;

        _renderedOnce = true;
        _loadToFirstRenderSpan?.Dispose();
        _loadToFirstRenderSpan = null;
    }

    private void AttachLayoutProbe(System.Windows.FrameworkElement element, string spanName)
    {
        if (_loggedLayoutProbes.Contains(spanName))
            return;

        void Handler(object? sender, EventArgs args)
        {
            if (_loggedLayoutProbes.Contains(spanName))
                return;

            _loggedLayoutProbes.Add(spanName);
            element.LayoutUpdated -= Handler;
            using var span = PerfSpan.Begin(spanName, new Dictionary<string, string>
            {
                ["actualWidth"] = element.ActualWidth.ToString("F2"),
                ["actualHeight"] = element.ActualHeight.ToString("F2"),
                ["visibility"] = element.Visibility.ToString(),
                ["isVisible"] = element.IsVisible.ToString()
            });
        }

        element.LayoutUpdated += Handler;
    }
}




