using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LocalPlayer.View.Primitives;

/// <summary>
/// 带布局动画的 WrapPanel。当子元素位置因增删改或窗口大小变化而改变时，
/// 会平滑过渡到新位置，而不是瞬间跳动。
/// </summary>
public class AnimatedWrapPanel : WrapPanel
{
    /// <summary>
    /// 位置过渡动画时长。
    /// </summary>
    public Duration AnimationDuration { get; set; } = new Duration(TimeSpan.FromMilliseconds(250));

    protected override System.Windows.Size ArrangeOverride(System.Windows.Size finalSize)
    {
        // 1. 记录当前视觉位置（包含正在进行的动画偏移）
        var positions = new System.Collections.Generic.Dictionary<UIElement, System.Windows.Point>();
        foreach (UIElement child in InternalChildren)
        {
            if (child == null) continue;
            try
            {
                positions[child] = child.TransformToAncestor(this).Transform(new System.Windows.Point(0, 0));
            }
            catch
            {
                positions[child] = new System.Windows.Point(0, 0);
            }

            // 暂停并清除旧的动画/Transform，避免干扰本次 Arrange 后的位置计算
            if (child.RenderTransform is TranslateTransform oldTt)
            {
                oldTt.BeginAnimation(TranslateTransform.XProperty, null);
                oldTt.BeginAnimation(TranslateTransform.YProperty, null);
            }
            child.RenderTransform = null;
        }

        // 2. 执行标准 Arrange
        System.Windows.Size result = base.ArrangeOverride(finalSize);

        // 3. 计算位移并播放动画
        foreach (UIElement child in InternalChildren)
        {
            if (child == null || !positions.TryGetValue(child, out System.Windows.Point oldVisualPos))
                continue;

            System.Windows.Point newLayoutPos;
            try
            {
                newLayoutPos = child.TransformToAncestor(this).Transform(new System.Windows.Point(0, 0));
            }
            catch
            {
                continue;
            }

            Vector offset = oldVisualPos - newLayoutPos;
            if (Math.Abs(offset.X) > 0.5 || Math.Abs(offset.Y) > 0.5)
            {
                AnimateOffset(child, offset);
            }
        }

        return result;
    }

    private void AnimateOffset(UIElement child, Vector initialOffset)
    {
        var tt = new TranslateTransform(initialOffset.X, initialOffset.Y);
        child.RenderTransform = tt;

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        var animX = new DoubleAnimation(initialOffset.X, 0.0, AnimationDuration)
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd
        };
        var animY = new DoubleAnimation(initialOffset.Y, 0.0, AnimationDuration)
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd
        };

        animX.Completed += (_, _) =>
        {
            if (tt != child.RenderTransform) return;
            if (Math.Abs(tt.X) < 0.01 && Math.Abs(tt.Y) < 0.01)
            {
                child.RenderTransform = null;
            }
        };

        tt.BeginAnimation(TranslateTransform.XProperty, animX);
        tt.BeginAnimation(TranslateTransform.YProperty, animY);
    }
}
