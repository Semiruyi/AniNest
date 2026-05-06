using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace LocalPlayer.Presentation.Behaviors;

public static class MouseTrackBehavior
{
    public static readonly DependencyProperty MouseMoveCommandProperty =
        DependencyProperty.RegisterAttached("MouseMoveCommand", typeof(ICommand), typeof(MouseTrackBehavior),
            new PropertyMetadata(null, OnPropertyChanged));

    public static readonly DependencyProperty MouseLeaveCommandProperty =
        DependencyProperty.RegisterAttached("MouseLeaveCommand", typeof(ICommand), typeof(MouseTrackBehavior),
            new PropertyMetadata(null, OnPropertyChanged));

    public static ICommand GetMouseMoveCommand(DependencyObject o) => (ICommand)o.GetValue(MouseMoveCommandProperty);
    public static void SetMouseMoveCommand(DependencyObject o, ICommand v) => o.SetValue(MouseMoveCommandProperty, v);
    public static ICommand GetMouseLeaveCommand(DependencyObject o) => (ICommand)o.GetValue(MouseLeaveCommandProperty);
    public static void SetMouseLeaveCommand(DependencyObject o, ICommand v) => o.SetValue(MouseLeaveCommandProperty, v);

    private static readonly HashSet<UIElement> _subscribed = new();

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement el && _subscribed.Add(el))
        {
            el.PreviewMouseMove += (_, args) =>
            {
                var cmd = GetMouseMoveCommand(el);
                var pos = args.GetPosition(el);
                var param = (pos.Y, el.RenderSize.Height);
                if (cmd?.CanExecute(param) == true) cmd.Execute(param);
            };
            el.MouseLeave += (_, _) =>
            {
                var cmd = GetMouseLeaveCommand(el);
                if (cmd?.CanExecute(null) == true) cmd.Execute(null);
            };
        }
    }
}

