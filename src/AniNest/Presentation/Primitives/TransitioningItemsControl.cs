using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AniNest.Infrastructure.Logging;
using AniNest.Presentation.Animations;

namespace AniNest.Presentation.Primitives;

public enum TransitioningItemsPreset
{
    Default,
    CardGrid
}

public class TransitioningItemsControl : ItemsControl
{
    private static readonly Logger Log = AppLog.For(nameof(TransitioningItemsControl));
    private const string RootPartName = "PART_Root";
    private const string GhostLayerPartName = "PART_GhostLayer";

    public static readonly DependencyProperty EnterDurationProperty =
        DependencyProperty.Register(
            nameof(EnterDuration),
            typeof(Duration),
            typeof(TransitioningItemsControl),
            new FrameworkPropertyMetadata(new Duration(TimeSpan.FromMilliseconds(240))));

    public static readonly DependencyProperty ExitDurationProperty =
        DependencyProperty.Register(
            nameof(ExitDuration),
            typeof(Duration),
            typeof(TransitioningItemsControl),
            new FrameworkPropertyMetadata(new Duration(TimeSpan.FromMilliseconds(180))));

    public static readonly DependencyProperty EnterStaggerDelayMsProperty =
        DependencyProperty.Register(
            nameof(EnterStaggerDelayMs),
            typeof(int),
            typeof(TransitioningItemsControl),
            new FrameworkPropertyMetadata(35));

    public static readonly DependencyProperty EnterFromScaleProperty =
        DependencyProperty.Register(
            nameof(EnterFromScale),
            typeof(double),
            typeof(TransitioningItemsControl),
            new FrameworkPropertyMetadata(0.92d));

    public static readonly DependencyProperty ExitToScaleProperty =
        DependencyProperty.Register(
            nameof(ExitToScale),
            typeof(double),
            typeof(TransitioningItemsControl),
            new FrameworkPropertyMetadata(0.88d));

    public static readonly DependencyProperty PresetProperty =
        DependencyProperty.Register(
            nameof(Preset),
            typeof(TransitioningItemsPreset),
            typeof(TransitioningItemsControl),
            new FrameworkPropertyMetadata(TransitioningItemsPreset.Default));

    private readonly HashSet<object> _enteredItems = [];
    private readonly List<object> _pendingEnterItems = [];
    private readonly HashSet<object> _pendingEnterLookup = [];
    private readonly Dictionary<object, Rect> _layoutSnapshots = [];
    private Grid? _root;
    private Canvas? _ghostLayer;
    private bool _isControlLoaded;
    private bool _enterProcessingScheduled;
    private bool _layoutCaptureScheduled;
    private int _layoutCaptureRetryCount;

    public TransitioningItemsControl()
    {
        Loaded += OnControlLoaded;
        Unloaded += OnControlUnloaded;
        LayoutUpdated += OnLayoutUpdated;
        ItemContainerGenerator.StatusChanged += OnItemContainerGeneratorStatusChanged;
    }

    public Duration EnterDuration
    {
        get => (Duration)GetValue(EnterDurationProperty);
        set => SetValue(EnterDurationProperty, value);
    }

    public Duration ExitDuration
    {
        get => (Duration)GetValue(ExitDurationProperty);
        set => SetValue(ExitDurationProperty, value);
    }

    public int EnterStaggerDelayMs
    {
        get => (int)GetValue(EnterStaggerDelayMsProperty);
        set => SetValue(EnterStaggerDelayMsProperty, value);
    }

    public double EnterFromScale
    {
        get => (double)GetValue(EnterFromScaleProperty);
        set => SetValue(EnterFromScaleProperty, value);
    }

    public double ExitToScale
    {
        get => (double)GetValue(ExitToScaleProperty);
        set => SetValue(ExitToScaleProperty, value);
    }

    public TransitioningItemsPreset Preset
    {
        get => (TransitioningItemsPreset)GetValue(PresetProperty);
        set => SetValue(PresetProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _root = GetTemplateChild(RootPartName) as Grid;
        _ghostLayer = GetTemplateChild(GhostLayerPartName) as Canvas;
        ScheduleLayoutCapture();
        SchedulePendingEnterProcessing();
    }

    protected override DependencyObject GetContainerForItemOverride()
        => new ContentPresenter();

    protected override bool IsItemItsOwnContainerOverride(object item)
        => item is UIElement || base.IsItemItsOwnContainerOverride(item);

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);

