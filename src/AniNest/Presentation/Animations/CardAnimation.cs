using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using AniNest.Infrastructure.Logging;

namespace AniNest.Presentation.Animations;

public static class CardAnimation
{
    private static readonly Logger Log = AppLog.For(nameof(CardAnimation));

    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(CardAnimation),
            new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject o) => (bool)o.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject o, bool v) => o.SetValue(EnabledProperty, v);

    public static readonly DependencyProperty HoverScaleProperty =
        DependencyProperty.RegisterAttached("HoverScale", typeof(double), typeof(CardAnimation),
            new PropertyMetadata(1.03));

    public static double GetHoverScale(DependencyObject o) => (double)o.GetValue(HoverScaleProperty);
    public static void SetHoverScale(DependencyObject o, double v) => o.SetValue(HoverScaleProperty, v);

    public static readonly DependencyProperty CoverScaleProperty =
        DependencyProperty.RegisterAttached("CoverScale", typeof(double), typeof(CardAnimation),
            new PropertyMetadata(1.08));

    public static double GetCoverScale(DependencyObject o) => (double)o.GetValue(CoverScaleProperty);
    public static void SetCoverScale(DependencyObject o, double v) => o.SetValue(CoverScaleProperty, v);

    public static readonly DependencyProperty CoverShiftYProperty =
        DependencyProperty.RegisterAttached("CoverShiftY", typeof(double), typeof(CardAnimation),
            new PropertyMetadata(-6.0));

    public static double GetCoverShiftY(DependencyObject o) => (double)o.GetValue(CoverShiftYProperty);
    public static void SetCoverShiftY(DependencyObject o, double v) => o.SetValue(CoverShiftYProperty, v);

    public static readonly DependencyProperty HoverDurationMsProperty =
        DependencyProperty.RegisterAttached("HoverDurationMs", typeof(int), typeof(CardAnimation),
            new PropertyMetadata(200));

    public static int GetHoverDurationMs(DependencyObject o) => (int)o.GetValue(HoverDurationMsProperty);
    public static void SetHoverDurationMs(DependencyObject o, int v) => o.SetValue(HoverDurationMsProperty, v);

    public static readonly DependencyProperty LeaveDurationMsProperty =
        DependencyProperty.RegisterAttached("LeaveDurationMs", typeof(int), typeof(CardAnimation),
            new PropertyMetadata(300));

    public static int GetLeaveDurationMs(DependencyObject o) => (int)o.GetValue(LeaveDurationMsProperty);
    public static void SetLeaveDurationMs(DependencyObject o, int v) => o.SetValue(LeaveDurationMsProperty, v);

    public static readonly DependencyProperty CoverDurationMsProperty =
        DependencyProperty.RegisterAttached("CoverDurationMs", typeof(int), typeof(CardAnimation),
            new PropertyMetadata(600));

    public static int GetCoverDurationMs(DependencyObject o) => (int)o.GetValue(CoverDurationMsProperty);
    public static void SetCoverDurationMs(DependencyObject o, int v) => o.SetValue(CoverDurationMsProperty, v);

    public static readonly DependencyProperty PressScaleProperty =
        DependencyProperty.RegisterAttached("PressScale", typeof(double), typeof(CardAnimation),
            new PropertyMetadata(0.96));

    public static double GetPressScale(DependencyObject o) => (double)o.GetValue(PressScaleProperty);
    public static void SetPressScale(DependencyObject o, double v) => o.SetValue(PressScaleProperty, v);

    public static readonly DependencyProperty PressDipMsProperty =
        DependencyProperty.RegisterAttached("PressDipMs", typeof(int), typeof(CardAnimation),
            new PropertyMetadata(80));

    public static int GetPressDipMs(DependencyObject o) => (int)o.GetValue(PressDipMsProperty);
    public static void SetPressDipMs(DependencyObject o, int v) => o.SetValue(PressDipMsProperty, v);

    public static readonly DependencyProperty PressRecoverMsProperty =
        DependencyProperty.RegisterAttached("PressRecoverMs", typeof(int), typeof(CardAnimation),
            new PropertyMetadata(300));

    public static int GetPressRecoverMs(DependencyObject o) => (int)o.GetValue(PressRecoverMsProperty);
    public static void SetPressRecoverMs(DependencyObject o, int v) => o.SetValue(PressRecoverMsProperty, v);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Border border)
            return;

        border.Loaded -= OnLoaded;
        border.Unloaded -= OnUnloaded;

        if (e.NewValue is true)
        {
            border.Loaded += OnLoaded;
            border.Unloaded += OnUnloaded;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Border border)
            return;

        if (border.RenderTransform is not ScaleTransform)
        {
            border.RenderTransformOrigin = new Point(0.5, 0.5);
            border.RenderTransform = new ScaleTransform(1, 1);
        }

        if (FindChild<Image>(border) is Image image && image.RenderTransform is not TransformGroup)
        {
            var group = new TransformGroup();
            group.Children.Add(new ScaleTransform(1, 1));
            group.Children.Add(new TranslateTransform(0, 0));
            image.RenderTransformOrigin = new Point(0.5, 0.5);
            image.RenderTransform = group;
        }

        ResetVisualState(border);

        border.MouseEnter -= OnMouseEnter;
        border.MouseLeave -= OnMouseLeave;
        border.PreviewMouseLeftButtonDown -= OnMouseDown;
        border.PreviewMouseLeftButtonUp -= OnMouseUp;
        border.MouseEnter += OnMouseEnter;
        border.MouseLeave += OnMouseLeave;
        border.PreviewMouseLeftButtonDown += OnMouseDown;
        border.PreviewMouseLeftButtonUp += OnMouseUp;

        if (border.IsMouseOver)
            ApplyHoverState(border);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Border border)
            return;

        border.MouseEnter -= OnMouseEnter;
        border.MouseLeave -= OnMouseLeave;
        border.PreviewMouseLeftButtonDown -= OnMouseDown;
        border.PreviewMouseLeftButtonUp -= OnMouseUp;
    }

    private static void ResetVisualState(Border border)
    {
        if (border.RenderTransform is ScaleTransform scale)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scale.ScaleX = 1;
            scale.ScaleY = 1;
        }

        if (FindChild<Image>(border)?.RenderTransform is TransformGroup g && g.Children.Count >= 2)
        {
            if (g.Children[0] is ScaleTransform imgScale)
            {
                imgScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                imgScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                imgScale.ScaleX = 1;
                imgScale.ScaleY = 1;
            }

            if (g.Children[1] is TranslateTransform imgTrans)
            {
                imgTrans.BeginAnimation(TranslateTransform.YProperty, null);
                imgTrans.Y = 0;
            }
        }
    }

    private static void OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Border border)
            return;

        Log.Debug($"OnMouseEnter: card={DescribeBorder(border)} original={DescribeSource(e.OriginalSource as DependencyObject)}");
        ApplyHoverState(border);
    }

    private static void ApplyHoverState(Border border)
    {
        Log.Debug($"ApplyHoverState: card={DescribeBorder(border)}");

        if (border.RenderTransform is ScaleTransform borderScale)
        {
            AnimationHelper.AnimateScaleTransform(borderScale, GetHoverScale(border), GetHoverDurationMs(border), AnimationHelper.EaseOut);
        }

        if (FindChild<Image>(border)?.RenderTransform is TransformGroup g && g.Children.Count >= 2)
        {
            if (g.Children[0] is ScaleTransform imgScale)
                AnimationHelper.AnimateScaleTransform(imgScale, GetCoverScale(border), GetCoverDurationMs(border), AnimationHelper.EaseOut);

            if (g.Children[1] is TranslateTransform imgTrans)
                AnimationHelper.AnimateFromCurrent(imgTrans, TranslateTransform.YProperty, GetCoverShiftY(border), GetCoverDurationMs(border), AnimationHelper.EaseOut);
        }
    }

    private static void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not Border border)
            return;

        bool sourceInsideButton = e.OriginalSource is DependencyObject d && IsInsideButton(d, border);
        Log.Debug(
            $"OnMouseLeave: card={DescribeBorder(border)} original={DescribeSource(e.OriginalSource as DependencyObject)} " +
            $"insideButton={sourceInsideButton}");

        if (sourceInsideButton)
            return;

        ApplyRestingState(border, e);
    }

    private static void ApplyRestingState(Border border, MouseEventArgs? e = null)
    {
        Log.Debug($"ApplyRestingState: card={DescribeBorder(border)} hasEvent={e != null}");

        if (border.RenderTransform is ScaleTransform borderScale)
            AnimationHelper.AnimateScaleTransform(borderScale, 1.0, GetLeaveDurationMs(border), AnimationHelper.EaseInOut);

        if (FindChild<Image>(border)?.RenderTransform is TransformGroup g && g.Children.Count >= 2)
        {
            if (g.Children[0] is ScaleTransform imgScale)
                AnimationHelper.AnimateScaleTransform(imgScale, 1.0, GetCoverDurationMs(border), AnimationHelper.EaseInOut);

            if (g.Children[1] is TranslateTransform imgTrans)
                AnimationHelper.AnimateFromCurrent(imgTrans, TranslateTransform.YProperty, 0, GetCoverDurationMs(border), AnimationHelper.EaseInOut);
        }
    }

    private static void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border)
            return;

        bool sourceInsideButton = e.OriginalSource is DependencyObject d && IsInsideButton(d, border);
        Log.Debug(
            $"OnMouseDown: card={DescribeBorder(border)} original={DescribeSource(e.OriginalSource as DependencyObject)} " +
            $"insideButton={sourceInsideButton}");

        if (sourceInsideButton)
            return;

        if (border.RenderTransform is ScaleTransform scale)
            AnimationHelper.AnimateScaleTransform(scale, GetPressScale(border), GetPressDipMs(border), AnimationHelper.EaseIn);
    }

    private static void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border)
            return;

        bool sourceInsideButton = e.OriginalSource is DependencyObject d && IsInsideButton(d, border);
        Log.Debug(
            $"OnMouseUp: card={DescribeBorder(border)} original={DescribeSource(e.OriginalSource as DependencyObject)} " +
            $"insideButton={sourceInsideButton}");

        if (sourceInsideButton)
            return;

        if (border.RenderTransform is ScaleTransform scale)
            AnimationHelper.AnimateScaleTransform(scale, GetHoverScale(border), GetPressRecoverMs(border), AnimationHelper.EaseOut);
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
                return result;

            var desc = FindChild<T>(child);
            if (desc != null)
                return desc;
        }

        return null;
    }

    private static bool IsInsideButton(DependencyObject source, DependencyObject boundary)
    {
        var current = source;
        while (current != null && current != boundary)
        {
            if (current is ButtonBase)
                return true;

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static string DescribeBorder(Border border)
    {
        var name = string.IsNullOrWhiteSpace(border.Name) ? "-" : border.Name;
        return $"{name}@{border.GetHashCode():X}";
    }

    private static string DescribeSource(DependencyObject? source)
    {
        if (source == null)
            return "null";

        if (source is FrameworkElement frameworkElement)
        {
            var name = string.IsNullOrWhiteSpace(frameworkElement.Name) ? "-" : frameworkElement.Name;
            return $"{frameworkElement.GetType().Name}({name})";
        }

        return source.GetType().Name;
    }
}
