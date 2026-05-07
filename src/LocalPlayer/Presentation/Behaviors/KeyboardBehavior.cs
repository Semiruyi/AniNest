using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace LocalPlayer.Presentation.Behaviors;

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
        if (d is not UIElement el || !_subscribed.Add(el))
            return;

        el.PreviewKeyDown += OnPreviewKeyDown;
        el.KeyDown += OnKeyDown;
        if (el is FrameworkElement fe)
            fe.Unloaded += OnUnloaded;
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is UIElement el)
            ExecuteCommand(el, e);
    }

    private static void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || sender is not UIElement el)
            return;

        ExecuteCommand(el, e);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not UIElement el || !_subscribed.Remove(el))
            return;

        el.PreviewKeyDown -= OnPreviewKeyDown;
        el.KeyDown -= OnKeyDown;
        if (el is FrameworkElement fe)
            fe.Unloaded -= OnUnloaded;
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

