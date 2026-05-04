using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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

    private static readonly Dictionary<Popup, (Window window, MouseButtonEventHandler downHandler, MouseButtonEventHandler upHandler)> _outsideSubs = new();
    private static readonly HashSet<Popup> _closedByOutsideClick = new();

    /// <summary>映射 Popup.Child → Popup，用于检测嵌套 Popup 的点击归属。</summary>
    private static readonly Dictionary<UIElement, Popup> _childToPopup = new();

    private static void OnBindOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Popup popup) return;
        if (popup.Child is not UIElement child) return;

        var origin = GetBindOpenOrigin(popup);
        var animator = new PopupAnimator(child, origin: origin);

        if ((bool)e.NewValue)
        {
            // 仅设 Opacity=0 防闪烁，不碰 RenderTransform，避免干扰首次 Popup 定位
            child.Opacity = 0;

            popup.Opened -= OnPopupOpened;
            popup.Opened += OnPopupOpened;
            popup.Closed -= OnPopupClosed;
            popup.Closed += OnPopupClosed;
            popup.IsOpen = true;

            // 延迟到 Popup 完成布局后再启动动画，避免 ScaleTransform(0,0) 干扰首次窗口尺寸计算
            child.Dispatcher.BeginInvoke(new Action(() => animator.Show()), DispatcherPriority.Loaded);
        }
        else
        {
            popup.Opened -= OnPopupOpened;
            popup.Closed -= OnPopupClosed;

            // 外部点击关闭时延迟取消订阅，让 upHandler 拦截 PreviewMouseLeftButtonUp
            if (!_closedByOutsideClick.Contains(popup))
                RemoveOutsideHandler(popup);

            animator.Hide(() =>
            {
                if (!GetBindOpen(popup))
                {
                    popup.IsOpen = false;
                    // 仅在确认关闭时才重置，避免覆盖正在进行的入场动画
                    child.RenderTransform = Transform.Identity;
                }
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

        // 将 PopupRoot 映射到 Popup，用于嵌套检测
        if (popup.Child is UIElement child)
        {
            var rootVisual = PresentationSource.FromDependencyObject(child)?.RootVisual;
            if (rootVisual is UIElement root)
                _childToPopup[root] = popup;
        }

        MouseButtonEventHandler downHandler = null!;
        MouseButtonEventHandler upHandler = null!;

        bool IsOutsideClick(MouseButtonEventArgs args)
        {
            var target = args.OriginalSource as DependencyObject;
            if (target is null) return false;

            if (popup.Child is UIElement child && child.IsAncestorOf(target)) return false;
            if (popup.PlacementTarget is UIElement trigger && trigger.IsAncestorOf(target)) return false;

            var targetSource = PresentationSource.FromDependencyObject(target);
            if (targetSource?.RootVisual is UIElement rootVisual &&
                _childToPopup.TryGetValue(rootVisual, out var targetPopup) &&
                targetPopup != popup &&
                IsPopupNestedInside(popup, targetPopup))
                return false;

            return true;
        }

        downHandler = (_, args) =>
        {
            if (!popup.IsOpen) return;
            if (!IsOutsideClick(args)) return;

            AppLog.Debug("PopupAnimator", $"外部点击▼ 关闭 {popup.Name}");
            args.Handled = true;
            _closedByOutsideClick.Add(popup);

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

        upHandler = (_, args) =>
        {
            if (_closedByOutsideClick.Remove(popup))
            {
                args.Handled = true;
                RemoveOutsideHandler(popup);
            }
        };

        _outsideSubs[popup] = (window, downHandler, upHandler);
        window.PreviewMouseLeftButtonDown += downHandler;
        window.PreviewMouseLeftButtonUp += upHandler;
    }

    /// <summary>沿逻辑树向上查找，判断 child 是否是 parent 的后代 Popup。</summary>
    private static bool IsPopupNestedInside(Popup parent, Popup child)
    {
        DependencyObject? current = child;
        while (current != null)
        {
            if (current == parent)
                return true;
            current = LogicalTreeHelper.GetParent(current);
        }
        return false;
    }

    private static void OnPopupClosed(object? sender, EventArgs e)
    {
        if (sender is Popup popup)
        {
            var stale = _childToPopup.Where(kv => kv.Value == popup).Select(kv => kv.Key).ToList();
            foreach (var key in stale)
                _childToPopup.Remove(key);
            RemoveOutsideHandler(popup);
        }
    }

    private static void RemoveOutsideHandler(Popup popup)
    {
        if (_outsideSubs.TryGetValue(popup, out var sub))
        {
            sub.window.PreviewMouseLeftButtonDown -= sub.downHandler;
            sub.window.PreviewMouseLeftButtonUp -= sub.upHandler;
            _outsideSubs.Remove(popup);
        }
        _closedByOutsideClick.Remove(popup);
    }
}
