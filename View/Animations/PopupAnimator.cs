using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LocalPlayer.Model;

namespace LocalPlayer.View.Animations;

/// <summary>
/// 弹窗 scale+opacity 显隐动画，替代 PauseOverlayController / SpeedPopupController / ThumbnailPreviewController 中重复的动画逻辑。
/// </summary>
public class PopupAnimator
{
    private readonly UIElement _element;
    private readonly Point _origin;
    private readonly ExitEffect _exitEffect;

    public PopupAnimator(UIElement element,
        double hideToScale = 0, int hideDurationMs = 180,
        IEasingFunction? hideEase = null, Point? origin = null)
    {
        _element = element;
        _origin = origin ?? EntranceEffect.Default.Origin;
        _exitEffect = new ExitEffect
        {
            Scale = new AnimationEffect { From = 1.0, To = hideToScale, DurationMs = hideDurationMs, Easing = hideEase ?? AnimationHelper.EaseIn },
            Opacity = new AnimationEffect { From = 1, To = 0, DurationMs = hideDurationMs, Easing = hideEase ?? AnimationHelper.EaseIn },
            Origin = _origin,
        };
    }

    public void Show()
    {
        var entrance = new EntranceEffect
        {
            Scale = EntranceEffect.Default.Scale,
            Opacity = EntranceEffect.Default.Opacity,
            Origin = _origin,
        };
        AnimationHelper.ApplyEntrance(_element, entrance);
    }

    public void Hide(Action? onCompleted = null)
    {
        AnimationHelper.ApplyExit(_element, _exitEffect, onCompleted);
    }

    public void ShowImmediate()
    {
        if (_element.RenderTransform is ScaleTransform scale)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scale.ScaleX = 1;
            scale.ScaleY = 1;
        }
        _element.BeginAnimation(UIElement.OpacityProperty, null);
        _element.Opacity = 1;
    }

    // ========== 附加属性：将 bool 绑定到显隐动画 ==========

    public static bool GetBindVisible(DependencyObject obj) => (bool)obj.GetValue(BindVisibleProperty);
    public static void SetBindVisible(DependencyObject obj, bool value) => obj.SetValue(BindVisibleProperty, value);

    public static readonly DependencyProperty BindVisibleProperty =
        DependencyProperty.RegisterAttached("BindVisible", typeof(bool), typeof(PopupAnimator),
            new PropertyMetadata(false, OnBindVisibleChanged));

    private static void OnBindVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;

        var animator = new PopupAnimator(element);
        if ((bool)e.NewValue)
            animator.Show();
        else
            animator.Hide();
    }

    // ========== 附加属性：将 bool 绑定到 Popup 显隐（带动画 + 外部点击关闭） ==========

    public static bool GetBindOpen(DependencyObject obj) => (bool)obj.GetValue(BindOpenProperty);
    public static void SetBindOpen(DependencyObject obj, bool value) => obj.SetValue(BindOpenProperty, value);

    public static readonly DependencyProperty BindOpenProperty =
        DependencyProperty.RegisterAttached("BindOpen", typeof(bool), typeof(PopupAnimator),
            new PropertyMetadata(false, OnBindOpenChanged));

    public static Point GetBindOpenOrigin(DependencyObject obj) => (Point)obj.GetValue(BindOpenOriginProperty);
    public static void SetBindOpenOrigin(DependencyObject obj, Point value) => obj.SetValue(BindOpenOriginProperty, value);

    public static readonly DependencyProperty BindOpenOriginProperty =
        DependencyProperty.RegisterAttached("BindOpenOrigin", typeof(Point), typeof(PopupAnimator),
            new PropertyMetadata(new Point(0.5, 0.5)));

    private static readonly Dictionary<Popup, (Window window, MouseButtonEventHandler handler)> _outsideSubs = new();

    private static void OnBindOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Popup popup) return;
        if (popup.Child is not UIElement child) return;

        var origin = GetBindOpenOrigin(popup);
        var animator = new PopupAnimator(child, origin: origin);

        if ((bool)e.NewValue)
        {
            animator.Show();
            popup.Opened -= OnPopupOpened;
            popup.Opened += OnPopupOpened;
            popup.Closed -= OnPopupClosed;
            popup.Closed += OnPopupClosed;
            popup.IsOpen = true;
        }
        else
        {
            popup.Opened -= OnPopupOpened;
            popup.Closed -= OnPopupClosed;
            RemoveOutsideHandler(popup);
            animator.Hide(() =>
            {
                if (!GetBindOpen(popup))
                    popup.IsOpen = false;
            });
        }
    }

    private static void OnPopupOpened(object? sender, EventArgs e)
    {
        if (sender is not Popup popup) return;
        if (_outsideSubs.ContainsKey(popup)) return;

        var window = Window.GetWindow(popup.PlacementTarget ?? popup.Child)
                  ?? Application.Current?.MainWindow;
        if (window is null)
        {
            AppLog.Debug("PopupAnimator", "OnPopupOpened: 无法获取 Window");
            return;
        }

        AppLog.Debug("PopupAnimator", $"订阅 {popup.Name} 外部点击");

        MouseButtonEventHandler handler = null!;
        handler = (_, args) =>
        {
            if (!popup.IsOpen) return;

            var target = args.OriginalSource as DependencyObject;
            if (target is null) return;

            if (popup.Child is UIElement child && child.IsAncestorOf(target))
                return;

            if (popup.PlacementTarget is UIElement trigger && trigger.IsAncestorOf(target))
                return;

            AppLog.Debug("PopupAnimator", $"外部点击，关闭 {popup.Name}");

            var expr = popup.GetBindingExpression(BindOpenProperty);
            if (expr?.DataItem is not null)
            {
                var path = expr.ParentBinding.Path.Path;
                var prop = expr.DataItem.GetType().GetProperty(path);
                if (prop is not null && prop.CanWrite)
                    prop.SetValue(expr.DataItem, false);
                else
                    AppLog.Debug("PopupAnimator", $"反射失败: path={path}");
            }
            else
                AppLog.Debug("PopupAnimator", "BindingExpression 不可用");
        };

        _outsideSubs[popup] = (window, handler);
        window.PreviewMouseLeftButtonDown += handler;
    }

    private static void OnPopupClosed(object? sender, EventArgs e)
    {
        if (sender is Popup popup)
            RemoveOutsideHandler(popup);
    }

    private static void RemoveOutsideHandler(Popup popup)
    {
        if (_outsideSubs.TryGetValue(popup, out var sub))
        {
            sub.window.PreviewMouseLeftButtonDown -= sub.handler;
            _outsideSubs.Remove(popup);
        }
    }
}
