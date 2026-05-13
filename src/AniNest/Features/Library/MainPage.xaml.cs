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
using AniNest.Presentation.Primitives;
using Point = System.Windows.Point;
namespace AniNest.Features.Library;

public partial class MainPage : System.Windows.Controls.UserControl
{
    private const string CardBorderElementName = "CardBorder";

    private enum CardOverlayId
    {
        StatusMenu,
        ContextMenu,
        ThumbnailActions
    }

    private sealed class CardOverlayRegistration
    {
        public required CardOverlayId Id { get; init; }
        public required AnimatedOverlay Overlay { get; init; }
        public CardOverlayId? ParentId { get; init; }
        public SelectableOptionGroup? HighlightGroup { get; init; }
        public Action? OnClosed { get; init; }
    }

    private static readonly Logger Log = AppLog.For(nameof(MainPage));
    private readonly Dictionary<CardOverlayId, CardOverlayRegistration> _cardOverlays = [];
    private readonly Dictionary<AnimatedOverlay, SelectableOptionGroup> _overlayHighlightGroups = [];
    private PerfSceneSession? _initialLoadScene;
    private MainPageViewModel? _viewModel;
    private bool _initialLoadCompleted;
    private int _renderFramesAfterLoadCompleted;
    private FolderListItem? _overlayItem;

    public MainPage()
    {
        InitializeComponent();
        InitializeCardOverlays();
        RegisterOverlayRegions();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeCardOverlays()
    {
        RegisterCardOverlay(new CardOverlayRegistration
        {
            Id = CardOverlayId.StatusMenu,
            Overlay = CardStatusMenuOverlay,
            HighlightGroup = CardStatusOptionGroup,
            OnClosed = () =>
            {
                CardStatusMenuOverlay.DataContext = null;
                _overlayItem = null;
            }
        });

        RegisterCardOverlay(new CardOverlayRegistration
        {
            Id = CardOverlayId.ContextMenu,
            Overlay = CardContextMenuOverlay,
            OnClosed = () =>
            {
                CardContextMenuOverlay.DataContext = null;
                _overlayItem = null;
            }
        });

        RegisterCardOverlay(new CardOverlayRegistration
        {
            Id = CardOverlayId.ThumbnailActions,
            Overlay = ThumbnailActionsOverlay,
            ParentId = CardOverlayId.ContextMenu
        });
    }

    private void RegisterCardOverlay(CardOverlayRegistration registration)
    {
        _cardOverlays.Add(registration.Id, registration);
        registration.Overlay.Closed += (_, e) => OnCardOverlayClosed(registration, e);

        if (registration.HighlightGroup == null)
            return;

        registration.HighlightGroup.IsSelectionHighlightActive = false;
        _overlayHighlightGroups.Add(registration.Overlay, registration.HighlightGroup);
        registration.Overlay.Opening += OnSelectableOverlayOpening;
        registration.Overlay.Opened += OnSelectableOverlayOpened;
        registration.Overlay.Closing += OnSelectableOverlayClosing;
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
        if (sender is not FrameworkElement host || host.DataContext is not FolderListItem item)
            return;

        var border = FindNamedDescendant<Border>(host, CardBorderElementName);
        if (border == null)
        {
            Log.Warning($"OnCardPreviewMouseRightButtonUp: missing card anchor name={item.Name}");
            return;
        }

        OverlayCoordinator.Instance.RegisterRegion(border, OverlayOutsideHitKind.ContentInteractive);
        CloseCardOverlay(CardOverlayId.StatusMenu, OverlayCloseReason.ChainSwitch);
        CloseCardOverlay(CardOverlayId.ThumbnailActions, OverlayCloseReason.ChainSwitch);

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

        CloseCardOverlay(CardOverlayId.StatusMenu, OverlayCloseReason.ChainSwitch);

        var opened = ThumbnailActionsOverlay.ToggleForAnchor(ThumbnailActionsMenuButton);
        Log.Debug($"ThumbnailActionsMenuButton_Click.Toggle: opened={opened}");
    }

    private void CardStatusMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not FolderListItem item)
            return;

