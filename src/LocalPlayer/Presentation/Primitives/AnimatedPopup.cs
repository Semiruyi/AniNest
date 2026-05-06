using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using LocalPlayer.Presentation.Animations;
using LocalPlayer.Presentation.Interop;
using Point = System.Windows.Point;

namespace LocalPlayer.Presentation.Primitives;

/// <summary>
/// Popup with built-in scale+opacity entrance/exit animation,
/// outside-click-to-close, window-move tracking, and z-order fix.
/// Bind <see cref="IsOpenAnimated"/> instead of Popup.IsOpen.
/// </summary>
public class AnimatedPopup : Popup
{
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

    public bool IsOpenAnimated { get => (bool)GetValue(IsOpenAnimatedProperty); set => SetValue(IsOpenAnimatedProperty, value); }
    public Point OpenAnimationOrigin { get => (Point)GetValue(OpenAnimationOriginProperty); set => SetValue(OpenAnimationOriginProperty, value); }
    public int CloseDurationMs { get => (int)GetValue(CloseDurationMsProperty); set => SetValue(CloseDurationMsProperty, value); }
    public bool CloseOnOutsideClick { get => (bool)GetValue(CloseOnOutsideClickProperty); set => SetValue(CloseOnOutsideClickProperty, value); }
    public PopupPassthroughTargets OutsideClickPassthroughTargets
    {
        get => (PopupPassthroughTargets)GetValue(OutsideClickPassthroughTargetsProperty);
        set => SetValue(OutsideClickPassthroughTargetsProperty, value);
    }

    // ========== Static state ==========

    private sealed record WindowSubs(Window Window, EventHandler Move);
    private static readonly Dictionary<AnimatedPopup, WindowSubs> _windowSubs = new();

    // ========== Open / Close orchestration ==========

    private static void OnIsOpenAnimatedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var popup = (AnimatedPopup)d;
        if (popup.Child is not UIElement child) return;

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
            popup.Closed -= OnClosed;
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
        AnimationHelper.ApplyEntrance(child, entrance);
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

        var window = Window.GetWindow(popup.PlacementTarget ?? popup.Child)
                  ?? Application.Current?.MainWindow;
        if (window is null) return;

        PopupInputCoordinator.Instance.RegisterOpenedPopup(popup);
        var moveHandler = new EventHandler((_, _) =>
        {
            if (popup.IsOpen)
                typeof(Popup).GetMethod("Reposition", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(popup, null);
        });

        _windowSubs[popup] = new WindowSubs(window, moveHandler);
        window.LocationChanged += moveHandler;

        PopupZOrderFix.Apply(popup);
    }

    private static void OnClosed(object? sender, EventArgs e)
    {
        if (sender is AnimatedPopup popup)
        {
            PopupInputCoordinator.Instance.RegisterClosedPopup(popup);
            RemoveWindowSubs(popup);
        }
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

        IsOpenAnimated = false;
    }

    public bool AllowsPassthrough(PopupHitKind hitKind) => hitKind switch
    {
        PopupHitKind.VideoSurface => OutsideClickPassthroughTargets.HasFlag(PopupPassthroughTargets.VideoSurface),
        PopupHitKind.ControlBarGesture => OutsideClickPassthroughTargets.HasFlag(PopupPassthroughTargets.ControlBarGesture),
        PopupHitKind.DismissBackground => OutsideClickPassthroughTargets.HasFlag(PopupPassthroughTargets.DismissBackground),
        _ => false,
    };
}

