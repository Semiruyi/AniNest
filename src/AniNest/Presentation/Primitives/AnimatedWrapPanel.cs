using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AniNest.Presentation.Animations;
using AniNest.Infrastructure.Diagnostics;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace AniNest.Presentation.Primitives;

public class AnimatedWrapPanel : WrapPanel
{
    public Duration AnimationDuration { get; set; } = new Duration(TimeSpan.FromMilliseconds(250));

    public int EntranceStaggerDelayMs { get; set; } = 35;

    private readonly HashSet<UIElement> _entranceDone = new();

    protected override Size ArrangeOverride(Size finalSize)
    {
        using var arrangeSpan = PerfSpan.Begin("AnimatedWrapPanel.ArrangeOverride", new Dictionary<string, string>
        {
            ["childCount"] = InternalChildren.Count.ToString(),
            ["finalWidth"] = finalSize.Width.ToString("F2"),
            ["finalHeight"] = finalSize.Height.ToString("F2")
        });

        var entranceCandidates = new List<UIElement>();

        // 1. Record current visual positions
        var positions = new Dictionary<UIElement, Point>();
        using (PerfSpan.Begin("AnimatedWrapPanel.RecordPositions", new Dictionary<string, string>
        {
            ["childCount"] = InternalChildren.Count.ToString()
        }))
        {
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
        }

        // 2. Standard arrange
        Size result;
        using (PerfSpan.Begin("AnimatedWrapPanel.BaseArrange", new Dictionary<string, string>
        {
            ["childCount"] = InternalChildren.Count.ToString()
        }))
        {
            result = base.ArrangeOverride(finalSize);
        }

        // 3. Position animation for existing children
        int animatedOffsetCount = 0;
        using (PerfSpan.Begin("AnimatedWrapPanel.AnimateExistingOffsets", new Dictionary<string, string>
        {
            ["childCount"] = InternalChildren.Count.ToString(),
            ["entranceCandidateCount"] = entranceCandidates.Count.ToString()
        }))
        {
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
                {
                    animatedOffsetCount++;
                    AnimateOffset(child, offset);
                }
            }
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
        using var span = PerfSpan.Begin("AnimatedWrapPanel.AnimateEntrance", new Dictionary<string, string>
        {
            ["childCount"] = children.Count.ToString()
        });

        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child == null || !InternalChildren.Contains(child)) continue;

            _entranceDone.Add(child);
            AnimationHelper.ApplyEntrance(child, EntranceEffect.Default, i * EntranceStaggerDelayMs);
        }
    }
}

