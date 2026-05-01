using System.Windows;
using System.Windows.Input;

namespace LocalPlayer.View.Behaviors;

public static class LoadedCommandBehavior
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached("Command", typeof(ICommand), typeof(LoadedCommandBehavior),
            new PropertyMetadata(null, OnLoadedChanged));

    public static readonly DependencyProperty FocusOnLoadedProperty =
        DependencyProperty.RegisterAttached("FocusOnLoaded", typeof(bool), typeof(LoadedCommandBehavior),
            new PropertyMetadata(false, OnLoadedChanged));

    public static ICommand? GetCommand(DependencyObject o) => (ICommand?)o.GetValue(CommandProperty);
    public static void SetCommand(DependencyObject o, ICommand? v) => o.SetValue(CommandProperty, v);

    public static bool GetFocusOnLoaded(DependencyObject o) => (bool)o.GetValue(FocusOnLoadedProperty);
    public static void SetFocusOnLoaded(DependencyObject o, bool v) => o.SetValue(FocusOnLoadedProperty, v);

    private static void OnLoadedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement el) return;
        el.Loaded -= OnElementLoaded;
        el.Loaded += OnElementLoaded;
    }

    private static void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement el) return;

        GetCommand(el)?.Execute(null);

        if (GetFocusOnLoaded(el))
        {
            Keyboard.Focus(el);
            FocusManager.SetFocusedElement(el, el);
        }
    }
}
