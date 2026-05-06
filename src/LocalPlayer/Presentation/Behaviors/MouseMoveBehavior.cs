using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace LocalPlayer.Presentation.Behaviors;

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
        if (d is UIElement el && _subscribed.Add(el))
        {
            el.MouseMove += (_, args) =>
            {
                var cmd = GetMouseMoveCommand(el);
                if (cmd?.CanExecute(args) == true) cmd.Execute(args);
            };
        }
    }
}

