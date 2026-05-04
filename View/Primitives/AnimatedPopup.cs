using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LocalPlayer.View.Animations;
using LocalPlayer.View.Interop;

namespace LocalPlayer.View.Primitives;

/// <summary>
/// Popup with built-in scale+opacity entrance/exit animation,
/// outside-click-to-close, window-move tracking, and z-order fix.
/// Bind <see cref="IsOpenAnimated"/> instead of Popup.IsOpen.
/// </summary>
public class AnimatedPopup : Popup
{
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

    public bool IsOpenAnimated { get => (bool)GetValue(IsOpenAnimatedProperty); set => SetValue(IsOpenAnimatedProperty, value); }
    public Point OpenAnimationOrigin { get => (Point)GetValue(OpenAnimationOriginProperty); set => SetValue(OpenAnimationOriginProperty, value); }
    public int CloseDurationMs { get => (int)GetValue(CloseDurationMsProperty); set => SetValue(CloseDurationMsProperty, value); }
    public bool CloseOnOutsideClick { get => (bool)GetValue(CloseOnOutsideClickProperty); set => SetValue(CloseOnOutsideClickProperty, value); }

    // ========== Static state ==========

    private sealed record WindowSubs(Window Window, MouseButtonEventHandler? Down, MouseButtonEventHandler? Up, EventHandler Move);
    private static readonly Dictionary<AnimatedPopup, WindowSubs> _windowSubs = new();
    private static readonly HashSet<AnimatedPopup> _closedByOutsideClick = new();
    private static readonly Dictionary<UIElement, AnimatedPopup> _rootToPopup = new();

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
            if (!_closedByOutsideClick.Contains(popup))
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

        if (popup.Child is UIElement child)
        {
            var root = PresentationSource.FromDependencyObject(child)?.RootVisual;
            if (root is UIElement r)
                _rootToPopup[r] = popup;
        }

        var downHandler = CreateDownHandler(popup);
        var upHandler = downHandler != null ? CreateUpHandler(popup) : null;
        var moveHandler = new EventHandler((_, _) =>
        {
            if (popup.IsOpen)
                typeof(Popup).GetMethod("Reposition", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(popup, null);
        });

        _windowSubs[popup] = new WindowSubs(window, downHandler!, upHandler!, moveHandler);
        if (downHandler != null)
        {
            window.PreviewMouseLeftButtonDown += downHandler;
            window.PreviewMouseLeftButtonUp += upHandler!;
        }
        window.LocationChanged += moveHandler;

        PopupZOrderFix.Apply(popup);
    }

    private static MouseButtonEventHandler? CreateDownHandler(AnimatedPopup popup)
    {
        if (!popup.CloseOnOutsideClick) return null;
        return (_, args) =>
        {
            if (!popup.IsOpen) return;
            if (!IsOutsideClick(popup, args)) return;

            args.Handled = true;
            _closedByOutsideClick.Add(popup);
            popup.IsOpenAnimated = false;
        };
    }

    private static MouseButtonEventHandler CreateUpHandler(AnimatedPopup popup) => (_, args) =>
    {
        if (_closedByOutsideClick.Remove(popup))
        {
            args.Handled = true;
            RemoveWindowSubs(popup);
        }
    };

    private static bool IsOutsideClick(AnimatedPopup popup, MouseButtonEventArgs args)
    {
        if (args.OriginalSource is not DependencyObject target) return false;
        if (popup.Child is UIElement child && child.IsAncestorOf(target)) return false;
        if (popup.PlacementTarget is UIElement trigger && trigger.IsAncestorOf(target)) return false;

        var source = PresentationSource.FromDependencyObject(target);
        if (source?.RootVisual is UIElement root &&
            _rootToPopup.TryGetValue(root, out var targetPopup) &&
            targetPopup != popup &&
            IsNestedInside(popup, targetPopup))
            return false;

        return true;
    }

    private static bool IsNestedInside(Popup parent, Popup child)
    {
        DependencyObject? cur = child;
        while (cur != null)
        {
            if (cur == parent) return true;
            cur = LogicalTreeHelper.GetParent(cur);
        }
        return false;
    }

    private static void OnClosed(object? sender, EventArgs e)
    {
        if (sender is AnimatedPopup popup)
        {
            var stale = _rootToPopup.Where(kv => kv.Value == popup).Select(kv => kv.Key).ToList();
            foreach (var k in stale) _rootToPopup.Remove(k);
            RemoveWindowSubs(popup);
        }
    }

    private static void RemoveWindowSubs(AnimatedPopup popup)
    {
        if (_windowSubs.TryGetValue(popup, out var sub))
        {
            if (sub.Down != null) sub.Window.PreviewMouseLeftButtonDown -= sub.Down;
            if (sub.Up != null) sub.Window.PreviewMouseLeftButtonUp -= sub.Up;
            sub.Window.LocationChanged -= sub.Move;
            _windowSubs.Remove(popup);
        }
        _closedByOutsideClick.Remove(popup);
    }
}
