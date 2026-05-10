using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using AniNest.Infrastructure.Logging;
using AniNest.Presentation.Animations;
using AniNest.Presentation.Interop;
using Point = System.Windows.Point;

namespace AniNest.Presentation.Primitives;

public enum AnimatedPopupPlacementPreset
{
    None,
    Relative,
    Bottom,
    Top,
    Left,
    Right,
    BottomCenter,
    TopCenter,
    LeftCenter,
    RightCenter
}

/// <summary>
/// Presentation-focused popup with built-in scale+opacity entrance/exit animation.
///
/// Intended scope:
/// - lightweight non-interactive popup visuals such as hover previews and tooltips
/// - popup-hosted content that should continue using WPF Popup semantics
/// - presentation-first surfaces where native popup behavior is more important than
///   unified interaction semantics
///
/// Not intended for:
/// - command menus
/// - settings panels
/// - context menus
/// - other interaction-heavy floating surfaces
///
/// Those interaction-heavy surfaces should use <see cref="AniNest.Presentation.Overlays.AnimatedOverlay"/>
/// and <see cref="AniNest.Presentation.Overlays.OverlayCoordinator"/>.
///
/// Bind <see cref="IsOpenAnimated"/> instead of Popup.IsOpen.
/// </summary>
[ContentProperty(nameof(Content))]
public class AnimatedPopup : Popup
{
    private static readonly Logger Log = AppLog.For(nameof(AnimatedPopup));
    private readonly PopupRoot _popupRoot;
    private readonly AnimationLayer _animationLayer;

    public AnimatedPopup? ParentPopup { get; internal set; }

    // ========== Dependency Properties ==========

