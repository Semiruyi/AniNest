using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace LocalPlayer.View.Behaviors;

public static class KeyboardBehavior
{
    public static readonly DependencyProperty KeyDownCommandProperty =
        DependencyProperty.RegisterAttached("KeyDownCommand", typeof(ICommand), typeof(KeyboardBehavior),
            new PropertyMetadata(null, OnKeyDownCommandChanged));

    public static ICommand GetKeyDownCommand(DependencyObject o) => (ICommand)o.GetValue(KeyDownCommandProperty);
    public static void SetKeyDownCommand(DependencyObject o, ICommand v) => o.SetValue(KeyDownCommandProperty, v);

    private static readonly HashSet<UIElement> _subscribed = new();

    private static void OnKeyDownCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement el && _subscribed.Add(el))
        {
            el.PreviewKeyDown += (_, args) => ExecuteCommand(el, args);
            el.KeyDown += (_, args) =>
            {
                if (args.Handled) return;
                ExecuteCommand(el, args);
            };
        }
    }

    private static void ExecuteCommand(UIElement el, KeyEventArgs args)
    {
        var cmd = GetKeyDownCommand(el);
        if (cmd?.CanExecute(args) == true)
        {
            cmd.Execute(args);
            args.Handled = true;
        }
    }
}
