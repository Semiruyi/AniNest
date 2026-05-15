using System.Windows.Media;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using AniNest.Infrastructure.Diagnostics;
using AniNest.Infrastructure.Media;
using AniNest.Presentation.Primitives;
using AniNest.Features.Player.Models;
using System.Windows.Input;
using System.Windows.Threading;
namespace AniNest.Features.Player;

public partial class PlayerPage : System.Windows.Controls.UserControl
{
    private const int VideoCursorHideDelayMs = 1500;
    private PerfSpan? _loadToFirstRenderSpan;
    private bool _renderedOnce;
    private readonly HashSet<string> _loggedLayoutProbes = new();
    private readonly DispatcherTimer _videoCursorHideTimer;
    private readonly IWpfVideoSurfaceSource _videoSurfaceSource;
    private PlayerViewModel? _playerViewModel;
    private PropertyChangedEventHandler? _playerViewModelPropertyChangedHandler;
    private PropertyChangedEventHandler? _videoSurfacePropertyChangedHandler;

    public PlayerPage()
    {
        _videoSurfaceSource = App.Services.GetRequiredService<IWpfVideoSurfaceSource>();
        InitializeComponent();
        _videoCursorHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(VideoCursorHideDelayMs) };
        _videoCursorHideTimer.Tick += OnVideoCursorHideTimerTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        using var loadedSetupSpan = PerfSpan.Begin("PlayerPage.LoadedSetup");
        _loadToFirstRenderSpan?.Dispose();
        _loadToFirstRenderSpan = PerfSpan.Begin("PlayerPage.LoadToFirstRender");

        HookVideoCursorAutoHide();
        var coordinator = PopupInputCoordinator.Instance;
        coordinator.RegisterRegion(VideoContainer, PopupHitKind.VideoSurface);

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

        HookVideoSurface();
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        UnhookVideoSurface();
        UnhookVideoCursorAutoHide();
        var coordinator = PopupInputCoordinator.Instance;
        coordinator.UnregisterRegion(VideoContainer, PopupHitKind.VideoSurface);
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

    private void HookVideoCursorAutoHide()
    {
        VideoContainer.MouseMove -= OnVideoContainerMouseMove;
        VideoContainer.MouseLeave -= OnVideoContainerMouseLeave;
        VideoContainer.MouseMove += OnVideoContainerMouseMove;
        VideoContainer.MouseLeave += OnVideoContainerMouseLeave;

        _playerViewModelPropertyChangedHandler ??= OnPlayerViewModelPropertyChanged;
        if (DataContext is PlayerViewModel viewModel && !ReferenceEquals(_playerViewModel, viewModel))
        {
            UnhookPlayerViewModel();
            _playerViewModel = viewModel;
            _playerViewModel.PropertyChanged += _playerViewModelPropertyChangedHandler;
            _playerViewModel.MediaReady += OnPlayerMediaReady;
        }

        UpdateVideoCursorState();
    }

    private void UnhookVideoCursorAutoHide()
    {
        VideoContainer.MouseMove -= OnVideoContainerMouseMove;
        VideoContainer.MouseLeave -= OnVideoContainerMouseLeave;
        _videoCursorHideTimer.Stop();
        VideoContainer.Cursor = null;
        UnhookPlayerViewModel();
    }

    private void UnhookPlayerViewModel()
    {
        if (_playerViewModel is null || _playerViewModelPropertyChangedHandler is null)
            return;

        _playerViewModel.PropertyChanged -= _playerViewModelPropertyChangedHandler;
        _playerViewModel.MediaReady -= OnPlayerMediaReady;
        _playerViewModel = null;
    }

    private void HookVideoSurface()
    {
        _videoSurfacePropertyChangedHandler ??= OnVideoSurfacePropertyChanged;
        _videoSurfaceSource.PropertyChanged += _videoSurfacePropertyChangedHandler;
        RefreshVideoSurface();
    }

    private void UnhookVideoSurface()
    {
        if (_videoSurfacePropertyChangedHandler is null)
            return;

        _videoSurfaceSource.PropertyChanged -= _videoSurfacePropertyChangedHandler;
    }

    private void OnVideoSurfacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IWpfVideoSurfaceSource.CurrentFrame))
            RefreshVideoSurface();
    }

    private void OnPlayerMediaReady()
        => Dispatcher.BeginInvoke(RefreshVideoSurface, DispatcherPriority.Background);

    private void RefreshVideoSurface()
        => VideoImage.Source = _videoSurfaceSource.CurrentFrame;

    private void OnPlayerViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerViewModel.IsPlaying))
            Dispatcher.BeginInvoke(UpdateVideoCursorState, DispatcherPriority.Background);
    }

    private void OnVideoContainerMouseMove(object sender, MouseEventArgs e)
    {
        if (!ShouldAutoHideVideoCursor())
            return;

        ShowVideoCursor();
        RestartVideoCursorHideTimer();
    }

    private void OnVideoContainerMouseLeave(object sender, MouseEventArgs e)
    {
        _videoCursorHideTimer.Stop();
        ShowVideoCursor();
    }

    private void OnVideoCursorHideTimerTick(object? sender, EventArgs e)
    {
        _videoCursorHideTimer.Stop();
        HideVideoCursor();
    }

    private void RestartVideoCursorHideTimer()
    {
        _videoCursorHideTimer.Stop();
        _videoCursorHideTimer.Start();
    }

    private void UpdateVideoCursorState()
    {
        if (!ShouldAutoHideVideoCursor())
        {
            _videoCursorHideTimer.Stop();
            ShowVideoCursor();
            return;
        }

        if (VideoContainer.IsMouseOver)
        {
            ShowVideoCursor();
            RestartVideoCursorHideTimer();
        }
    }

    private bool ShouldAutoHideVideoCursor()
        => _playerViewModel?.IsPlaying == true;

    private void HideVideoCursor()
    {
        if (ShouldAutoHideVideoCursor() && VideoContainer.IsMouseOver)
            VideoContainer.Cursor = Cursors.None;
    }

    private void ShowVideoCursor()
    {
        VideoContainer.Cursor = null;
    }
}




