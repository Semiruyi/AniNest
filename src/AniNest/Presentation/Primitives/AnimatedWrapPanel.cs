using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AniNest.Presentation.Animations;

namespace AniNest.Presentation.Primitives;

public class AnimatedWrapPanel : WrapPanel
{
    private sealed record LayoutAnimation(
        UIElement Child,
        Vector Offset,
        bool IsEntrance,
        int Order);

    private sealed record LayoutPass(
        int Version,
        List<UIElement> Children,
        Dictionary<UIElement, Point> OldPositions);

    public static readonly DependencyProperty AnimationDurationProperty =
        DependencyProperty.Register(
            nameof(AnimationDuration),
            typeof(Duration),
            typeof(AnimatedWrapPanel),
            new FrameworkPropertyMetadata(new Duration(TimeSpan.FromMilliseconds(250)), OnAnimationSettingChanged));

    public static readonly DependencyProperty EntranceDurationProperty =
        DependencyProperty.Register(
            nameof(EntranceDuration),
            typeof(Duration),
            typeof(AnimatedWrapPanel),
            new FrameworkPropertyMetadata(new Duration(TimeSpan.FromMilliseconds(220)), OnAnimationSettingChanged));

    public static readonly DependencyProperty EntranceStaggerDelayMsProperty =
        DependencyProperty.Register(
            nameof(EntranceStaggerDelayMs),
            typeof(int),
            typeof(AnimatedWrapPanel),
            new FrameworkPropertyMetadata(35, OnAnimationSettingChanged));

    public static readonly DependencyProperty EntranceOffsetXProperty =
        DependencyProperty.Register(
            nameof(EntranceOffsetX),
            typeof(double),
            typeof(AnimatedWrapPanel),
            new FrameworkPropertyMetadata(0d, OnAnimationSettingChanged));

    public static readonly DependencyProperty EntranceOffsetYProperty =
        DependencyProperty.Register(
            nameof(EntranceOffsetY),
            typeof(double),
            typeof(AnimatedWrapPanel),
            new FrameworkPropertyMetadata(14d, OnAnimationSettingChanged));

    public static readonly DependencyProperty MovementThresholdProperty =
        DependencyProperty.Register(
            nameof(MovementThreshold),
            typeof(double),
            typeof(AnimatedWrapPanel),
            new FrameworkPropertyMetadata(0.5d, OnAnimationSettingChanged));

    public static readonly DependencyProperty AnimateEntranceProperty =
        DependencyProperty.Register(
            nameof(AnimateEntrance),
            typeof(bool),
            typeof(AnimatedWrapPanel),
            new FrameworkPropertyMetadata(true, OnAnimationSettingChanged));

    public static readonly DependencyProperty AnimateMovesProperty =
        DependencyProperty.Register(
            nameof(AnimateMoves),
            typeof(bool),
            typeof(AnimatedWrapPanel),
            new FrameworkPropertyMetadata(true, OnAnimationSettingChanged));

    private static readonly DependencyProperty LayoutTranslateTransformProperty =
        DependencyProperty.RegisterAttached(
            "LayoutTranslateTransform",
            typeof(TranslateTransform),
            typeof(AnimatedWrapPanel),
            new PropertyMetadata(null));

    public Duration AnimationDuration
    {
        get => (Duration)GetValue(AnimationDurationProperty);
        set => SetValue(AnimationDurationProperty, value);
    }

    public Duration EntranceDuration
    {
        get => (Duration)GetValue(EntranceDurationProperty);
        set => SetValue(EntranceDurationProperty, value);
    }

    public int EntranceStaggerDelayMs
    {
        get => (int)GetValue(EntranceStaggerDelayMsProperty);
        set => SetValue(EntranceStaggerDelayMsProperty, value);
    }

    public double EntranceOffsetX
    {
        get => (double)GetValue(EntranceOffsetXProperty);
        set => SetValue(EntranceOffsetXProperty, value);
    }

    public double EntranceOffsetY
    {
        get => (double)GetValue(EntranceOffsetYProperty);
        set => SetValue(EntranceOffsetYProperty, value);
    }

    public double MovementThreshold
    {
        get => (double)GetValue(MovementThresholdProperty);
        set => SetValue(MovementThresholdProperty, value);
    }

    public bool AnimateEntrance
    {
        get => (bool)GetValue(AnimateEntranceProperty);
        set => SetValue(AnimateEntranceProperty, value);
    }

    public bool AnimateMoves
    {
        get => (bool)GetValue(AnimateMovesProperty);
        set => SetValue(AnimateMovesProperty, value);
    }

    private bool _isLoaded;
    private int _layoutVersion;

