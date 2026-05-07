using System.Windows;
using System.Windows.Media;

namespace AniNest.Presentation.Animations;

public static class SlideAnimator
{
    public static readonly DependencyProperty TargetYProperty =
        DependencyProperty.RegisterAttached("TargetY", typeof(double), typeof(SlideAnimator),
            new PropertyMetadata(0.0, OnTargetYChanged));

    public static double GetTargetY(DependencyObject obj) => (double)obj.GetValue(TargetYProperty);
    public static void SetTargetY(DependencyObject obj, double value) => obj.SetValue(TargetYProperty, value);

    public static readonly DependencyProperty DurationMsProperty =
        DependencyProperty.RegisterAttached("DurationMs", typeof(int), typeof(SlideAnimator),
            new PropertyMetadata(300));

    public static int GetDurationMs(DependencyObject obj) => (int)obj.GetValue(DurationMsProperty);
    public static void SetDurationMs(DependencyObject obj, int value) => obj.SetValue(DurationMsProperty, value);

    private static void OnTargetYChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;
        if (element.RenderTransform is not TranslateTransform tt) return;

        var duration = GetDurationMs(d);
        AnimationHelper.AnimateFromCurrent(tt, TranslateTransform.YProperty,
            (double)e.NewValue, duration, AnimationHelper.EaseInOut);
    }
}

