using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace AniNest.Presentation.Behaviors;

public static class MouseMoveBehavior
{
    public static readonly DependencyProperty MouseMoveCommandProperty =
        DependencyProperty.RegisterAttached("MouseMoveCommand", typeof(ICommand), typeof(MouseMoveBehavior),
            new PropertyMetadata(null, OnMouseMoveCommandChanged));

    public static ICommand GetMouseMoveCommand(DependencyObject o) => (ICommand)o.GetValue(MouseMoveCommandProperty);
    public static void SetMouseMoveCommand(DependencyObject o, ICommand v) => o.SetValue(MouseMoveCommandProperty, v);

    private static readonly HashSet<UIElement> _subscribed = new();

    private static void OnMouseMoveCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement el || !_subscribed.Add(el))
            return;

        el.MouseMove += OnMouseMove;
        if (el is FrameworkElement fe)
            fe.Unloaded += OnUnloaded;
    }

    private static void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not UIElement el)
            return;

        var cmd = GetMouseMoveCommand(el);
        if (cmd?.CanExecute(e) == true)
            cmd.Execute(e);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not UIElement el || !_subscribed.Remove(el))
            return;

        el.MouseMove -= OnMouseMove;
        if (el is FrameworkElement fe)
            fe.Unloaded -= OnUnloaded;
    }
}

