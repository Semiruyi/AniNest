using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading.Tasks;
using AniNest.Features.Library;
using AniNest.Features.Library.Models;
using AniNest.Infrastructure.Diagnostics;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Persistence;
using AniNest.Presentation.Animations;
using AniNest.Presentation.Behaviors;
using AniNest.Presentation.Overlays;
using Point = System.Windows.Point;
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
        CardStatusMenuOverlay.Closed += OnCardStatusMenuClosed;
        CardStatusMenuOverlay.Opening += OnCardStatusMenuOpening;
        CardStatusMenuOverlay.Opened += OnCardStatusMenuOpened;
        CardStatusMenuOverlay.Closing += OnCardStatusMenuClosing;
        CardContextMenuOverlay.Closed += OnCardContextMenuClosed;
        ThumbnailActionsOverlay.Closed += OnThumbnailActionsOverlayClosed;
        CardStatusOptionGroup.IsSelectionHighlightActive = false;
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
        if (CardStatusMenuOverlay.IsOpen)
            CardStatusMenuOverlay.Close(OverlayCloseReason.ChainSwitch);

        if (ThumbnailActionsOverlay.IsOpen)
            ThumbnailActionsOverlay.Close(OverlayCloseReason.ChainSwitch);

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

    private void ThumbnailActionsMenuButton_Click(object sender, RoutedEventArgs e)
    {
        Log.Debug(
            $"ThumbnailActionsMenuButton_Click: item={_overlayItem?.Name ?? "null"} " +
            $"submenuOpen={ThumbnailActionsOverlay.IsOpen}");

        if (CardStatusMenuOverlay.IsOpen)
            CardStatusMenuOverlay.Close(OverlayCloseReason.ChainSwitch);

        var opened = ThumbnailActionsOverlay.ToggleForAnchor(ThumbnailActionsMenuButton);
        Log.Debug($"ThumbnailActionsMenuButton_Click.Toggle: opened={opened}");
    }

    private void CardStatusButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not FolderListItem item)
            return;

        OverlayCoordinator.Instance.RegisterRegion(button, OverlayOutsideHitKind.ContentInteractive);
        if (CardContextMenuOverlay.IsOpen || ThumbnailActionsOverlay.IsOpen)
            CloseCardContextMenu(OverlayCloseReason.ChainSwitch);

        Log.Debug(
            $"CardStatusButton_Click: name={item.Name} status={item.Status} " +
            $"overlayOpen={CardStatusMenuOverlay.IsOpen} overlayItem={_overlayItem?.Name ?? "null"}");

        Dispatcher.BeginInvoke(new Action(() => OpenCardStatusMenu(button, item, openAsSubmenu: false)), DispatcherPriority.Input);
    }

    private void CardStatusMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not FolderListItem item)
            return;

        OverlayCoordinator.Instance.RegisterRegion(button, OverlayOutsideHitKind.ContentInteractive);
        if (ThumbnailActionsOverlay.IsOpen)
            ThumbnailActionsOverlay.Close(OverlayCloseReason.ChainSwitch);

        Log.Debug(
            $"CardStatusMenuButton_Click: name={item.Name} status={item.Status} " +
            $"overlayOpen={CardStatusMenuOverlay.IsOpen} overlayItem={_overlayItem?.Name ?? "null"}");

        Dispatcher.BeginInvoke(new Action(() => OpenCardStatusMenu(button, item, openAsSubmenu: true)), DispatcherPriority.Input);
    }

    private async void MarkWatchingMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteFolderStatusChangeAsync(WatchStatus.Watching);
    }

    private async void MarkUnsortedMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteFolderStatusChangeAsync(WatchStatus.Unsorted);
    }

    private async void MarkCompletedMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteFolderStatusChangeAsync(WatchStatus.Completed);
    }

    private async void MarkDroppedMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteFolderStatusChangeAsync(WatchStatus.Dropped);
    }

    private void OnLibraryScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (CardContextMenuOverlay.IsOpen || CardStatusMenuOverlay.IsOpen)
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
        if (CardStatusMenuOverlay.IsOpen)
        {
            Log.Debug($"CloseCardStatusMenu: reason={reason}");
            CardStatusMenuOverlay.Close(reason);
        }

        if (!ThumbnailActionsOverlay.IsOpen)
            return;

        Log.Debug($"CloseCardContextMenuChildOverlays: reason={reason}");
        ThumbnailActionsOverlay.Close(reason);
    }

    private void OnCardStatusMenuClosed(object? sender, AnimatedOverlay.OverlayClosedEventArgs e)
    {
        Log.Debug($"OnCardStatusMenuClosed: reason={e.Reason} overlayItem={_overlayItem?.Name ?? "null"}");
        CardStatusOptionGroup.IsSelectionHighlightActive = false;
        CardStatusMenuOverlay.DataContext = null;
        _overlayItem = null;
    }

    private void OnCardStatusMenuOpening(object? sender, EventArgs e)
    {
        CardStatusOptionGroup.IsSelectionHighlightActive = false;
    }

    private void OnCardStatusMenuOpened(object? sender, EventArgs e)
    {
        CardStatusOptionGroup.IsSelectionHighlightActive = true;
        SelectionHighlightAnimation.InvalidateDescendants(CardStatusOptionGroup);
        Dispatcher.BeginInvoke(
            new Action(() => SelectionHighlightAnimation.InvalidateDescendants(CardStatusOptionGroup)),
            DispatcherPriority.Loaded);
    }

    private void OnCardStatusMenuClosing(object? sender, EventArgs e)
    {
        CardStatusOptionGroup.IsSelectionHighlightActive = false;
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

    private async Task ExecuteFolderStatusChangeAsync(WatchStatus status)
    {
        var item = _overlayItem;
        if (item == null || _viewModel == null)
            return;

        Log.Debug($"ExecuteFolderStatusChangeAsync: name={item.Name} status={status}");
        await _viewModel.SetFolderWatchStatusAsync(item, status);
    }

    private void OpenCardStatusMenu(FrameworkElement anchor, FolderListItem item, bool openAsSubmenu)
    {
        ConfigureCardStatusMenuOverlay(openAsSubmenu);
        CardStatusMenuOverlay.DataContext = item;
        var opened = CardStatusMenuOverlay.ToggleForAnchor(anchor);
        if (opened)
        {
            _overlayItem = item;
            return;
        }

        CardStatusMenuOverlay.DataContext = null;
        _overlayItem = null;
    }

    private void ConfigureCardStatusMenuOverlay(bool openAsSubmenu)
    {
        if (openAsSubmenu)
        {
            CardStatusMenuOverlay.Placement = OverlayPlacement.RightTop;
            CardStatusMenuOverlay.HorizontalOffset = TryFindResource("SubmenuPopupHorizontalOffset") is double horizontal
                ? horizontal
                : 2d;
            CardStatusMenuOverlay.VerticalOffset = 0;
            CardStatusMenuOverlay.AnimationOrigin = new Point(0, 0);
            return;
        }

        CardStatusMenuOverlay.Placement = OverlayPlacement.TopCenter;
        CardStatusMenuOverlay.HorizontalOffset = 0;
        CardStatusMenuOverlay.VerticalOffset = 12;
        CardStatusMenuOverlay.AnimationOrigin = new Point(0.5, 1);
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


