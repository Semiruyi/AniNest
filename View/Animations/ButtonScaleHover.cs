using System;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LocalPlayer.View.Animations;

/// <summary>
/// 按钮 hover/press 缩放动画附加行为。
/// 替代 ControlBarView 中重复的 4 个鼠标事件处理器 + AnimateScale 方法。
/// </summary>
public static class ButtonScaleHover
{
    /// <param name="button">目标按钮</param>
    /// <param name="scale">按钮模板中的 ScaleTransform（通常叫 AnimScale）</param>
    /// <param name="hoverScale">hover 时缩放倍数</param>
    /// <param name="pressScale">按下时缩放倍数</param>
    /// <param name="hoverEnterMs">hover 入场动画时长</param>
    /// <param name="hoverExitMs">hover 退场动画时长</param>
    /// <param name="pressMs">按下动画时长</param>
    /// <param name="releaseMs">松开动画时长</param>
    /// <param name="ease">缓动函数</param>
    public static void Attach(Button button, ScaleTransform scale,
        double hoverScale = 1.2, double pressScale = 0.85,
        int hoverEnterMs = 150, int hoverExitMs = 250,
        int pressMs = 130, int releaseMs = 280,
        IEasingFunction? ease = null)
    {
        var e = ease ?? new CubicBezierEase
        {
            X1 = 0.25, Y1 = 0.1, X2 = 0.25, Y2 = 1.0,
            EasingMode = EasingMode.EaseIn
        };

        button.MouseEnter += (_, _) =>
        {
            if (!button.IsPressed)
                AnimationHelper.AnimateScaleTransform(scale, hoverScale, hoverEnterMs, e);
        };

        button.MouseLeave += (_, _) =>
        {
            if (!button.IsPressed)
                AnimationHelper.AnimateScaleTransform(scale, 1.0, hoverExitMs, e);
        };

        button.PreviewMouseDown += (_, _) =>
        {
            AnimationHelper.AnimateScaleTransform(scale, pressScale, pressMs, e);
        };

        button.PreviewMouseUp += (_, _) =>
        {
            double target = button.IsMouseOver ? hoverScale : 1.0;
            AnimationHelper.AnimateScaleTransform(scale, target, releaseMs, e);
        };
    }
}
