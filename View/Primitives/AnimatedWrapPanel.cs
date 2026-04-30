using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LocalPlayer.View.Primitives;

/// <summary>
/// 带布局动画的 WrapPanel。当子元素位置因增删改或窗口大小变化而改变时，
/// 会平滑过渡到新位置，而不是瞬间跳动。新子元素自动执行交错入场动画。
/// </summary>
public class AnimatedWrapPanel : WrapPanel
{
    public Duration AnimationDuration { get; set; } = new Duration(TimeSpan.FromMilliseconds(250));

    // Entrance animation
    public double EntranceFromScale { get; set; } = 0.88;
    public int EntranceScaleDurationMs { get; set; } = 420;
    public int EntranceOpacityDurationMs { get; set; } = 320;
    public int EntranceStaggerDelayMs { get; set; } = 35;

    private readonly HashSet<UIElement> _entranceDone = new();

    protected override Size ArrangeOverride(Size finalSize)
    {
        var entranceCandidates = new List<UIElement>();

        // 1. Record current visual positions
        var positions = new Dictionary<UIElement, Point>();
        foreach (UIElement child in InternalChildren)
        {
            if (child == null) continue;

            if (!_entranceDone.Contains(child))
                entranceCandidates.Add(child);

            try
            {
                positions[child] = child.TransformToAncestor(this).Transform(new Point(0, 0));
            }
            catch
            {
                positions[child] = new Point(0, 0);
            }

            if (child.RenderTransform is TranslateTransform oldTt)
            {
                oldTt.BeginAnimation(TranslateTransform.XProperty, null);
                oldTt.BeginAnimation(TranslateTransform.YProperty, null);
            }
            child.RenderTransform = null;
        }

        // 2. Standard arrange
        Size result = base.ArrangeOverride(finalSize);

        // 3. Position animation for existing children
        foreach (UIElement child in InternalChildren)
        {
            if (child == null || entranceCandidates.Contains(child))
                continue;
            if (!positions.TryGetValue(child, out Point oldVisualPos))
                continue;

            Point newLayoutPos;
            try
            {
                newLayoutPos = child.TransformToAncestor(this).Transform(new Point(0, 0));
            }
            catch
            {
                continue;
            }

            Vector offset = oldVisualPos - newLayoutPos;
            if (Math.Abs(offset.X) > 0.5 || Math.Abs(offset.Y) > 0.5)
                AnimateOffset(child, offset);
        }

        // 4. Entrance animation for new children
        if (entranceCandidates.Count > 0)
        {
            Dispatcher.BeginInvoke(() => AnimateEntrance(entranceCandidates),
                DispatcherPriority.Loaded);
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
                child.RenderTransform = null;
        };

        tt.BeginAnimation(TranslateTransform.XProperty, animX);
        tt.BeginAnimation(TranslateTransform.YProperty, animY);
    }

    private void AnimateEntrance(List<UIElement> children)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child == null || !InternalChildren.Contains(child)) continue;

            _entranceDone.Add(child);

            child.RenderTransformOrigin = new Point(0.5, 0.5);
            child.RenderTransform = new ScaleTransform(EntranceFromScale, EntranceFromScale);
            child.Opacity = 0;

            int delayMs = i * EntranceStaggerDelayMs;

            child.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(EntranceOpacityDurationMs))
                {
                    BeginTime = TimeSpan.FromMilliseconds(delayMs),
                    EasingFunction = ease
                });

            var scale = (ScaleTransform)child.RenderTransform;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(EntranceFromScale, 1.0, TimeSpan.FromMilliseconds(EntranceScaleDurationMs))
                {
                    BeginTime = TimeSpan.FromMilliseconds(delayMs),
                    EasingFunction = ease
                });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(EntranceFromScale, 1.0, TimeSpan.FromMilliseconds(EntranceScaleDurationMs))
                {
                    BeginTime = TimeSpan.FromMilliseconds(delayMs),
                    EasingFunction = ease
                });
        }
    }
}
