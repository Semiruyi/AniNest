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
        if (d is not UIElement el || !_subscribed.Add(el))
            return;

        el.PreviewMouseMove += OnPreviewMouseMove;
        el.MouseLeave += OnMouseLeave;
        if (el is FrameworkElement fe)
            fe.Unloaded += OnUnloaded;
    }

    private static void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not UIElement el)
            return;

        var cmd = GetMouseMoveCommand(el);
        var pos = e.GetPosition(el);
        var param = (pos.Y, el.RenderSize.Height);
        if (cmd?.CanExecute(param) == true)
            cmd.Execute(param);
    }

    private static void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not UIElement el)
            return;

        var cmd = GetMouseLeaveCommand(el);
        if (cmd?.CanExecute(null) == true)
            cmd.Execute(null);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not UIElement el || !_subscribed.Remove(el))
            return;

        el.PreviewMouseMove -= OnPreviewMouseMove;
        el.MouseLeave -= OnMouseLeave;
        if (el is FrameworkElement fe)
            fe.Unloaded -= OnUnloaded;
    }
}

