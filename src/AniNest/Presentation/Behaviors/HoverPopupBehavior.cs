using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace AniNest.Presentation.Behaviors;

public static class HoverPopupBehavior
{
    public static readonly DependencyProperty ControllerProperty =
        DependencyProperty.RegisterAttached(
            "Controller",
            typeof(HoverPopupController),
            typeof(HoverPopupBehavior),
            new PropertyMetadata(null, OnPropertyChanged));

    public static readonly DependencyProperty ZoneProperty =
        DependencyProperty.RegisterAttached(
            "Zone",
            typeof(HoverPopupZone),
            typeof(HoverPopupBehavior),
            new PropertyMetadata(HoverPopupZone.Host, OnPropertyChanged));

    private static readonly HashSet<UIElement> SubscribedElements = new();

    public static HoverPopupController? GetController(DependencyObject obj)
        => (HoverPopupController?)obj.GetValue(ControllerProperty);

    public static void SetController(DependencyObject obj, HoverPopupController? value)
        => obj.SetValue(ControllerProperty, value);

    public static HoverPopupZone GetZone(DependencyObject obj)
        => (HoverPopupZone)obj.GetValue(ZoneProperty);

    public static void SetZone(DependencyObject obj, HoverPopupZone value)
        => obj.SetValue(ZoneProperty, value);

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element || !SubscribedElements.Add(element))
            return;

        element.MouseEnter += OnMouseEnter;
        element.MouseLeave += OnMouseLeave;
        if (element is FrameworkElement frameworkElement)
            frameworkElement.Unloaded += OnUnloaded;
    }

    private static void OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not UIElement element || GetController(element) is not { } controller)
            return;

        switch (GetZone(element))
        {
            case HoverPopupZone.Host:
                controller.OnHostEnter();
                break;
            case HoverPopupZone.Popup:
                controller.OnPopupEnter();
                break;
        }
    }

    private static void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not UIElement element || GetController(element) is not { } controller)
            return;

        switch (GetZone(element))
        {
            case HoverPopupZone.Host:
                controller.OnHostLeave();
                break;
            case HoverPopupZone.Popup:
                controller.OnPopupLeave();
                break;
        }
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not UIElement element || !SubscribedElements.Remove(element))
            return;

        element.MouseEnter -= OnMouseEnter;
        element.MouseLeave -= OnMouseLeave;
        if (element is FrameworkElement frameworkElement)
            frameworkElement.Unloaded -= OnUnloaded;
    }
}