        if (element is not FrameworkElement container)
            return;

        if (_enteredItems.Contains(item))
        {
            SnapContainerToVisible(container);
        }
        else
        {
            InitializeContainerForEnter(container);
            QueueEnter(item);
        }

        ScheduleLayoutCapture();
    }

    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is FrameworkElement container)
            StopContainerAnimations(container);

        base.ClearContainerForItemOverride(element, item);
    }

    protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
    {
        base.OnItemsSourceChanged(oldValue, newValue);

        _enteredItems.Clear();
        _pendingEnterItems.Clear();
        _pendingEnterLookup.Clear();
        _layoutSnapshots.Clear();
        _ghostLayer?.Children.Clear();
        SchedulePendingEnterProcessing();
        ScheduleLayoutCapture();
    }

    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        Log.Debug(
            $"OnItemsChanged: action={e.Action} oldCount={e.OldItems?.Count ?? 0} newCount={e.NewItems?.Count ?? 0} " +
            $"itemsBeforeBase={Items.Count} entered={_enteredItems.Count} pending={_pendingEnterItems.Count} snapshots={_layoutSnapshots.Count}");

        if (e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace && e.OldItems != null)
        {
            foreach (object item in e.OldItems)
                BeginExitGhost(item);
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            _enteredItems.Clear();
            _pendingEnterItems.Clear();
            _pendingEnterLookup.Clear();
            _layoutSnapshots.Clear();
            _ghostLayer?.Children.Clear();
        }

        base.OnItemsChanged(e);

        if (e.OldItems != null)
        {
            foreach (object item in e.OldItems)
            {
                _enteredItems.Remove(item);
                _pendingEnterLookup.Remove(item);
                _pendingEnterItems.Remove(item);
                _layoutSnapshots.Remove(item);
            }
        }

        if (e.NewItems != null)
        {
            foreach (object item in e.NewItems)
                QueueEnter(item);
        }

        SchedulePendingEnterProcessing();
        ScheduleLayoutCapture();
    }

    private void OnControlLoaded(object sender, RoutedEventArgs e)
    {
        _isControlLoaded = true;
        SchedulePendingEnterProcessing();
        ScheduleLayoutCapture();
    }

    private void OnControlUnloaded(object sender, RoutedEventArgs e)
    {
        _isControlLoaded = false;
        _enterProcessingScheduled = false;
        _layoutCaptureScheduled = false;
        _ghostLayer?.Children.Clear();
    }

    private void OnItemContainerGeneratorStatusChanged(object? sender, EventArgs e)
    {
        if (ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
        {
            SchedulePendingEnterProcessing();
            ScheduleLayoutCapture();
        }
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_layoutCaptureScheduled)
            return;

        _layoutCaptureScheduled = false;
        CaptureLiveLayout();
    }

    private void QueueEnter(object item)
    {
        if (_enteredItems.Contains(item))
            return;

        if (_pendingEnterLookup.Add(item))
            _pendingEnterItems.Add(item);
    }

    private void SchedulePendingEnterProcessing()
    {
        if (_enterProcessingScheduled || !_isControlLoaded)
            return;

        _enterProcessingScheduled = true;
        Dispatcher.BeginInvoke(ProcessPendingEnterAnimations, DispatcherPriority.Loaded);
    }

    private void ProcessPendingEnterAnimations()
    {
        _enterProcessingScheduled = false;

        if (!_isControlLoaded)
            return;

        if (ItemContainerGenerator.Status != System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
        {
            SchedulePendingEnterProcessing();
            return;
        }

        int enterOrder = 0;
        var remainingItems = new List<object>(_pendingEnterItems.Count);

        for (int i = 0; i < _pendingEnterItems.Count; i++)
        {
            object item = _pendingEnterItems[i];
            var container = ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
            if (container == null || !container.IsLoaded)
            {
                remainingItems.Add(item);
                continue;
            }

            _pendingEnterLookup.Remove(item);
            _enteredItems.Add(item);
            BeginEnterAnimation(container, enterOrder * ResolveEnterStaggerDelayMs());
            enterOrder++;
        }

        _pendingEnterItems.Clear();
        _pendingEnterItems.AddRange(remainingItems);

        if (_pendingEnterItems.Count > 0)
            SchedulePendingEnterProcessing();
    }

    private void ScheduleLayoutCapture()
    {
        _layoutCaptureRetryCount = 0;
        _layoutCaptureScheduled = true;
    }

    private void CaptureLiveLayout()
    {
        if (_root == null)
            return;

        int captured = 0;
        int skipped = 0;
        int deferred = 0;

        foreach (object item in Items)
        {
            if (ItemContainerGenerator.ContainerFromItem(item) is not FrameworkElement container)
            {
                skipped++;
                continue;
            }

            if (!container.IsLoaded || container.RenderSize.Width <= 0 || container.RenderSize.Height <= 0)
            {
                deferred++;
                continue;
            }

            try
            {
                Point topLeft = container.TransformToVisual(_root).Transform(new Point(0, 0));
                _layoutSnapshots[item] = new Rect(topLeft, container.RenderSize);
                captured++;
            }
            catch (InvalidOperationException)
            {
                skipped++;
            }
        }

        Log.Debug(
            $"CaptureLiveLayout: captured={captured} skipped={skipped} deferred={deferred} " +
            $"snapshotCount={_layoutSnapshots.Count} retry={_layoutCaptureRetryCount}");

        if (captured == 0 && deferred > 0 && _layoutCaptureRetryCount < 5)
        {
            _layoutCaptureRetryCount++;
            _layoutCaptureScheduled = true;
            Dispatcher.BeginInvoke(new Action(InvalidateArrange), DispatcherPriority.Loaded);
            return;
        }

        _layoutCaptureRetryCount = 0;
    }

    private void BeginExitGhost(object item)
    {
        if (_ghostLayer == null || !_enteredItems.Contains(item))
            return;

        if (!TryResolveVisualBounds(item, out Rect bounds) || bounds.Width <= 0 || bounds.Height <= 0)
            return;

        Log.Debug(
            $"BeginExitGhost: item={DescribeItem(item)} left={bounds.Left:F1} top={bounds.Top:F1} " +
            $"width={bounds.Width:F1} height={bounds.Height:F1}");

        var presenter = new ContentPresenter
        {
            Content = item,
            ContentTemplate = ItemTemplate,
            ContentTemplateSelector = ItemTemplateSelector,
            ContentStringFormat = ItemStringFormat,
            Width = bounds.Width,
            Height = bounds.Height,
            IsHitTestVisible = false,
            Opacity = 1,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1)
        };

        Canvas.SetLeft(presenter, bounds.Left);
        Canvas.SetTop(presenter, bounds.Top);
        Panel.SetZIndex(presenter, 1);
        _ghostLayer.Children.Add(presenter);

        int durationMs = ResolveDurationMs(ExitDuration);
        durationMs = ResolveExitDurationMs(durationMs);
        var scale = (ScaleTransform)presenter.RenderTransform;
        scale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            AnimationHelper.CreateAnim(1, ResolveExitToScale(), durationMs, AnimationHelper.EaseIn));
        scale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            AnimationHelper.CreateAnim(1, ResolveExitToScale(), durationMs, AnimationHelper.EaseIn));

        var opacityAnimation = AnimationHelper.CreateAnim(1, 0, durationMs, AnimationHelper.EaseIn);
        opacityAnimation.Completed += (_, _) =>
        {
            _ghostLayer.Children.Remove(presenter);
        };
        presenter.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
    }

    private bool TryResolveVisualBounds(object item, out Rect bounds)
    {
        if (_root != null && ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement container && container.IsLoaded)
        {
            try
            {
                Point topLeft = container.TransformToVisual(_root).Transform(new Point(0, 0));
                bounds = new Rect(topLeft, container.RenderSize);
                return true;
            }
            catch (InvalidOperationException)
            {
                // Ignore and fall back to the last captured snapshot.
            }
        }

        return _layoutSnapshots.TryGetValue(item, out bounds);
    }

    private void InitializeContainerForEnter(FrameworkElement container)
    {
        var scale = EnsureLifecycleScaleTransform(container);
        StopContainerAnimations(container);
        double fromScale = ResolveEnterFromScale();
        scale.ScaleX = fromScale;
        scale.ScaleY = fromScale;
        container.Opacity = 0;
    }

    private void BeginEnterAnimation(FrameworkElement container, int beginTimeMs)
    {
        var scale = EnsureLifecycleScaleTransform(container);
        StopContainerAnimations(container);
        double fromScale = ResolveEnterFromScale();
        scale.ScaleX = fromScale;
        scale.ScaleY = fromScale;
        container.Opacity = 0;

        int durationMs = ResolveEnterDurationMs(ResolveDurationMs(EnterDuration));
        scale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            AnimationHelper.CreateAnim(fromScale, 1, durationMs, AnimationHelper.EaseOut, beginTimeMs));
        scale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            AnimationHelper.CreateAnim(fromScale, 1, durationMs, AnimationHelper.EaseOut, beginTimeMs));

        var opacityAnimation = AnimationHelper.CreateAnim(0, 1, durationMs, AnimationHelper.EaseOut, beginTimeMs);
        opacityAnimation.Completed += (_, _) =>
        {
            container.BeginAnimation(UIElement.OpacityProperty, null);
            container.Opacity = 1;
            ScheduleLayoutCapture();
        };
        container.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
    }

    private static void SnapContainerToVisible(FrameworkElement container)
    {
        var scale = EnsureLifecycleScaleTransform(container);
        StopContainerAnimations(container);
        scale.ScaleX = 1;
        scale.ScaleY = 1;
        container.Opacity = 1;
    }

    private static void StopContainerAnimations(FrameworkElement container)
    {
        container.BeginAnimation(UIElement.OpacityProperty, null);

        var scale = EnsureLifecycleScaleTransform(container);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
    }

    private static ScaleTransform EnsureLifecycleScaleTransform(FrameworkElement element)
    {
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        return TransformComposer.EnsurePrimaryScaleTransform(element);
    }

    private static int ResolveDurationMs(Duration duration)
    {
        if (!duration.HasTimeSpan)
            return 0;

        return (int)Math.Round(duration.TimeSpan.TotalMilliseconds);
    }

    private int ResolveEnterDurationMs(int configuredDurationMs)
        => HasLocalValue(EnterDurationProperty) ? configuredDurationMs : Preset switch
        {
            TransitioningItemsPreset.CardGrid => 240,
            _ => configuredDurationMs
        };

    private int ResolveExitDurationMs(int configuredDurationMs)
        => HasLocalValue(ExitDurationProperty) ? configuredDurationMs : Preset switch
        {
            TransitioningItemsPreset.CardGrid => 180,
            _ => configuredDurationMs
        };

    private int ResolveEnterStaggerDelayMs()
        => HasLocalValue(EnterStaggerDelayMsProperty) ? EnterStaggerDelayMs : Preset switch
        {
            TransitioningItemsPreset.CardGrid => 60,
            _ => EnterStaggerDelayMs
        };

    private double ResolveEnterFromScale()
        => HasLocalValue(EnterFromScaleProperty) ? EnterFromScale : Preset switch
        {
            TransitioningItemsPreset.CardGrid => 0.92d,
            _ => EnterFromScale
        };

    private double ResolveExitToScale()
        => HasLocalValue(ExitToScaleProperty) ? ExitToScale : Preset switch
        {
            TransitioningItemsPreset.CardGrid => 0.88d,
            _ => ExitToScale
        };

    private bool HasLocalValue(DependencyProperty property)
        => ReadLocalValue(property) != DependencyProperty.UnsetValue;

    private static string DescribeItem(object? item)
    {
        if (item == null)
            return "null";

        if (item is Features.Library.Models.FolderListItem folder)
            return $"{folder.Name}({folder.Path})";

        return item.ToString() ?? item.GetType().Name;
    }

}