        OverlayCoordinator.Instance.RegisterRegion(button, OverlayOutsideHitKind.ContentInteractive);
        CloseCardOverlay(CardOverlayId.ThumbnailActions, OverlayCloseReason.ChainSwitch);

        Log.Debug(
            $"CardStatusMenuButton_Click: name={item.Name} status={item.Status} " +
            $"overlayOpen={CardStatusMenuOverlay.IsOpen} overlayItem={_overlayItem?.Name ?? "null"}");

        Dispatcher.BeginInvoke(new Action(() => OpenCardStatusMenu(button, item, openAsSubmenu: true)), DispatcherPriority.Input);
    }

    private async void CardStatusMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: WatchStatus status })
            return;

        await ExecuteFolderStatusChangeAsync(status);
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
        CloseCardOverlay(CardOverlayId.StatusMenu, reason);
        CloseCardOverlay(CardOverlayId.ThumbnailActions, reason);
    }

    private void CloseCardOverlay(CardOverlayId id, OverlayCloseReason reason)
    {
        if (!TryGetCardOverlayRegistration(id, out var registration))
            return;

        if (!registration.Overlay.IsOpen)
            return;

        Log.Debug($"Close{registration.Overlay.Name}: reason={reason}");
        registration.Overlay.Close(reason);
    }

    private void OnCardOverlayClosed(
        CardOverlayRegistration registration,
        AnimatedOverlay.OverlayClosedEventArgs e)
    {
        registration.OnClosed?.Invoke();
        foreach (var child in _cardOverlays.Values)
        {
            if (child.ParentId != registration.Id)
                continue;

            CloseCardOverlay(child.Id, OverlayCloseReason.ParentClosed);
        }

        Log.Debug($"On{registration.Overlay.Name}Closed: reason={e.Reason} overlayItem={_overlayItem?.Name ?? "null"}");
    }

    private void OnSelectableOverlayOpening(object? sender, EventArgs e)
    {
        SetSelectableHighlightActivation(sender, false);
    }

    private void OnSelectableOverlayOpened(object? sender, EventArgs e)
    {
        if (sender is not AnimatedOverlay overlay ||
            !_overlayHighlightGroups.TryGetValue(overlay, out var highlightGroup))
            return;

        highlightGroup.IsSelectionHighlightActive = true;
        SelectionHighlightAnimation.InvalidateDescendants(highlightGroup);
        Dispatcher.BeginInvoke(
            new Action(() => SelectionHighlightAnimation.InvalidateDescendants(highlightGroup)),
            DispatcherPriority.Loaded);
    }

    private void OnSelectableOverlayClosing(object? sender, EventArgs e)
    {
        SetSelectableHighlightActivation(sender, false);
    }

    private void SetSelectableHighlightActivation(object? sender, bool isActive)
    {
        if (sender is AnimatedOverlay overlay &&
            _overlayHighlightGroups.TryGetValue(overlay, out var highlightGroup))
        {
            highlightGroup.IsSelectionHighlightActive = isActive;
        }
    }

    private bool TryGetCardOverlayRegistration(CardOverlayId id, out CardOverlayRegistration registration)
    {
        if (_cardOverlays.TryGetValue(id, out registration!))
            return true;

        Log.Warning($"Missing card overlay registration: id={id}");
        return false;
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
            CardStatusMenuOverlay.HorizontalOffset = TryFindResource("LibraryCardStatusSubmenuHorizontalOffset") is double horizontal
                ? horizontal
                : 0d;
            CardStatusMenuOverlay.VerticalOffset = 0;
            CardStatusMenuOverlay.AnimationOrigin = new Point(0, 0);
            return;
        }
    }

    private static T? FindNamedDescendant<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T element && string.Equals(element.Name, name, StringComparison.Ordinal))
                return element;

            var nested = FindNamedDescendant<T>(child, name);
            if (nested != null)
                return nested;
        }

        return null;
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


