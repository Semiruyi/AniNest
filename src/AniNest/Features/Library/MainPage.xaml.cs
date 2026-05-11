using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AniNest.Features.Library;
using AniNest.Features.Library.Models;
using AniNest.Infrastructure.Diagnostics;
using AniNest.Infrastructure.Logging;
using AniNest.Presentation.Behaviors;
using AniNest.Presentation.Overlays;
namespace AniNest.Features.Library;

public partial class MainPage : System.Windows.Controls.UserControl
{
    private static readonly Logger Log = AppLog.For(nameof(MainPage));
    private PerfSceneSession? _initialLoadScene;
    private MainPageViewModel? _viewModel;
    private bool _initialLoadCompleted;
    private int _renderFramesAfterLoadCompleted;
    private FolderListItem? _overlayItem;

    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
        Initialized += OnInitialized;
    }

    private void OnInitialized(object? sender, EventArgs e)
    {
        CardContextMenuOverlay.Closed += OnCardContextMenuClosed;
        ThumbnailActionsOverlay.Closed += OnThumbnailActionsOverlayClosed;
        RegisterOverlayRegions();
    }

    private void RegisterOverlayRegions()
    {
        var coordinator = OverlayCoordinator.Instance;
        coordinator.RegisterRegion(LibraryScrollViewer, OverlayOutsideHitKind.ContentBackground);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        SyncViewModel();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        SyncViewModel();

        if (_initialLoadScene != null)
            return;

        _initialLoadCompleted = false;
        _renderFramesAfterLoadCompleted = 0;
        _initialLoadScene = PerfScenes.Begin("Library.InitialLoad");

        CompositionTarget.Rendering += OnRendering;

        if (_viewModel != null)
            await _viewModel.LoadDataCommand.ExecuteAsync(null);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        CloseCardContextMenu(OverlayCloseReason.ViewChanged);

        if (_viewModel != null)
        {
            _viewModel.LoadDataCompleted -= OnLoadDataCompleted;
            _viewModel = null;
        }

        CompleteInitialLoadScene();
    }

    private void SyncViewModel()
    {
        var vm = DataContext as MainPageViewModel;
        if (ReferenceEquals(_viewModel, vm))
            return;

        if (_viewModel != null)
            _viewModel.LoadDataCompleted -= OnLoadDataCompleted;

        _viewModel = vm;

        if (_viewModel != null)
            _viewModel.LoadDataCompleted += OnLoadDataCompleted;
    }

    private void OnLoadDataCompleted(object? sender, EventArgs e)
    {
        _initialLoadCompleted = true;
        _renderFramesAfterLoadCompleted = 0;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!_initialLoadCompleted || _initialLoadScene == null)
            return;

        _renderFramesAfterLoadCompleted++;
        if (_renderFramesAfterLoadCompleted >= 2)
            CompleteInitialLoadScene();
    }

    private void CompleteInitialLoadScene()
    {
        if (_initialLoadScene == null)
            return;

        _initialLoadScene.Stop();
        _initialLoadScene = null;
        _initialLoadCompleted = false;
        _renderFramesAfterLoadCompleted = 0;
    }

    private void OnCardPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not FolderListItem item)
            return;

        OverlayCoordinator.Instance.RegisterRegion(border, OverlayOutsideHitKind.ContentInteractive);

        Log.Debug(
            $"OnCardPreviewMouseRightButtonUp: name={item.Name} handled={e.Handled} " +
            $"changedButton={e.ChangedButton} original={DescribeSource(e.OriginalSource as DependencyObject)} " +
            $"overlayOpen={CardContextMenuOverlay.IsOpen} overlayItem={_overlayItem?.Name ?? "null"}");

        MouseGestureBehavior.ResetRightState(border);

        e.Handled = true;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            Log.Debug($"OpenOverlayDispatch: name={item.Name}");
            CardContextMenuOverlay.DataContext = item;
            var opened = CardContextMenuOverlay.ToggleForAnchor(border);
            if (opened)
            {
                _overlayItem = item;
                return;
            }

            CardContextMenuOverlay.DataContext = null;
            _overlayItem = null;
        }), DispatcherPriority.Input);
    }

    private void OnOverlayActionClick(object sender, RoutedEventArgs e)
    {
        Log.Debug(
            $"OnOverlayActionClick: original={DescribeSource(e.OriginalSource as DependencyObject)} " +
            $"item={_overlayItem?.Name ?? "null"}");

        Dispatcher.BeginInvoke(new Action(() =>
        {
            CloseCardContextMenu(OverlayCloseReason.Programmatic);
        }), DispatcherPriority.Background);
    }

    private void OnFavoriteButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not FolderListItem item)
            return;

        string filter = _viewModel?.SelectedFilter.ToString() ?? "null";
        Log.Debug(
            $"OnFavoriteButtonClick: name={item.Name} path={item.Path} isFavoriteBefore={item.IsFavorite} " +
            $"selectedFilter={filter} original={DescribeSource(e.OriginalSource as DependencyObject)} " +
            $"buttonIsMouseOver={button.IsMouseOver} buttonIsPressed={button.IsPressed} " +
            $"buttonVisibility={button.Visibility} buttonOpacity={button.Opacity}");
    }

    private void ThumbnailActionsMenuButton_Click(object sender, RoutedEventArgs e)
    {
        Log.Debug(
            $"ThumbnailActionsMenuButton_Click: item={_overlayItem?.Name ?? "null"} " +
            $"submenuOpen={ThumbnailActionsOverlay.IsOpen}");

        var opened = ThumbnailActionsOverlay.ToggleForAnchor(ThumbnailActionsMenuButton);
        Log.Debug($"ThumbnailActionsMenuButton_Click.Toggle: opened={opened}");
    }

    private void OnLibraryScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (CardContextMenuOverlay.IsOpen)
        {
            Log.Debug(
                $"OnLibraryScrollChanged: h={e.HorizontalOffset:F1} v={e.VerticalOffset:F1} " +
                $"dh={e.HorizontalChange:F1} dv={e.VerticalChange:F1}");
            CloseCardContextMenu(OverlayCloseReason.ChainSwitch);
        }
    }

    private void CloseCardContextMenu(OverlayCloseReason reason)
    {
        CloseCardContextMenuChildOverlays(reason == OverlayCloseReason.Programmatic
            ? OverlayCloseReason.ParentClosed
            : reason);

        if (!CardContextMenuOverlay.IsOpen)
            return;

        Log.Debug($"CloseCardContextMenu: reason={reason}");
        CardContextMenuOverlay.Close(reason);
    }

    private void CloseCardContextMenuChildOverlays(OverlayCloseReason reason)
    {
        if (!ThumbnailActionsOverlay.IsOpen)
            return;

        Log.Debug($"CloseCardContextMenuChildOverlays: reason={reason}");
        ThumbnailActionsOverlay.Close(reason);
    }

    private void OnCardContextMenuClosed(object? sender, AnimatedOverlay.OverlayClosedEventArgs e)
    {
        Log.Debug($"OnCardContextMenuClosed: reason={e.Reason} overlayItem={_overlayItem?.Name ?? "null"}");
        CardContextMenuOverlay.DataContext = null;
        _overlayItem = null;
    }

    private void OnThumbnailActionsOverlayClosed(object? sender, AnimatedOverlay.OverlayClosedEventArgs e)
    {
        Log.Debug($"OnThumbnailActionsOverlayClosed: reason={e.Reason} overlayItem={_overlayItem?.Name ?? "null"}");
    }

    private void OnCardMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not FolderListItem item)
            return;

        Log.Debug(
            $"OnCardMouseEnter: name={item.Name} original={DescribeSource(e.OriginalSource as DependencyObject)} " +
            $"overlayOpen={CardContextMenuOverlay.IsOpen} overlayItem={_overlayItem?.Name ?? "null"}");
    }

    private void OnCardMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not FolderListItem item)
            return;

        var position = e.GetPosition(border);
        Log.Debug(
            $"OnCardMouseLeave: name={item.Name} x={position.X:F1} y={position.Y:F1} " +
            $"inside={position.X >= 0 && position.Y >= 0 && position.X <= border.ActualWidth && position.Y <= border.ActualHeight} " +
            $"original={DescribeSource(e.OriginalSource as DependencyObject)} overlayOpen={CardContextMenuOverlay.IsOpen} " +
            $"overlayItem={_overlayItem?.Name ?? "null"}");
    }

    private static string DescribeSource(DependencyObject? source)
    {
        if (source == null)
            return "null";

        if (source is FrameworkElement frameworkElement)
        {
            var name = string.IsNullOrWhiteSpace(frameworkElement.Name) ? "-" : frameworkElement.Name;
            return $"{frameworkElement.GetType().Name}({name})";
        }

        return source.GetType().Name;
    }
}




