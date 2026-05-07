using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LocalPlayer.Presentation.Animations;

public static class ButtonScaleHover
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(ButtonScaleHover),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty HoverScaleProperty =
        DependencyProperty.RegisterAttached("HoverScale", typeof(double), typeof(ButtonScaleHover),
            new PropertyMetadata(1.2));

    public static readonly DependencyProperty HoverScaleEnabledProperty =
        DependencyProperty.RegisterAttached("HoverScaleEnabled", typeof(bool), typeof(ButtonScaleHover),
            new PropertyMetadata(true));

    public static readonly DependencyProperty PressScaleProperty =
        DependencyProperty.RegisterAttached("PressScale", typeof(double), typeof(ButtonScaleHover),
            new PropertyMetadata(0.85));

    public static readonly DependencyProperty HoverEnterDurationMsProperty =
        DependencyProperty.RegisterAttached("HoverEnterDurationMs", typeof(int), typeof(ButtonScaleHover),
            new PropertyMetadata(150));

    public static readonly DependencyProperty HoverExitDurationMsProperty =
        DependencyProperty.RegisterAttached("HoverExitDurationMs", typeof(int), typeof(ButtonScaleHover),
            new PropertyMetadata(250));

    public static readonly DependencyProperty PressDurationMsProperty =
        DependencyProperty.RegisterAttached("PressDurationMs", typeof(int), typeof(ButtonScaleHover),
            new PropertyMetadata(130));

    public static readonly DependencyProperty ReleaseDurationMsProperty =
        DependencyProperty.RegisterAttached("ReleaseDurationMs", typeof(int), typeof(ButtonScaleHover),
            new PropertyMetadata(280));

    public static readonly DependencyProperty EasingProperty =
        DependencyProperty.RegisterAttached("Easing", typeof(IEasingFunction), typeof(ButtonScaleHover),
            new PropertyMetadata(null));

    private static readonly DependencyProperty AttachedScaleProperty =
        DependencyProperty.RegisterAttached("AttachedScale", typeof(ScaleTransform), typeof(ButtonScaleHover));

    // ---- getter / setter ----

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    public static double GetHoverScale(DependencyObject obj) => (double)obj.GetValue(HoverScaleProperty);
    public static void SetHoverScale(DependencyObject obj, double value) => obj.SetValue(HoverScaleProperty, value);

    public static bool GetHoverScaleEnabled(DependencyObject obj) => (bool)obj.GetValue(HoverScaleEnabledProperty);
    public static void SetHoverScaleEnabled(DependencyObject obj, bool value) => obj.SetValue(HoverScaleEnabledProperty, value);

    public static double GetPressScale(DependencyObject obj) => (double)obj.GetValue(PressScaleProperty);
    public static void SetPressScale(DependencyObject obj, double value) => obj.SetValue(PressScaleProperty, value);

    public static int GetHoverEnterDurationMs(DependencyObject obj) => (int)obj.GetValue(HoverEnterDurationMsProperty);
    public static void SetHoverEnterDurationMs(DependencyObject obj, int value) => obj.SetValue(HoverEnterDurationMsProperty, value);

    public static int GetHoverExitDurationMs(DependencyObject obj) => (int)obj.GetValue(HoverExitDurationMsProperty);
    public static void SetHoverExitDurationMs(DependencyObject obj, int value) => obj.SetValue(HoverExitDurationMsProperty, value);

    public static int GetPressDurationMs(DependencyObject obj) => (int)obj.GetValue(PressDurationMsProperty);
    public static void SetPressDurationMs(DependencyObject obj, int value) => obj.SetValue(PressDurationMsProperty, value);

    public static int GetReleaseDurationMs(DependencyObject obj) => (int)obj.GetValue(ReleaseDurationMsProperty);
    public static void SetReleaseDurationMs(DependencyObject obj, int value) => obj.SetValue(ReleaseDurationMsProperty, value);

    public static IEasingFunction GetEasing(DependencyObject obj) => (IEasingFunction)obj.GetValue(EasingProperty);
    public static void SetEasing(DependencyObject obj, IEasingFunction value) => obj.SetValue(EasingProperty, value);


    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Button btn)
            return;

        btn.Loaded -= OnLoaded;
        btn.Unloaded -= OnUnloaded;

        if (e.NewValue is true)
        {
            btn.Loaded += OnLoaded;
            btn.Unloaded += OnUnloaded;
        }
        else
        {
            Detach(btn);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn)
            return;

        Detach(btn);

        if (btn.Template.FindName("AnimScale", btn) is ScaleTransform st)
        {
            btn.SetValue(AttachedScaleProperty, st);
            Attach(btn, st,
                GetHoverScale(btn), GetPressScale(btn),
                GetHoverScaleEnabled(btn),
                GetHoverEnterDurationMs(btn), GetHoverExitDurationMs(btn),
                GetPressDurationMs(btn), GetReleaseDurationMs(btn),
                GetEasing(btn));
        }
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
            Detach(btn);
    }

    public static void Attach(Button button, ScaleTransform scale,
        double hoverScale = 1.2, double pressScale = 0.85,
        bool hoverScaleEnabled = true,
        int hoverEnterMs = 150, int hoverExitMs = 250,
        int pressMs = 130, int releaseMs = 280,
        IEasingFunction? ease = null)
    {
        var easing = ease ?? DefaultEase();

        button.MouseEnter += OnMouseEnter;
        button.MouseLeave += OnMouseLeave;
        button.PreviewMouseDown += OnPreviewMouseDown;
        button.PreviewMouseUp += OnPreviewMouseUp;

        button.SetValue(HoverScaleProperty, hoverScale);
        button.SetValue(PressScaleProperty, pressScale);
        button.SetValue(HoverScaleEnabledProperty, hoverScaleEnabled);
        button.SetValue(HoverEnterDurationMsProperty, hoverEnterMs);
        button.SetValue(HoverExitDurationMsProperty, hoverExitMs);
        button.SetValue(PressDurationMsProperty, pressMs);
        button.SetValue(ReleaseDurationMsProperty, releaseMs);
        button.SetValue(EasingProperty, easing);
    }

    private static void OnMouseEnter(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.GetValue(AttachedScaleProperty) is not ScaleTransform scale)
            return;

        if (!GetHoverScaleEnabled(button) || button.IsPressed)
            return;

        AnimationHelper.AnimateScaleTransform(scale, GetHoverScale(button), GetHoverEnterDurationMs(button), GetEasing(button) ?? DefaultEase());
    }

    private static void OnMouseLeave(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.GetValue(AttachedScaleProperty) is not ScaleTransform scale)
            return;

        if (!GetHoverScaleEnabled(button) || button.IsPressed)
            return;

        AnimationHelper.AnimateScaleTransform(scale, 1.0, GetHoverExitDurationMs(button), GetEasing(button) ?? DefaultEase());
    }

    private static void OnPreviewMouseDown(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.GetValue(AttachedScaleProperty) is not ScaleTransform scale)
            return;

        AnimationHelper.AnimateScaleTransform(scale, GetPressScale(button), GetPressDurationMs(button), GetEasing(button) ?? DefaultEase());
    }

    private static void OnPreviewMouseUp(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.GetValue(AttachedScaleProperty) is not ScaleTransform scale)
            return;

        double target = (GetHoverScaleEnabled(button) && button.IsMouseOver) ? GetHoverScale(button) : 1.0;
        AnimationHelper.AnimateScaleTransform(scale, target, GetReleaseDurationMs(button), GetEasing(button) ?? DefaultEase());
    }

    private static void Detach(Button button)
    {
        button.MouseEnter -= OnMouseEnter;
        button.MouseLeave -= OnMouseLeave;
        button.PreviewMouseDown -= OnPreviewMouseDown;
        button.PreviewMouseUp -= OnPreviewMouseUp;
        button.ClearValue(AttachedScaleProperty);
    }

    private static IEasingFunction DefaultEase() => new CubicBezierEase
    {
        X1 = 0.25, Y1 = 0.1, X2 = 0.25, Y2 = 1.0,
        EasingMode = EasingMode.EaseIn
    };
}

