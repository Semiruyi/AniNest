using System.Windows;

namespace LocalPlayer.Presentation.Animations;

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
            AnimationHelper.ApplyEntrance(el, EntranceEffect.Default);
        };
    }
}

