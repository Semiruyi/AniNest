using System;
using System.Windows;
using System.Windows.Input;

namespace AniNest.Presentation.Behaviors;

public static class HoverRevealBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(HoverRevealBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty IsRevealActiveProperty =
        DependencyProperty.RegisterAttached(
            "IsRevealActive",
            typeof(bool),
            typeof(HoverRevealBehavior),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ShowDelayMsProperty =
        DependencyProperty.RegisterAttached(
            "ShowDelayMs",
            typeof(int),
            typeof(HoverRevealBehavior),
            new PropertyMetadata(0, OnTimingChanged));

    public static readonly DependencyProperty HideDelayMsProperty =
        DependencyProperty.RegisterAttached(
            "HideDelayMs",
            typeof(int),
            typeof(HoverRevealBehavior),
            new PropertyMetadata(120, OnTimingChanged));

    public static readonly DependencyProperty MinVisibleMsProperty =
        DependencyProperty.RegisterAttached(
            "MinVisibleMs",
            typeof(int),
            typeof(HoverRevealBehavior),
            new PropertyMetadata(220, OnTimingChanged));

    private static readonly DependencyProperty ControllerProperty =
        DependencyProperty.RegisterAttached(
            "Controller",
            typeof(HoverRevealController),
            typeof(HoverRevealBehavior),
            new PropertyMetadata(null));

    public static bool GetIsEnabled(DependencyObject obj)
        => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value)
        => obj.SetValue(IsEnabledProperty, value);

    public static bool GetIsRevealActive(DependencyObject obj)
        => (bool)obj.GetValue(IsRevealActiveProperty);

    public static void SetIsRevealActive(DependencyObject obj, bool value)
        => obj.SetValue(IsRevealActiveProperty, value);

    public static int GetShowDelayMs(DependencyObject obj)
        => (int)obj.GetValue(ShowDelayMsProperty);

    public static void SetShowDelayMs(DependencyObject obj, int value)
        => obj.SetValue(ShowDelayMsProperty, value);

    public static int GetHideDelayMs(DependencyObject obj)
        => (int)obj.GetValue(HideDelayMsProperty);

    public static void SetHideDelayMs(DependencyObject obj, int value)
        => obj.SetValue(HideDelayMsProperty, value);

    public static int GetMinVisibleMs(DependencyObject obj)
        => (int)obj.GetValue(MinVisibleMsProperty);

    public static void SetMinVisibleMs(DependencyObject obj, int value)
        => obj.SetValue(MinVisibleMsProperty, value);

    private static HoverRevealController? GetController(DependencyObject obj)
        => (HoverRevealController?)obj.GetValue(ControllerProperty);

    private static void SetController(DependencyObject obj, HoverRevealController? value)
        => obj.SetValue(ControllerProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;

        if ((bool)e.NewValue)
        {
            Attach(element);
            return;
        }

        Detach(element);
    }

    private static void OnTimingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || !GetIsEnabled(element))
            return;

        GetController(element)?.UpdateTiming(GetTiming(element));
    }

    private static void Attach(FrameworkElement element)
    {
        if (GetController(element) != null)
            return;

        var controller = new HoverRevealController(
            GetTiming(element),
            () => GetIsRevealActive(element),
            isActive => SetIsRevealActive(element, isActive));

        SetController(element, controller);
        element.MouseEnter += OnMouseEnter;
        element.MouseLeave += OnMouseLeave;
        element.Loaded += OnLoaded;
        element.Unloaded += OnUnloaded;

        SyncPointerState(element, controller);
    }

    private static void Detach(FrameworkElement element)
    {
        var controller = GetController(element);
        if (controller == null)
            return;

        element.MouseEnter -= OnMouseEnter;
        element.MouseLeave -= OnMouseLeave;
        element.Loaded -= OnLoaded;
        element.Unloaded -= OnUnloaded;
        controller.Reset();
        controller.Dispose();
        SetController(element, null);
        SetIsRevealActive(element, false);
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || GetController(element) is not { } controller)
            return;

        SyncPointerState(element, controller);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || GetController(element) is not { } controller)
            return;

        controller.Reset();
    }

    private static void OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement element || GetController(element) is not { } controller)
            return;

        controller.OnPointerEnter();
    }

    private static void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement element || GetController(element) is not { } controller)
            return;

        controller.OnPointerLeave();
    }

    private static void SyncPointerState(FrameworkElement element, HoverRevealController controller)
    {
        if (element.IsMouseOver)
        {
            controller.OnPointerEnter();
            return;
        }

        controller.Reset();
    }

    private static HoverRevealTiming GetTiming(DependencyObject obj)
        => new(
            TimeSpan.FromMilliseconds(Math.Max(0, GetShowDelayMs(obj))),
            TimeSpan.FromMilliseconds(Math.Max(0, GetHideDelayMs(obj))),
            TimeSpan.FromMilliseconds(Math.Max(0, GetMinVisibleMs(obj))));
}
