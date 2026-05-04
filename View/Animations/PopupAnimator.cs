using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LocalPlayer.View.Animations;

/// <summary>
/// UIElement scale+opacity 显隐动画。
/// 对于 Popup 的完整动画管理，请使用 <see cref="View.Primitives.AnimatedPopup"/>。
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
}
