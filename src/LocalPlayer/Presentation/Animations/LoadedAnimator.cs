using System.Windows;

namespace AniNest.Presentation.Animations;

public static class LoadedAnimator
{
    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(LoadedAnimator),
            new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject o) => (bool)o.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject o, bool v) => o.SetValue(EnabledProperty, v);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement el)
            return;

        el.Loaded -= OnLoaded;
        if (e.NewValue is true)
        {
            el.Opacity = 0;
            el.Loaded += OnLoaded;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement el)
            return;

        AnimationHelper.ApplyEntrance(el, EntranceEffect.Default);
    }
}

