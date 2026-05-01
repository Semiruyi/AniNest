using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LocalPlayer.View.Animations;

/// <summary>
/// 弹窗 scale+opacity 显隐动画，替代 PauseOverlayController / SpeedPopupController / ThumbnailPreviewController 中重复的动画逻辑。
/// </summary>
public class PopupAnimator
{
    private readonly UIElement _element;
    private readonly ExitEffect _exitEffect;

    public PopupAnimator(UIElement element,
        double hideToScale = 0, int hideDurationMs = 180,
        IEasingFunction? hideEase = null)
    {
        _element = element;
        _exitEffect = new ExitEffect
        {
            Scale = new AnimationEffect { From = 1.0, To = hideToScale, DurationMs = hideDurationMs, Easing = hideEase ?? AnimationHelper.EaseIn },
            Opacity = new AnimationEffect { From = 1, To = 0, DurationMs = hideDurationMs, Easing = hideEase ?? AnimationHelper.EaseIn },
            Origin = ExitEffect.Default.Origin,
        };
    }

    public void Show()
    {
        AnimationHelper.ApplyEntrance(_element, EntranceEffect.Default);
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

    // ========== 附加属性：将 bool 绑定到 Popup 显隐（带动画） ==========

    public static bool GetBindOpen(DependencyObject obj) => (bool)obj.GetValue(BindOpenProperty);
    public static void SetBindOpen(DependencyObject obj, bool value) => obj.SetValue(BindOpenProperty, value);

    public static readonly DependencyProperty BindOpenProperty =
        DependencyProperty.RegisterAttached("BindOpen", typeof(bool), typeof(PopupAnimator),
            new PropertyMetadata(false, OnBindOpenChanged));

    private static void OnBindOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Popup popup) return;
        if (popup.Child is not UIElement child) return;

        var animator = new PopupAnimator(child);

        if ((bool)e.NewValue)
        {
            popup.IsOpen = true;
            popup.Dispatcher.BeginInvoke(() => animator.Show(), DispatcherPriority.Loaded);
        }
        else
        {
            animator.Hide(() => popup.IsOpen = false);
        }
    }
}
