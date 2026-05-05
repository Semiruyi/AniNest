using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace LocalPlayer.Presentation.Behaviors;

public static class HoverBehavior
{
    public static readonly DependencyProperty MouseEnterCommandProperty =
        DependencyProperty.RegisterAttached("MouseEnterCommand", typeof(ICommand), typeof(HoverBehavior),
            new PropertyMetadata(null, OnPropertyChanged));

    public static readonly DependencyProperty MouseLeaveCommandProperty =
        DependencyProperty.RegisterAttached("MouseLeaveCommand", typeof(ICommand), typeof(HoverBehavior),
            new PropertyMetadata(null, OnPropertyChanged));

    public static readonly DependencyProperty IgnoreChildLeaveProperty =
        DependencyProperty.RegisterAttached("IgnoreChildLeave", typeof(bool), typeof(HoverBehavior),
            new PropertyMetadata(false));

    public static ICommand GetMouseEnterCommand(DependencyObject o) => (ICommand)o.GetValue(MouseEnterCommandProperty);
    public static void SetMouseEnterCommand(DependencyObject o, ICommand v) => o.SetValue(MouseEnterCommandProperty, v);
    public static ICommand GetMouseLeaveCommand(DependencyObject o) => (ICommand)o.GetValue(MouseLeaveCommandProperty);
    public static void SetMouseLeaveCommand(DependencyObject o, ICommand v) => o.SetValue(MouseLeaveCommandProperty, v);
    public static bool GetIgnoreChildLeave(DependencyObject o) => (bool)o.GetValue(IgnoreChildLeaveProperty);
    public static void SetIgnoreChildLeave(DependencyObject o, bool v) => o.SetValue(IgnoreChildLeaveProperty, v);

    private static readonly HashSet<UIElement> _subscribed = new();

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement el && _subscribed.Add(el))
        {
            el.MouseEnter += (_, _) =>
            {
                var cmd = GetMouseEnterCommand(el);
                if (cmd?.CanExecute(null) == true) cmd.Execute(null);
            };
            el.MouseLeave += (_, _) =>
            {
                if (GetIgnoreChildLeave(el) && el.IsMouseOver) return;
                var cmd = GetMouseLeaveCommand(el);
                if (cmd?.CanExecute(null) == true) cmd.Execute(null);
            };
        }
    }
}

