using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LocalPlayer.View.Animations;

/// <summary>
/// 弹窗 scale+opacity 显隐动画，替代 PauseOverlayController / SpeedPopupController / ThumbnailPreviewController 中重复的动画逻辑。
/// </summary>
public class PopupAnimator
{
    private readonly ScaleTransform _scale;
    private readonly UIElement _element;
    private readonly double _showFromScale;
    private readonly double _showToScale;
    private readonly int _showDurationMs;
    private readonly double _hideToScale;
    private readonly int _hideDurationMs;
    private readonly IEasingFunction _showEase;
    private readonly IEasingFunction _hideEase;

    public PopupAnimator(ScaleTransform scale, UIElement element,
        double showFromScale = 0, double showToScale = 1.0, int showDurationMs = 250,
        double hideToScale = 0, int hideDurationMs = 180,
        IEasingFunction? showEase = null, IEasingFunction? hideEase = null)
    {
        _scale = scale;
        _element = element;
        _showFromScale = showFromScale;
        _showToScale = showToScale;
        _showDurationMs = showDurationMs;
        _hideToScale = hideToScale;
        _hideDurationMs = hideDurationMs;
        _showEase = showEase ?? AnimationHelper.EaseOut;
        _hideEase = hideEase ?? AnimationHelper.EaseIn;
    }

    public void Show()
    {
        _scale.ScaleX = _showFromScale;
        _scale.ScaleY = _showFromScale;
        _element.Opacity = 0;
        AnimationHelper.AnimateScaleTransform(_scale, _showToScale, _showDurationMs, _showEase);
        AnimationHelper.Animate(_element, UIElement.OpacityProperty, 0, 1, _showDurationMs, _showEase);
    }

    public void Hide(Action? onCompleted = null)
    {
        AnimationHelper.AnimateScaleTransform(_scale, _hideToScale, _hideDurationMs, _hideEase);
        AnimationHelper.AnimateFromCurrent(_element, UIElement.OpacityProperty, 0, _hideDurationMs, _hideEase, onCompleted);
    }

    public void ShowImmediate()
    {
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _element.BeginAnimation(UIElement.OpacityProperty, null);
        _scale.ScaleX = 1;
        _scale.ScaleY = 1;
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

        var scale = element.RenderTransform as ScaleTransform
                    ?? (element.RenderTransform as TransformGroup)?.Children
                        .OfType<ScaleTransform>().FirstOrDefault();
        if (scale == null) return;

        var animator = new PopupAnimator(scale, element);
        if ((bool)e.NewValue)
            animator.Show();
        else
            animator.Hide();
    }
}
