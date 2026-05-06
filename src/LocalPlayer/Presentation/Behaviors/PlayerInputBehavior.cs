using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using LocalPlayer.Features.Player.Input;

namespace LocalPlayer.Presentation.Behaviors;

public static class PlayerInputBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(PlayerInputBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly HashSet<UIElement> Subscribed = new();

    public static bool GetIsEnabled(DependencyObject o) => (bool)o.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject o, bool value) => o.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element || e.NewValue is not bool enabled)
            return;

        if (enabled)
        {
            if (!Subscribed.Add(element))
                return;

            element.PreviewMouseDown += OnPreviewMouseDown;
            element.PreviewMouseUp += OnPreviewMouseUp;
            element.PreviewMouseWheel += OnPreviewMouseWheel;
            if (element is FrameworkElement fe)
                fe.Unloaded += OnElementUnloaded;
        }
        else
        {
            Unsubscribe(element);
        }
    }

    private static void OnElementUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement element)
            Unsubscribe(element);
    }

    private static void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is IPlayerInputHost host)
            host.InputService.TryHandlePreviewMouseDown(host, e);
    }

    private static void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is IPlayerInputHost host)
            host.InputService.TryHandlePreviewMouseUp(host, e);
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is IPlayerInputHost host)
            host.InputService.TryHandlePreviewMouseWheel(host, e);
    }

    private static void Unsubscribe(UIElement element)
    {
        if (!Subscribed.Remove(element))
            return;

        element.PreviewMouseDown -= OnPreviewMouseDown;
        element.PreviewMouseUp -= OnPreviewMouseUp;
        element.PreviewMouseWheel -= OnPreviewMouseWheel;
        if (element is FrameworkElement fe)
            fe.Unloaded -= OnElementUnloaded;
    }
}