    public static readonly DependencyProperty IsOpenAnimatedProperty =
        DependencyProperty.Register(nameof(IsOpenAnimated), typeof(bool), typeof(AnimatedPopup),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenAnimatedChanged));

    public static readonly DependencyProperty OpenAnimationOriginProperty =
        DependencyProperty.Register(nameof(OpenAnimationOrigin), typeof(Point), typeof(AnimatedPopup),
            new PropertyMetadata(new Point(0.5, 0.5)));

    public static readonly DependencyProperty CloseDurationMsProperty =
        DependencyProperty.Register(nameof(CloseDurationMs), typeof(int), typeof(AnimatedPopup),
            new PropertyMetadata(180));

    public static readonly DependencyProperty CloseOnOutsideClickProperty =
        DependencyProperty.Register(nameof(CloseOnOutsideClick), typeof(bool), typeof(AnimatedPopup),
            new PropertyMetadata(true));

    public static readonly DependencyProperty OutsideClickPassthroughTargetsProperty =
        DependencyProperty.Register(nameof(OutsideClickPassthroughTargets), typeof(PopupPassthroughTargets), typeof(AnimatedPopup),
            new PropertyMetadata(PopupPassthroughTargets.None));

    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(nameof(Content), typeof(UIElement), typeof(AnimatedPopup),
            new PropertyMetadata(null, OnContentChanged));

    public static readonly DependencyProperty PlacementPresetProperty =
        DependencyProperty.Register(nameof(PlacementPreset), typeof(AnimatedPopupPlacementPreset), typeof(AnimatedPopup),
            new PropertyMetadata(AnimatedPopupPlacementPreset.None, OnPlacementPresetChanged));

    public bool IsOpenAnimated { get => (bool)GetValue(IsOpenAnimatedProperty); set => SetValue(IsOpenAnimatedProperty, value); }
    public Point OpenAnimationOrigin { get => (Point)GetValue(OpenAnimationOriginProperty); set => SetValue(OpenAnimationOriginProperty, value); }
    public int CloseDurationMs { get => (int)GetValue(CloseDurationMsProperty); set => SetValue(CloseDurationMsProperty, value); }
    public bool CloseOnOutsideClick { get => (bool)GetValue(CloseOnOutsideClickProperty); set => SetValue(CloseOnOutsideClickProperty, value); }
    public UIElement? Content { get => (UIElement?)GetValue(ContentProperty); set => SetValue(ContentProperty, value); }
    public AnimatedPopupPlacementPreset PlacementPreset
    {
        get => (AnimatedPopupPlacementPreset)GetValue(PlacementPresetProperty);
        set => SetValue(PlacementPresetProperty, value);
    }
    public PopupPassthroughTargets OutsideClickPassthroughTargets
    {
        get => (PopupPassthroughTargets)GetValue(OutsideClickPassthroughTargetsProperty);
        set => SetValue(OutsideClickPassthroughTargetsProperty, value);
    }

    // ========== Static state ==========

    private static readonly MethodInfo? RepositionMethod =
        typeof(Popup).GetMethod("Reposition", BindingFlags.Instance | BindingFlags.NonPublic);

    private sealed record WindowSubs(Window Window, EventHandler Move);
    private static readonly Dictionary<AnimatedPopup, WindowSubs> _windowSubs = new();
    private sealed class PopupRoot : Border;
    private sealed class AnimationLayer : Border;

    public AnimatedPopup()
    {
        _animationLayer = new AnimationLayer
        {
            Background = Brushes.Transparent
        };
        _popupRoot = new PopupRoot
        {
            Background = Brushes.Transparent,
            Child = _animationLayer
        };
        base.Child = _popupRoot;
        Unloaded += OnUnloaded;
        UpdatePopupPlacementCallback();
    }

    // ========== Open / Close orchestration ==========

    private static void OnIsOpenAnimatedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var popup = (AnimatedPopup)d;
        var child = popup.GetAnimationTarget();
        if (child is null) return;

        Log.Debug($"IsOpenAnimatedChanged: popup={popup.GetHashCode()} newValue={e.NewValue} isOpen={popup.IsOpen}");

        if ((bool)e.NewValue)
        {
            child.Opacity = 0;
            popup.Opened -= OnOpened;
            popup.Opened += OnOpened;
            popup.Closed -= OnClosed;
            popup.Closed += OnClosed;
            popup.IsOpen = true;
            child.Dispatcher.BeginInvoke(new Action(() => PlayEntrance(popup, child)), DispatcherPriority.Loaded);
        }
        else
        {
            popup.Opened -= OnOpened;
            PopupInputCoordinator.Instance.RegisterClosedPopup(popup);
            RemoveWindowSubs(popup);
            PlayExit(popup, child, () =>
            {
                if (!popup.IsOpenAnimated)
                {
                    popup.IsOpen = false;
                    child.RenderTransform = Transform.Identity;
                }
            });
        }
    }

    // ========== Animation ==========

    private static void PlayEntrance(AnimatedPopup popup, UIElement child)
    {
        var entrance = new EntranceEffect
        {
            Scale = EntranceEffect.Default.Scale,
            Opacity = EntranceEffect.Default.Opacity,
            Origin = popup.OpenAnimationOrigin,
        };
        AnimationHelper.ApplyEntrance(child, entrance, onCompleted: () => ForceReposition(popup));
    }

    private static void PlayExit(AnimatedPopup popup, UIElement child, Action onCompleted)
    {
        var exit = new ExitEffect
        {
            Scale = new AnimationEffect { From = 1.0, To = 0, DurationMs = popup.CloseDurationMs, Easing = AnimationHelper.EaseIn },
            Opacity = new AnimationEffect { From = 1, To = 0, DurationMs = popup.CloseDurationMs, Easing = AnimationHelper.EaseIn },
            Origin = popup.OpenAnimationOrigin,
        };
        AnimationHelper.ApplyExit(child, exit, onCompleted);
    }

    // ========== Window events ==========

    private static void OnOpened(object? sender, EventArgs e)
    {
        if (sender is not AnimatedPopup popup) return;
        if (_windowSubs.ContainsKey(popup)) return;

        Log.Debug($"Opened: popup={popup.GetHashCode()}");

        var window = Window.GetWindow(popup.PlacementTarget ?? popup.Child)
                  ?? Application.Current?.MainWindow;
        if (window is null) return;

        PopupInputCoordinator.Instance.RegisterOpenedPopup(popup);
        var moveHandler = new EventHandler((_, _) =>
        {
            if (popup.IsOpen)
                ForceReposition(popup);
        });

        _windowSubs[popup] = new WindowSubs(window, moveHandler);
        window.LocationChanged += moveHandler;

        PopupZOrderFix.Apply(popup);
    }

    private static void OnClosed(object? sender, EventArgs e)
    {
        if (sender is AnimatedPopup popup)
        {
            Log.Debug($"Closed: popup={popup.GetHashCode()}");
            PopupInputCoordinator.Instance.RegisterClosedPopup(popup);
            RemoveWindowSubs(popup);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Opened -= OnOpened;
        Closed -= OnClosed;

        if (IsOpen)
            IsOpen = false;

        IsOpenAnimated = false;
        PopupInputCoordinator.Instance.RegisterClosedPopup(this);
        RemoveWindowSubs(this);
        _animationLayer.Child = null;
    }

    private static void RemoveWindowSubs(AnimatedPopup popup)
    {
        if (_windowSubs.TryGetValue(popup, out var sub))
        {
            sub.Window.LocationChanged -= sub.Move;
            _windowSubs.Remove(popup);
        }
    }

    public void RequestClose(PopupCloseReason reason)
    {
        if (!IsOpenAnimated)
            return;

        Log.Debug($"RequestClose: popup={GetHashCode()} reason={reason}");
        IsOpenAnimated = false;
    }

    public bool AllowsPassthrough(PopupHitKind hitKind) => hitKind switch
    {
        PopupHitKind.VideoSurface => OutsideClickPassthroughTargets.HasFlag(PopupPassthroughTargets.VideoSurface),
        _ => false,
    };

    private static void ForceReposition(AnimatedPopup popup)
    {
        RepositionMethod?.Invoke(popup, null);
    }

    private UIElement? GetAnimationTarget()
    {
        return _animationLayer;
    }

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var popup = (AnimatedPopup)d;
        popup._animationLayer.Child = e.NewValue as UIElement;
    }

    private static void OnPlacementPresetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var popup = (AnimatedPopup)d;
        popup.UpdatePopupPlacementCallback();
        if (popup.IsOpen)
            ForceReposition(popup);
    }

    private void UpdatePopupPlacementCallback()
    {
        switch (PlacementPreset)
        {
            case AnimatedPopupPlacementPreset.None:
                CustomPopupPlacementCallback = null;
                return;
            case AnimatedPopupPlacementPreset.Relative:
                CustomPopupPlacementCallback = null;
                Placement = PlacementMode.Relative;
                return;
            case AnimatedPopupPlacementPreset.Bottom:
                CustomPopupPlacementCallback = null;
                Placement = PlacementMode.Bottom;
                return;
            case AnimatedPopupPlacementPreset.Top:
                CustomPopupPlacementCallback = null;
                Placement = PlacementMode.Top;
                return;
            case AnimatedPopupPlacementPreset.Left:
                CustomPopupPlacementCallback = null;
                Placement = PlacementMode.Left;
                return;
            case AnimatedPopupPlacementPreset.Right:
                CustomPopupPlacementCallback = null;
                Placement = PlacementMode.Right;
                return;
        }

        Placement = PlacementMode.Custom;
        CustomPopupPlacementCallback = PlaceUsingPreset;
    }

    private CustomPopupPlacement[] PlaceUsingPreset(Size popupSize, Size targetSize, Point offset)
    {
        return PlacementPreset switch
        {
            AnimatedPopupPlacementPreset.BottomCenter =>
                [new CustomPopupPlacement(new Point((targetSize.Width - popupSize.Width) / 2d + offset.X, targetSize.Height + offset.Y), PopupPrimaryAxis.Horizontal)],
            AnimatedPopupPlacementPreset.TopCenter =>
                [new CustomPopupPlacement(new Point((targetSize.Width - popupSize.Width) / 2d + offset.X, -popupSize.Height + offset.Y), PopupPrimaryAxis.Horizontal)],
            AnimatedPopupPlacementPreset.LeftCenter =>
                [new CustomPopupPlacement(new Point(-popupSize.Width + offset.X, (targetSize.Height - popupSize.Height) / 2d + offset.Y), PopupPrimaryAxis.Vertical)],
            AnimatedPopupPlacementPreset.RightCenter =>
                [new CustomPopupPlacement(new Point(targetSize.Width + offset.X, (targetSize.Height - popupSize.Height) / 2d + offset.Y), PopupPrimaryAxis.Vertical)],
            _ => []
        };
    }
}

