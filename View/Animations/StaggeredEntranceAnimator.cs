using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LocalPlayer.View.Animations;

/// <summary>
/// 列表元素交错入场动画：scale + opacity，每项之间有 staggerDelay 延迟。
/// 替代 MainPage.Animations 和 PlaylistPanelView 中重复的入场动画逻辑。
/// </summary>
public static class StaggeredEntranceAnimator
{
    public static Task AnimateAsync(IEnumerable<FrameworkElement> elements,
        double fromScale = 0, int scaleDurationMs = 420,
        int opacityDurationMs = 320, int staggerDelayMs = 35,
        IEasingFunction? ease = null)
    {
        var e = ease ?? AnimationHelper.EaseOut;
        int i = 0;
        foreach (var el in elements)
        {
            int delayMs = i * staggerDelayMs;
            el.RenderTransformOrigin = new Point(0.5, 0.5);
            el.RenderTransform = new ScaleTransform(fromScale, fromScale);
            el.Opacity = 0;
            el.BeginAnimation(UIElement.OpacityProperty,
                AnimationHelper.CreateAnim(0, 1, opacityDurationMs, beginTimeMs: delayMs));
            var scale = (ScaleTransform)el.RenderTransform;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                AnimationHelper.CreateAnim(fromScale, 1.0, scaleDurationMs, e, delayMs));
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                AnimationHelper.CreateAnim(fromScale, 1.0, scaleDurationMs, e, delayMs));
            i++;
        }
        return Task.CompletedTask;
    }

    public static Task AnimateSingleAsync(FrameworkElement element,
        double fromScale = 0, int scaleDurationMs = 380,
        int opacityDurationMs = 300, IEasingFunction? ease = null)
    {
        var e = ease ?? AnimationHelper.EaseOut;
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = new ScaleTransform(fromScale, fromScale);
        element.Opacity = 0;
        element.BeginAnimation(UIElement.OpacityProperty,
            AnimationHelper.CreateAnim(0, 1, opacityDurationMs));
        var scale = (ScaleTransform)element.RenderTransform;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            AnimationHelper.CreateAnim(fromScale, 1.0, scaleDurationMs, e));
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,
            AnimationHelper.CreateAnim(fromScale, 1.0, scaleDurationMs, e));
        return Task.CompletedTask;
    }
}
