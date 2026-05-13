using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AniNest.Infrastructure.Logging;
using AniNest.Presentation.Animations;
using Point = System.Windows.Point;

namespace AniNest.Presentation.Overlays;

/// <summary>
/// Tree-hosted animated overlay primitive.
///
/// Intended public usage:
/// - Anchor and placement define where the overlay lives.
/// - <see cref="ToggleForAnchor"/> and <see cref="OpenOrRetarget"/> are the main
///   interaction entry points for feature code.
/// - Dismissal policy is primarily driven by <see cref="InteractionPreset"/>, with
///   the raw close/pointer properties available for advanced cases.
/// - Actual pointer arbitration is delegated to <see cref="OverlayCoordinator"/>.
/// - <see cref="Closed"/> and <see cref="LastCloseReason"/> provide the feature layer
///   with a single cleanup hook.
///
/// Use this type for interaction-heavy floating UI such as menus, context menus,
/// settings surfaces, and tool panels.
/// </summary>
public class AnimatedOverlay : ContentControl
{
    public const int DefaultAnimationDurationMs = 240;

    public sealed class OverlayClosedEventArgs : EventArgs
    {
        public OverlayClosedEventArgs(OverlayCloseReason reason) => Reason = reason;
        public OverlayCloseReason Reason { get; }
    }

