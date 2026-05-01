using System.Windows;
using System.Windows.Media.Animation;

namespace LocalPlayer.View.Animations;

public static class LoadedAnimator
{
    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(LoadedAnimator),
            new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject o) => (bool)o.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject o, bool v) => o.SetValue(EnabledProperty, v);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement el || e.NewValue is not true) return;
        el.Opacity = 0;
        el.Loaded += (_, _) =>
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
            var anim = new DoubleAnimation(0, 1, System.TimeSpan.FromMilliseconds(300)) { EasingFunction = ease };
            anim.Completed += (_, _) =>
            {
                el.BeginAnimation(UIElement.OpacityProperty, null);
                el.Opacity = 1;
            };
            el.BeginAnimation(UIElement.OpacityProperty, anim);
        };
    }
}