    public AnimatedWrapPanel()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private static void OnAnimationSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnimatedWrapPanel panel)
            panel.InvalidateArrange();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = CollectChildren();
        if (children.Count == 0)
            return base.ArrangeOverride(finalSize);

        var pass = BeginLayoutPass(children);
        var result = base.ArrangeOverride(finalSize);
        QueueAnimations(pass, CaptureVisualPositions(children));

        return result;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        _layoutVersion++;
    }

    private List<UIElement> CollectChildren()
    {
        var children = new List<UIElement>(InternalChildren.Count);
        foreach (UIElement? child in InternalChildren)
        {
            if (child != null)
                children.Add(child);
        }

        return children;
    }

    private LayoutPass BeginLayoutPass(List<UIElement> children)
    {
        int version = ++_layoutVersion;
        var oldPositions = CaptureVisualPositions(children);
        ResetLayoutTransforms(children);
        return new LayoutPass(version, children, oldPositions);
    }

    private void QueueAnimations(LayoutPass pass, Dictionary<UIElement, Point> newPositions)
    {
        if (!_isLoaded)
            return;

        var animations = BuildAnimations(pass.Children, pass.OldPositions, newPositions);
        if (animations.Count == 0)
            return;

        var liveChildren = new HashSet<UIElement>(pass.Children);
        Dispatcher.BeginInvoke(
            new Action(() => ApplyAnimations(pass.Version, animations, liveChildren)),
            DispatcherPriority.Loaded);
    }

    private Dictionary<UIElement, Point> CaptureVisualPositions(IReadOnlyList<UIElement> children)
    {
        var positions = new Dictionary<UIElement, Point>(children.Count);

        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (TryGetVisualTopLeft(child, out var position))
                positions[child] = position;
        }

        return positions;
    }

    private List<LayoutAnimation> BuildAnimations(
        IReadOnlyList<UIElement> children,
        IReadOnlyDictionary<UIElement, Point> oldPositions,
        IReadOnlyDictionary<UIElement, Point> newPositions)
    {
        var animations = new List<LayoutAnimation>(children.Count);

        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (!newPositions.TryGetValue(child, out var newPosition))
                continue;

            if (!oldPositions.TryGetValue(child, out var oldPosition))
            {
                if (AnimateEntrance)
                {
                    animations.Add(new LayoutAnimation(
                        child,
                        new Vector(EntranceOffsetX, EntranceOffsetY),
                        IsEntrance: true,
                        Order: i));
                }

                continue;
            }

            if (!AnimateMoves)
                continue;

            var offset = oldPosition - newPosition;
            if (Math.Abs(offset.X) <= MovementThreshold && Math.Abs(offset.Y) <= MovementThreshold)
                continue;

            animations.Add(new LayoutAnimation(
                child,
                offset,
                IsEntrance: false,
                Order: i));
        }

        return animations;
    }

    private void ApplyAnimations(int version, IReadOnlyList<LayoutAnimation> animations, ISet<UIElement> liveChildren)
    {
        if (version != _layoutVersion)
            return;

        for (int i = 0; i < animations.Count; i++)
        {
            var animation = animations[i];
            if (!liveChildren.Contains(animation.Child))
                continue;

            var translate = EnsureLayoutTranslateTransform(animation.Child);
            int durationMs = ResolveDurationMs(animation.IsEntrance ? EntranceDuration : AnimationDuration);

            translate.BeginAnimation(TranslateTransform.XProperty, null);
            translate.BeginAnimation(TranslateTransform.YProperty, null);
            translate.X = animation.Offset.X;
            translate.Y = animation.Offset.Y;

            int beginTimeMs = animation.IsEntrance ? animation.Order * EntranceStaggerDelayMs : 0;

            translate.BeginAnimation(
                TranslateTransform.XProperty,
                AnimationHelper.CreateAnim(animation.Offset.X, 0, durationMs, AnimationHelper.EaseOut, beginTimeMs));
            translate.BeginAnimation(
                TranslateTransform.YProperty,
                AnimationHelper.CreateAnim(animation.Offset.Y, 0, durationMs, AnimationHelper.EaseOut, beginTimeMs));
        }
    }

    private void ResetLayoutTransforms(IReadOnlyList<UIElement> children)
    {
        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child.ReadLocalValue(LayoutTranslateTransformProperty) is not TranslateTransform translate)
                continue;

            translate.BeginAnimation(TranslateTransform.XProperty, null);
            translate.BeginAnimation(TranslateTransform.YProperty, null);
            translate.X = 0;
            translate.Y = 0;
        }
    }

    private TranslateTransform EnsureLayoutTranslateTransform(UIElement child)
    {
        if (child.ReadLocalValue(LayoutTranslateTransformProperty) is TranslateTransform existing)
            return existing;

        var translate = new TranslateTransform();
        var current = child.RenderTransform;

        if (current == null || ReferenceEquals(current, Transform.Identity))
        {
            child.RenderTransform = translate;
        }
        else if (current is TransformGroup currentGroup)
        {
            var clonedGroup = currentGroup.CloneCurrentValue();
            clonedGroup.Children.Add(translate);
            child.RenderTransform = clonedGroup;
        }
        else
        {
            var group = new TransformGroup();
            group.Children.Add(current);
            group.Children.Add(translate);
            child.RenderTransform = group;
        }

        child.SetValue(LayoutTranslateTransformProperty, translate);
        return translate;
    }

    private bool TryGetVisualTopLeft(UIElement child, out Point position)
    {
        try
        {
            position = child.TransformToAncestor(this).Transform(new Point(0, 0));
            return true;
        }
        catch (InvalidOperationException)
        {
            position = default;
            return false;
        }
    }

    private static int ResolveDurationMs(Duration preferred)
    {
        if (!preferred.HasTimeSpan)
            return 0;

        return (int)Math.Round(preferred.TimeSpan.TotalMilliseconds);
    }
}