    private static readonly Logger Log = AppLog.For(nameof(AnimatedOverlay));
    private FrameworkElement? _surface;
    private FrameworkElement? _positionHost;
    private FrameworkElement? _host;
    private Window? _window;
    private OverlayState _state = OverlayState.Closed;
    private bool _isRepositioning;
    private bool _repositionQueued;
    private bool _isOpenRetryPending;
    private int _transitionVersion;

    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(AnimatedOverlay),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));

    public static readonly DependencyProperty AnchorElementProperty =
        DependencyProperty.Register(nameof(AnchorElement), typeof(FrameworkElement), typeof(AnimatedOverlay),
            new PropertyMetadata(null, OnAnchorElementChanged));

    public static readonly DependencyProperty PlacementProperty =
        DependencyProperty.Register(nameof(Placement), typeof(OverlayPlacement), typeof(AnimatedOverlay),
            new PropertyMetadata(OverlayPlacement.RightTop));

    public static readonly DependencyProperty HorizontalOffsetProperty =
        DependencyProperty.Register(nameof(HorizontalOffset), typeof(double), typeof(AnimatedOverlay),
            new PropertyMetadata(0d));

    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.Register(nameof(VerticalOffset), typeof(double), typeof(AnimatedOverlay),
            new PropertyMetadata(0d));

    public static readonly DependencyProperty ConstrainToHostBoundsProperty =
        DependencyProperty.Register(nameof(ConstrainToHostBounds), typeof(bool), typeof(AnimatedOverlay),
            new PropertyMetadata(true));

    public static readonly DependencyProperty OpenDurationMsProperty =
        DependencyProperty.Register(nameof(OpenDurationMs), typeof(int), typeof(AnimatedOverlay),
            new PropertyMetadata(DefaultAnimationDurationMs));

    public static readonly DependencyProperty CloseDurationMsProperty =
        DependencyProperty.Register(nameof(CloseDurationMs), typeof(int), typeof(AnimatedOverlay),
            new PropertyMetadata(DefaultAnimationDurationMs));

    public static readonly DependencyProperty AnimationOriginProperty =
        DependencyProperty.Register(nameof(AnimationOrigin), typeof(Point), typeof(AnimatedOverlay),
            new PropertyMetadata(new Point(0, 0)));

    public static readonly DependencyProperty OpenScaleFromProperty =
        DependencyProperty.Register(nameof(OpenScaleFrom), typeof(double), typeof(AnimatedOverlay),
            new PropertyMetadata(0d));

    public static readonly DependencyProperty CloseScaleToProperty =
        DependencyProperty.Register(nameof(CloseScaleTo), typeof(double), typeof(AnimatedOverlay),
            new PropertyMetadata(0d));

    public static readonly DependencyProperty CloseOnOutsideClickProperty =
        DependencyProperty.Register(nameof(CloseOnOutsideClick), typeof(bool), typeof(AnimatedOverlay),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ResetAnchorOnCloseProperty =
        DependencyProperty.Register(nameof(ResetAnchorOnClose), typeof(bool), typeof(AnimatedOverlay),
            new PropertyMetadata(false));

    public static readonly DependencyProperty CloseOnEscapeProperty =
        DependencyProperty.Register(nameof(CloseOnEscape), typeof(bool), typeof(AnimatedOverlay),
            new PropertyMetadata(false));

    public static readonly DependencyProperty LeftAnchorClickBehaviorProperty =
        DependencyProperty.Register(nameof(LeftAnchorClickBehavior), typeof(OverlayPointerBehavior), typeof(AnimatedOverlay),
            new PropertyMetadata(OverlayPointerBehavior.KeepOpen));

    public static readonly DependencyProperty RightAnchorClickBehaviorProperty =
        DependencyProperty.Register(nameof(RightAnchorClickBehavior), typeof(OverlayPointerBehavior), typeof(AnimatedOverlay),
            new PropertyMetadata(OverlayPointerBehavior.KeepOpen));

    public static readonly DependencyProperty OutsidePointerBehaviorProperty =
        DependencyProperty.Register(nameof(OutsidePointerBehavior), typeof(OverlayPointerBehavior), typeof(AnimatedOverlay),
            new PropertyMetadata(OverlayPointerBehavior.CloseAndPassThrough));

    public static readonly DependencyProperty OutsidePassthroughTargetsProperty =
        DependencyProperty.Register(nameof(OutsidePassthroughTargets), typeof(OverlayOutsidePassthroughTargets), typeof(AnimatedOverlay),
            new PropertyMetadata(OverlayOutsidePassthroughTargets.None));

    public static readonly DependencyProperty InteractionPresetProperty =
        DependencyProperty.Register(nameof(InteractionPreset), typeof(OverlayInteractionPreset), typeof(AnimatedOverlay),
            new PropertyMetadata(OverlayInteractionPreset.None, OnInteractionPresetChanged));

    private static readonly DependencyPropertyKey SurfaceMarginPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(SurfaceMargin), typeof(Thickness), typeof(AnimatedOverlay),
            new PropertyMetadata(new Thickness()));

    private static readonly DependencyPropertyKey CurrentStatePropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(CurrentState), typeof(string), typeof(AnimatedOverlay),
            new PropertyMetadata("Closed"));

    private static readonly DependencyPropertyKey LastCloseReasonPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(LastCloseReason), typeof(OverlayCloseReason), typeof(AnimatedOverlay),
            new PropertyMetadata(OverlayCloseReason.Programmatic));

    public static readonly DependencyProperty SurfaceMarginProperty = SurfaceMarginPropertyKey.DependencyProperty;
    public static readonly DependencyProperty CurrentStateProperty = CurrentStatePropertyKey.DependencyProperty;
    public static readonly DependencyProperty LastCloseReasonProperty = LastCloseReasonPropertyKey.DependencyProperty;

    public event EventHandler? Opened;
    public event EventHandler<OverlayClosedEventArgs>? Closed;
    public event EventHandler? Opening;
    public event EventHandler? Closing;

    public AnimatedOverlay? ParentOverlay { get; internal set; }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public FrameworkElement? AnchorElement
    {
        get => (FrameworkElement?)GetValue(AnchorElementProperty);
        set => SetValue(AnchorElementProperty, value);
    }

    public OverlayPlacement Placement
    {
        get => (OverlayPlacement)GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    public double HorizontalOffset
    {
        get => (double)GetValue(HorizontalOffsetProperty);
        set => SetValue(HorizontalOffsetProperty, value);
    }

    public double VerticalOffset
    {
        get => (double)GetValue(VerticalOffsetProperty);
        set => SetValue(VerticalOffsetProperty, value);
    }

    public bool ConstrainToHostBounds
    {
        get => (bool)GetValue(ConstrainToHostBoundsProperty);
        set => SetValue(ConstrainToHostBoundsProperty, value);
    }

    public int OpenDurationMs
    {
        get => (int)GetValue(OpenDurationMsProperty);
        set => SetValue(OpenDurationMsProperty, value);
    }

    public int CloseDurationMs
    {
        get => (int)GetValue(CloseDurationMsProperty);
        set => SetValue(CloseDurationMsProperty, value);
    }

    public Point AnimationOrigin
    {
        get => (Point)GetValue(AnimationOriginProperty);
        set => SetValue(AnimationOriginProperty, value);
    }

    public double OpenScaleFrom
    {
        get => (double)GetValue(OpenScaleFromProperty);
        set => SetValue(OpenScaleFromProperty, value);
    }

    public double CloseScaleTo
    {
        get => (double)GetValue(CloseScaleToProperty);
        set => SetValue(CloseScaleToProperty, value);
    }

    public bool CloseOnOutsideClick
    {
        get => (bool)GetValue(CloseOnOutsideClickProperty);
        set => SetValue(CloseOnOutsideClickProperty, value);
    }

    public bool ResetAnchorOnClose
    {
        get => (bool)GetValue(ResetAnchorOnCloseProperty);
        set => SetValue(ResetAnchorOnCloseProperty, value);
    }

    public bool CloseOnEscape
    {
        get => (bool)GetValue(CloseOnEscapeProperty);
        set => SetValue(CloseOnEscapeProperty, value);
    }

    public OverlayPointerBehavior LeftAnchorClickBehavior
    {
        get => (OverlayPointerBehavior)GetValue(LeftAnchorClickBehaviorProperty);
        set => SetValue(LeftAnchorClickBehaviorProperty, value);
    }

    public OverlayPointerBehavior RightAnchorClickBehavior
    {
        get => (OverlayPointerBehavior)GetValue(RightAnchorClickBehaviorProperty);
        set => SetValue(RightAnchorClickBehaviorProperty, value);
    }

    public OverlayPointerBehavior OutsidePointerBehavior
    {
        get => (OverlayPointerBehavior)GetValue(OutsidePointerBehaviorProperty);
        set => SetValue(OutsidePointerBehaviorProperty, value);
    }

    public OverlayOutsidePassthroughTargets OutsidePassthroughTargets
    {
        get => (OverlayOutsidePassthroughTargets)GetValue(OutsidePassthroughTargetsProperty);
        set => SetValue(OutsidePassthroughTargetsProperty, value);
    }

    public OverlayInteractionPreset InteractionPreset
    {
        get => (OverlayInteractionPreset)GetValue(InteractionPresetProperty);
        set => SetValue(InteractionPresetProperty, value);
    }

    public Thickness SurfaceMargin
    {
        get => (Thickness)GetValue(SurfaceMarginProperty);
        private set => SetValue(SurfaceMarginPropertyKey, value);
    }

    public string CurrentState
    {
        get => (string)GetValue(CurrentStateProperty);
        private set => SetValue(CurrentStatePropertyKey, value);
    }

    public OverlayCloseReason LastCloseReason
    {
        get => (OverlayCloseReason)GetValue(LastCloseReasonProperty);
        private set => SetValue(LastCloseReasonPropertyKey, value);
    }

    public AnimatedOverlay()
    {
        Visibility = Visibility.Collapsed;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        IsHitTestVisible = false;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _positionHost = GetTemplateChild("PART_PositionHost") as FrameworkElement;
        _surface = GetTemplateChild("PART_Surface") as FrameworkElement;
        Log.Debug($"OnApplyTemplate: hasPositionHost={_positionHost != null} hasSurface={_surface != null}");
        UpdateSurfaceInteractiveState();
    }

    public bool ContainsSurfaceTarget(DependencyObject? target)
    {
        if (target == null)
            return false;

        DependencyObject? current = target;
        while (current != null)
        {
            if ((_surface != null && ReferenceEquals(current, _surface)) ||
                (_positionHost != null && ReferenceEquals(current, _positionHost)))
                return true;

            current = current switch
            {
                Visual visual => VisualTreeHelper.GetParent(visual),
                System.Windows.Media.Media3D.Visual3D visual3D => VisualTreeHelper.GetParent(visual3D),
                _ => LogicalTreeHelper.GetParent(current),
            };
        }

        return false;
    }

    public bool ContainsAnchorTarget(DependencyObject? target)
    {
        if (AnchorElement == null || target == null)
            return false;

        DependencyObject? current = target;
        while (current != null)
        {
            if (ReferenceEquals(current, AnchorElement))
                return true;

            current = current switch
            {
                Visual visual => VisualTreeHelper.GetParent(visual),
                System.Windows.Media.Media3D.Visual3D visual3D => VisualTreeHelper.GetParent(visual3D),
                _ => LogicalTreeHelper.GetParent(current),
            };
        }

        return false;
    }

    public void Close(OverlayCloseReason reason = OverlayCloseReason.Programmatic)
    {
        if (!IsOpen && _state == OverlayState.Closed)
            return;

        Log.Debug(
            $"Close.Request: overlay={Name} preset={InteractionPreset} reason={reason} " +
            $"state={_state} isOpen={IsOpen}");

        var closeDecision = OverlayInteractionPresets.ResolveCloseRequest(this, reason);
        if (closeDecision.IsHandled || !closeDecision.ShouldClose)
        {
            if (closeDecision.IsHandled)
                OverlayInteractionPresets.TryHandleCloseRequest(this, reason);

            Log.Debug(
                $"Close decision prevented dismissal: preset={InteractionPreset} reason={reason} " +
                $"state={_state} handled={closeDecision.IsHandled} detail={closeDecision.Detail}");
            return;
        }

        Log.Debug($"Close: reason={reason} state={_state}");
        LastCloseReason = reason;
        IsOpen = false;

        if (ResetAnchorOnClose)
            ResetAnchor();
    }

    public bool ToggleForAnchor(FrameworkElement anchor, OverlayCloseReason toggleCloseReason = OverlayCloseReason.Toggle)
    {
        if (anchor == null)
            throw new ArgumentNullException(nameof(anchor));

        if (IsOpen && ReferenceEquals(AnchorElement, anchor))
        {
            Log.Debug($"ToggleForAnchor: closing current anchor={anchor.Name}");
            Close(toggleCloseReason);
            return false;
        }

        OpenOrRetarget(anchor);
        return true;
    }

    public void OpenOrRetarget(FrameworkElement anchor)
    {
        if (anchor == null)
            throw new ArgumentNullException(nameof(anchor));

        Log.Debug($"OpenOrRetarget: anchor={anchor.Name} isOpen={IsOpen} state={_state}");
        SwitchAnchor(anchor);

        if (!IsOpen)
            IsOpen = true;
    }

    public void ResetAnchor()
    {
        Log.Debug("ResetAnchor");
        SwitchAnchor(null);
    }

    public bool HandlePointerDown(DependencyObject? target, OverlayCloseReason reason = OverlayCloseReason.OutsideClick)
    {
        if (!IsOpen || !CloseOnOutsideClick)
            return false;

        var contains = ContainsSurfaceTarget(target);
        Log.Debug(
            $"HandlePointerDown: target={DescribeTarget(target)} containsSurface={contains} " +
            $"state={_state} closeOnOutside={CloseOnOutsideClick}");

        if (contains)
            return false;

        Close(reason);
        return true;
    }

    public bool HandleKeyDown(KeyEventArgs? args, OverlayCloseReason reason = OverlayCloseReason.EscapeKey)
    {
        if (args == null || !IsOpen || args.Key != Key.Escape)
            return false;

        var closeDecision = OverlayInteractionPresets.ResolveCloseRequest(this, reason);
        if (closeDecision.IsHandled)
        {
            Log.Debug(
                $"HandleKeyDown intercepted by preset: key={args.Key} preset={InteractionPreset} " +
                $"state={_state} detail={closeDecision.Detail}");
            return true;
        }

        if (!closeDecision.ShouldClose || !OverlayInteractionPresets.ShouldCloseOnEscape(this))
        {
            Log.Debug(
                $"HandleKeyDown ignored by preset: key={args.Key} preset={InteractionPreset} " +
                $"state={_state} detail={closeDecision.Detail}");
            return false;
        }

        if (!CloseOnEscape)
            return false;

        Log.Debug($"HandleKeyDown: key={args.Key} state={_state} closeOnEscape={CloseOnEscape}");
        Close(reason);
        return true;
    }

    internal OverlayPointerBehavior GetAnchorClickBehavior(MouseButton button)
        => button == MouseButton.Right ? RightAnchorClickBehavior : LeftAnchorClickBehavior;

    public void SwitchAnchor(FrameworkElement? anchor)
    {
        Log.Debug($"SwitchAnchor: anchor={(anchor == null ? "null" : anchor.Name)} isOpen={IsOpen} state={_state}");
        var shouldTemporarilyHide = IsOpen && anchor != null && (_state == OverlayState.Open || _state == OverlayState.Opening);
        if (shouldTemporarilyHide)
            SetSurfaceVisibility(Visibility.Hidden);

        AnchorElement = anchor;

        if (IsOpen && anchor != null)
        {
            Reposition();
            UpdateLayout();
            if (shouldTemporarilyHide)
                SetSurfaceVisibility(Visibility.Visible);
        }
    }

    public void Reposition()
    {
        if (!IsLoaded || _surface == null || _positionHost == null || AnchorElement == null)
            return;

        if (_isRepositioning)
        {
            _repositionQueued = true;
            return;
        }

        _host ??= Parent as FrameworkElement;
        if (_host == null || !AnchorElement.IsLoaded)
            return;

        _isRepositioning = true;
        try
        {
            _surface.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desired = _surface.DesiredSize;
            if (desired.Width <= 0 || desired.Height <= 0)
                return;

            var transform = AnchorElement.TransformToAncestor(_host);
            var anchorTopLeft = transform.Transform(new Point(0, 0));
            var anchorRect = new Rect(anchorTopLeft, new Size(AnchorElement.ActualWidth, AnchorElement.ActualHeight));

            double hostWidth = _host.ActualWidth;
            double hostHeight = _host.ActualHeight;
            double left;
            double top;
            var requestedPlacement = Placement;
            var actualPlacement = requestedPlacement;

            switch (requestedPlacement)
            {
                case OverlayPlacement.LeftTop:
                    left = anchorRect.Left - desired.Width - HorizontalOffset;
                    top = anchorRect.Top + VerticalOffset;
                    break;
                case OverlayPlacement.BottomLeft:
                    left = anchorRect.Left + HorizontalOffset;
                    top = anchorRect.Bottom + VerticalOffset;
                    break;
                case OverlayPlacement.BottomCenter:
                    left = anchorRect.Left + (anchorRect.Width - desired.Width) / 2 + HorizontalOffset;
                    top = anchorRect.Bottom + VerticalOffset;
                    break;
                case OverlayPlacement.TopLeft:
                    left = anchorRect.Left + HorizontalOffset;
                    top = anchorRect.Top - desired.Height - VerticalOffset;
                    break;
                case OverlayPlacement.TopCenter:
                    left = anchorRect.Left + (anchorRect.Width - desired.Width) / 2 + HorizontalOffset;
                    top = anchorRect.Top - desired.Height - VerticalOffset;
                    break;
                case OverlayPlacement.RightTop:
                default:
                    left = anchorRect.Right + HorizontalOffset;
                    top = anchorRect.Top + VerticalOffset;
                    break;
            }

            if (requestedPlacement == OverlayPlacement.RightTop && left + desired.Width > hostWidth)
            {
                left = anchorRect.Left - desired.Width - HorizontalOffset;
                actualPlacement = OverlayPlacement.LeftTop;
            }

            if (requestedPlacement == OverlayPlacement.LeftTop && left < 0)
            {
                left = anchorRect.Right + HorizontalOffset;
                actualPlacement = OverlayPlacement.RightTop;
            }

            if (ConstrainToHostBounds)
            {
                left = Math.Max(0, Math.Min(left, Math.Max(0, hostWidth - desired.Width)));
                top = Math.Max(0, Math.Min(top, Math.Max(0, hostHeight - desired.Height)));
            }

            var nextMargin = new Thickness(left, top, 0, 0);
            if (!AreClose(SurfaceMargin, nextMargin))
            {
                Log.Debug(
                    $"Reposition: state={_state} requested={requestedPlacement} actual={actualPlacement} " +
                    $"anchorLeft={anchorRect.Left:F1} anchorTop={anchorRect.Top:F1} anchorWidth={anchorRect.Width:F1} anchorHeight={anchorRect.Height:F1} " +
                    $"hostWidth={hostWidth:F1} hostHeight={hostHeight:F1} desiredWidth={desired.Width:F1} desiredHeight={desired.Height:F1} " +
                    $"positionHostWidth={_positionHost.ActualWidth:F1} positionHostHeight={_positionHost.ActualHeight:F1} " +
                    $"left={left:F1} top={top:F1}");
                SurfaceMargin = nextMargin;
            }
        }
        finally
        {
            _isRepositioning = false;
        }

        if (_repositionQueued)
        {
            _repositionQueued = false;
            Dispatcher.BeginInvoke(new Action(Reposition));
        }
    }

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var overlay = (AnimatedOverlay)d;
        Log.Debug($"OnIsOpenChanged: newValue={e.NewValue}");
        if ((bool)e.NewValue)
            overlay.BeginOpen();
        else
            overlay.BeginClose();
    }

    private static void OnAnchorElementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var overlay = (AnimatedOverlay)d;
        overlay.DetachAnchor(e.OldValue as FrameworkElement);
        overlay.AttachAnchor(e.NewValue as FrameworkElement);
        overlay.Reposition();
    }

    private static void OnInteractionPresetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        OverlayInteractionPresets.ApplyPreset((AnimatedOverlay)d, (OverlayInteractionPreset)e.NewValue);
    }

    private void BeginOpen()
    {
        var version = ++_transitionVersion;
        Log.Debug($"BeginOpen: state={_state} version={version}");

        ResetSurfaceAnimations();
        SetSurfaceVisibility(Visibility.Hidden);
        if (_surface != null)
            _surface.Opacity = 0;

        Visibility = Visibility.Visible;
        ApplyTemplate();

        if (_positionHost == null)
        {
            _positionHost ??= GetTemplateChild("PART_PositionHost") as FrameworkElement;
        }

        if (_surface == null)
        {
            _surface ??= GetTemplateChild("PART_Surface") as FrameworkElement;
        }

        if (_positionHost == null || _surface == null)
        {
            if (_isOpenRetryPending)
                return;

            _isOpenRetryPending = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _isOpenRetryPending = false;
                if (IsOpen)
                    BeginOpen();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
            return;
        }

        _isOpenRetryPending = false;
        _host ??= Parent as FrameworkElement;
        if (_host == null || AnchorElement == null)
        {
            IsOpen = false;
            return;
        }

        SetState(OverlayState.Opening);
        Reposition();
        UpdateLayout();
        UpdateSurfaceInteractiveState();
        Log.Debug($"BeginOpen.AfterLayout: version={version} marginLeft={SurfaceMargin.Left:F1} marginTop={SurfaceMargin.Top:F1}");
        SetSurfaceVisibility(Visibility.Visible);

        var entrance = new EntranceEffect
        {
            Scale = new AnimationEffect { From = OpenScaleFrom, To = 1.0, DurationMs = OpenDurationMs, Easing = AnimationHelper.EaseOut },
            Opacity = new AnimationEffect { From = 0, To = 1, DurationMs = OpenDurationMs, Easing = AnimationHelper.EaseOut },
            Origin = AnimationOrigin,
        };

        AnimationHelper.ApplyEntrance(_surface, entrance, onCompleted: () =>
        {
            if (version != _transitionVersion)
            {
                Log.Debug($"BeginOpen.Completed ignored: staleVersion={version} currentVersion={_transitionVersion}");
                return;
            }

            if (!IsOpen)
                return;

            SetState(OverlayState.Open);
            UpdateSurfaceInteractiveState();
            Reposition();
            Log.Debug($"BeginOpen.Completed: version={version} marginLeft={SurfaceMargin.Left:F1} marginTop={SurfaceMargin.Top:F1}");
        });
    }

    private void BeginClose()
    {
        var version = ++_transitionVersion;
        Log.Debug($"BeginClose: state={_state} version={version}");
        if (_surface == null)
        {
            Visibility = Visibility.Collapsed;
            SetState(OverlayState.Closed);
            return;
        }

        if (_state == OverlayState.Closed)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        SetState(OverlayState.Closing);
        UpdateSurfaceInteractiveState();

        var exit = new ExitEffect
        {
            Scale = new AnimationEffect { From = 1.0, To = CloseScaleTo, DurationMs = CloseDurationMs, Easing = AnimationHelper.EaseIn },
            Opacity = new AnimationEffect { From = 1, To = 0, DurationMs = CloseDurationMs, Easing = AnimationHelper.EaseIn },
            Origin = AnimationOrigin,
        };

        AnimationHelper.ApplyExit(_surface, exit, () =>
        {
            if (version != _transitionVersion)
            {
                Log.Debug($"BeginClose.Completed ignored: staleVersion={version} currentVersion={_transitionVersion}");
                return;
            }

            if (IsOpen)
                return;

            SetSurfaceVisibility(Visibility.Visible);
            Visibility = Visibility.Collapsed;
            SetState(OverlayState.Closed);
            UpdateSurfaceInteractiveState();
            Log.Debug($"BeginClose.Completed: version={version}");
        });
    }

    private void UpdateSurfaceInteractiveState()
    {
        var interactive = _state == OverlayState.Open || _state == OverlayState.Opening;

        if (_surface != null)
            _surface.IsHitTestVisible = interactive;

        if (_positionHost != null)
            _positionHost.IsHitTestVisible = interactive;

        IsHitTestVisible = interactive;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _host = Parent as FrameworkElement;
        if (_host != null)
            _host.SizeChanged += OnHostSizeChanged;

        AttachWindow();
        Reposition();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachAnchor(AnchorElement);
        if (_host != null)
            _host.SizeChanged -= OnHostSizeChanged;
        _host = null;
        DetachWindow();
    }

    private void AttachAnchor(FrameworkElement? anchor)
    {
        if (anchor == null)
            return;

        anchor.SizeChanged += OnAnchorSizeChanged;
        anchor.Unloaded += OnAnchorUnloaded;
    }

    private void DetachAnchor(FrameworkElement? anchor)
    {
        if (anchor == null)
            return;

        anchor.SizeChanged -= OnAnchorSizeChanged;
        anchor.Unloaded -= OnAnchorUnloaded;
    }

    private void OnAnchorSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsOpen)
            Reposition();
    }

    private void OnHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsOpen)
            Reposition();
    }

    private void OnAnchorUnloaded(object sender, RoutedEventArgs e)
    {
        Close(OverlayCloseReason.AnchorUnavailable);
    }

    private void AttachWindow()
    {
        var window = Window.GetWindow(this);
        if (ReferenceEquals(_window, window))
            return;

        DetachWindow();
        _window = window;
        if (_window != null)
            _window.PreviewKeyDown += OnWindowPreviewKeyDown;
    }

    private void DetachWindow()
    {
        if (_window == null)
            return;

        _window.PreviewKeyDown -= OnWindowPreviewKeyDown;
        _window = null;
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (HandleKeyDown(e))
            e.Handled = true;
    }

    private void SetSurfaceVisibility(Visibility visibility)
    {
        if (_positionHost != null)
            _positionHost.Visibility = visibility;

        if (_surface != null)
            _surface.Visibility = visibility;
    }

    private void ResetSurfaceAnimations()
    {
        if (_surface == null)
            return;

        _surface.BeginAnimation(UIElement.OpacityProperty, null);
        if (TransformComposer.TryGetPrimaryScaleTransform(_surface, out var scale))
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }
    }

    private static string DescribeTarget(DependencyObject? target)
    {
        if (target == null)
            return "null";

        if (target is FrameworkElement frameworkElement)
        {
            var name = string.IsNullOrWhiteSpace(frameworkElement.Name) ? "-" : frameworkElement.Name;
            return $"{frameworkElement.GetType().Name}({name})";
        }

        return target.GetType().Name;
    }

    private void SetState(OverlayState state)
    {
        if (_state == state)
            return;

        _state = state;
        CurrentState = state.ToString();
        Log.Debug($"StateChanged: state={CurrentState}");

        switch (state)
        {
            case OverlayState.Open:
                OverlayCoordinator.Instance.RegisterOpenedOverlay(this);
                break;
            case OverlayState.Closed:
                OverlayCoordinator.Instance.RegisterClosedOverlay(this);
                break;
        }

        switch (state)
        {
            case OverlayState.Opening:
                Opening?.Invoke(this, EventArgs.Empty);
                break;
            case OverlayState.Open:
                Opened?.Invoke(this, EventArgs.Empty);
                break;
            case OverlayState.Closing:
                Closing?.Invoke(this, EventArgs.Empty);
                break;
            case OverlayState.Closed:
                Closed?.Invoke(this, new OverlayClosedEventArgs(LastCloseReason));
                break;
        }
    }

    private enum OverlayState
    {
        Closed,
        Opening,
        Open,
        Closing,
    }

    private static bool AreClose(Thickness a, Thickness b)
    {
        const double epsilon = 0.5;
        return Math.Abs(a.Left - b.Left) < epsilon &&
               Math.Abs(a.Top - b.Top) < epsilon &&
               Math.Abs(a.Right - b.Right) < epsilon &&
               Math.Abs(a.Bottom - b.Bottom) < epsilon;
    }
}
