using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AniNest.Presentation.Animations;

public enum ScaleFadeVisibilityPreset
{
    Default,
    Compact,
    Badge,
    Emphasis
}

public static class ScaleFadeVisibilityAnimator
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(ScaleFadeVisibilityAnimator),
            new PropertyMetadata(false, OnConfigurationChanged));

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.RegisterAttached("IsActive", typeof(bool), typeof(ScaleFadeVisibilityAnimator),
            new PropertyMetadata(false, OnStateChanged));

    public static readonly DependencyProperty VisibilityStateProperty =
        DependencyProperty.RegisterAttached("VisibilityState", typeof(Visibility), typeof(ScaleFadeVisibilityAnimator),
            new PropertyMetadata(Visibility.Collapsed, OnVisibilityStateChanged));

    public static readonly DependencyProperty FromScaleProperty =
        DependencyProperty.RegisterAttached("FromScale", typeof(double), typeof(ScaleFadeVisibilityAnimator),
            new PropertyMetadata(0.84, OnConfigurationChanged));

    public static readonly DependencyProperty EnterDurationMsProperty =
        DependencyProperty.RegisterAttached("EnterDurationMs", typeof(int), typeof(ScaleFadeVisibilityAnimator),
            new PropertyMetadata(240, OnConfigurationChanged));

    public static readonly DependencyProperty ExitDurationMsProperty =
        DependencyProperty.RegisterAttached("ExitDurationMs", typeof(int), typeof(ScaleFadeVisibilityAnimator),
            new PropertyMetadata(180, OnConfigurationChanged));

    public static readonly DependencyProperty CollapseWhenInactiveProperty =
        DependencyProperty.RegisterAttached("CollapseWhenInactive", typeof(bool), typeof(ScaleFadeVisibilityAnimator),
            new PropertyMetadata(true, OnConfigurationChanged));

    public static readonly DependencyProperty EnterEaseProperty =
        DependencyProperty.RegisterAttached("EnterEase", typeof(IEasingFunction), typeof(ScaleFadeVisibilityAnimator),
            new PropertyMetadata(AnimationHelper.EaseOut));

    public static readonly DependencyProperty ExitEaseProperty =
        DependencyProperty.RegisterAttached("ExitEase", typeof(IEasingFunction), typeof(ScaleFadeVisibilityAnimator),
            new PropertyMetadata(AnimationHelper.EaseIn));

    public static readonly DependencyProperty PresetProperty =
        DependencyProperty.RegisterAttached("Preset", typeof(ScaleFadeVisibilityPreset), typeof(ScaleFadeVisibilityAnimator),
            new PropertyMetadata(ScaleFadeVisibilityPreset.Default, OnConfigurationChanged));

    private static readonly DependencyProperty AnimationVersionProperty =
        DependencyProperty.RegisterAttached("AnimationVersion", typeof(int), typeof(ScaleFadeVisibilityAnimator),
            new PropertyMetadata(0));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    public static bool GetIsActive(DependencyObject obj) => (bool)obj.GetValue(IsActiveProperty);
    public static void SetIsActive(DependencyObject obj, bool value) => obj.SetValue(IsActiveProperty, value);

    public static Visibility GetVisibilityState(DependencyObject obj) => (Visibility)obj.GetValue(VisibilityStateProperty);
    public static void SetVisibilityState(DependencyObject obj, Visibility value) => obj.SetValue(VisibilityStateProperty, value);

    public static double GetFromScale(DependencyObject obj) => (double)obj.GetValue(FromScaleProperty);
    public static void SetFromScale(DependencyObject obj, double value) => obj.SetValue(FromScaleProperty, value);

    public static int GetEnterDurationMs(DependencyObject obj) => (int)obj.GetValue(EnterDurationMsProperty);
    public static void SetEnterDurationMs(DependencyObject obj, int value) => obj.SetValue(EnterDurationMsProperty, value);

    public static int GetExitDurationMs(DependencyObject obj) => (int)obj.GetValue(ExitDurationMsProperty);
    public static void SetExitDurationMs(DependencyObject obj, int value) => obj.SetValue(ExitDurationMsProperty, value);

    public static bool GetCollapseWhenInactive(DependencyObject obj) => (bool)obj.GetValue(CollapseWhenInactiveProperty);
    public static void SetCollapseWhenInactive(DependencyObject obj, bool value) => obj.SetValue(CollapseWhenInactiveProperty, value);

    public static IEasingFunction GetEnterEase(DependencyObject obj) => (IEasingFunction)obj.GetValue(EnterEaseProperty);
    public static void SetEnterEase(DependencyObject obj, IEasingFunction value) => obj.SetValue(EnterEaseProperty, value);

    public static IEasingFunction GetExitEase(DependencyObject obj) => (IEasingFunction)obj.GetValue(ExitEaseProperty);
    public static void SetExitEase(DependencyObject obj, IEasingFunction value) => obj.SetValue(ExitEaseProperty, value);

    public static ScaleFadeVisibilityPreset GetPreset(DependencyObject obj) => (ScaleFadeVisibilityPreset)obj.GetValue(PresetProperty);
    public static void SetPreset(DependencyObject obj, ScaleFadeVisibilityPreset value) => obj.SetValue(PresetProperty, value);

    private static void OnVisibilityStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;

        SetIsActive(element, (Visibility)e.NewValue == Visibility.Visible);
    }

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || !GetIsEnabled(element))
            return;

        AnimateState(element, (bool)e.NewValue);
    }

    private static void OnConfigurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;

        if (!GetIsEnabled(element))
            return;

        SnapState(element, GetIsActive(element));
    }

    private static void AnimateState(FrameworkElement element, bool isActive)
    {
        IncrementVersion(element);
        StopCurrentAnimations(element);

        if (isActive)
        {
            element.Visibility = Visibility.Visible;
            element.IsHitTestVisible = true;
            AnimationHelper.ApplyEntrance(element, BuildEntranceEffect(element));
            return;
        }

        element.IsHitTestVisible = false;
        int version = GetAnimationVersion(element);
        AnimationHelper.ApplyExit(element, BuildExitEffect(element), () =>
        {
            if (GetAnimationVersion(element) != version)
                return;

            if (GetCollapseWhenInactive(element))
                element.Visibility = Visibility.Collapsed;
            element.Opacity = 0;
        });
    }

    private static void SnapState(FrameworkElement element, bool isActive)
    {
        StopCurrentAnimations(element);

        double scaleValue = isActive ? 1.0 : GetFromScale(element);
        var scale = EnsureScaleTransform(element);
        scale.ScaleX = scaleValue;
        scale.ScaleY = scaleValue;

        element.Opacity = isActive ? 1 : 0;
        element.IsHitTestVisible = isActive;
        element.Visibility = isActive
            ? Visibility.Visible
            : GetCollapseWhenInactive(element) ? Visibility.Collapsed : Visibility.Visible;
    }

    private static EntranceEffect BuildEntranceEffect(FrameworkElement element)
        => new()
        {
            Scale = new AnimationEffect
            {
                From = ResolveFromScale(element),
                To = 1,
                DurationMs = ResolveEnterDurationMs(element),
                Easing = ResolveEnterEase(element)
            },
            Opacity = new AnimationEffect
            {
                From = 0,
                To = 1,
                DurationMs = ResolveEnterDurationMs(element),
                Easing = ResolveEnterEase(element)
            },
            Origin = new Point(0.5, 0.5)
        };

    private static ExitEffect BuildExitEffect(FrameworkElement element)
        => new()
        {
            Scale = new AnimationEffect
            {
                From = 1,
                To = ResolveFromScale(element),
                DurationMs = ResolveExitDurationMs(element),
                Easing = ResolveExitEase(element)
            },
            Opacity = new AnimationEffect
            {
                From = element.Opacity,
                To = 0,
                DurationMs = ResolveExitDurationMs(element),
                Easing = ResolveExitEase(element)
            },
            Origin = new Point(0.5, 0.5)
        };

    private static double ResolveFromScale(FrameworkElement element)
        => GetPreset(element) switch
        {
            ScaleFadeVisibilityPreset.Compact => 0.0,
            ScaleFadeVisibilityPreset.Badge => 0.84,
            ScaleFadeVisibilityPreset.Emphasis => 0.78,
            _ => GetFromScale(element)
        };

    private static int ResolveEnterDurationMs(FrameworkElement element)
        => GetPreset(element) switch
        {
            ScaleFadeVisibilityPreset.Compact => 200,
            ScaleFadeVisibilityPreset.Badge => 240,
            ScaleFadeVisibilityPreset.Emphasis => 260,
            _ => GetEnterDurationMs(element)
        };

    private static int ResolveExitDurationMs(FrameworkElement element)
        => GetPreset(element) switch
        {
            ScaleFadeVisibilityPreset.Compact => 160,
            ScaleFadeVisibilityPreset.Badge => 180,
            ScaleFadeVisibilityPreset.Emphasis => 200,
            _ => GetExitDurationMs(element)
        };

    private static IEasingFunction ResolveEnterEase(FrameworkElement element)
        => GetPreset(element) switch
        {
            ScaleFadeVisibilityPreset.Compact => AnimationHelper.EaseOut,
            ScaleFadeVisibilityPreset.Badge => AnimationHelper.EaseOut,
            ScaleFadeVisibilityPreset.Emphasis => AnimationHelper.EaseOut,
            _ => GetEnterEase(element)
        };

    private static IEasingFunction ResolveExitEase(FrameworkElement element)
        => GetPreset(element) switch
        {
            ScaleFadeVisibilityPreset.Compact => AnimationHelper.EaseIn,
            ScaleFadeVisibilityPreset.Badge => AnimationHelper.EaseIn,
            ScaleFadeVisibilityPreset.Emphasis => AnimationHelper.EaseIn,
            _ => GetExitEase(element)
        };

    private static ScaleTransform EnsureScaleTransform(FrameworkElement element)
    {
        return AnimationHelper.GetScaleTransform(element);
    }

    private static void StopCurrentAnimations(FrameworkElement element)
    {
        double currentOpacity = element.Opacity;
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.Opacity = currentOpacity;

        var scale = EnsureScaleTransform(element);
        double currentScaleX = scale.ScaleX;
        double currentScaleY = scale.ScaleY;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        scale.ScaleX = currentScaleX;
        scale.ScaleY = currentScaleY;
    }

    private static void IncrementVersion(DependencyObject element)
        => element.SetValue(AnimationVersionProperty, GetAnimationVersion(element) + 1);

    private static int GetAnimationVersion(DependencyObject element)
        => (int)element.GetValue(AnimationVersionProperty);
}
