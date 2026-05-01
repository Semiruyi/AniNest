using System.Windows;
using System.Windows.Input;

namespace LocalPlayer.View.Behaviors;

public static class FocusOnLoadedBehavior
{
    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(FocusOnLoadedBehavior),
            new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject o) => (bool)o.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject o, bool v) => o.SetValue(EnabledProperty, v);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement el || e.NewValue is not true) return;
        el.Loaded += (_, _) =>
        {
            Keyboard.Focus(el);
            FocusManager.SetFocusedElement(el, el);
        };
    }
}
