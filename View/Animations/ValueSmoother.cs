using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;

namespace LocalPlayer.View.Animations;

public static class ValueSmoother
{
    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(ValueSmoother),
            new PropertyMetadata(false, OnEnabledChanged));

    public static readonly DependencyProperty DurationMsProperty =
        DependencyProperty.RegisterAttached("DurationMs", typeof(int), typeof(ValueSmoother),
            new PropertyMetadata(400));

    public static readonly DependencyProperty JumpThresholdMsProperty =
        DependencyProperty.RegisterAttached("JumpThresholdMs", typeof(long), typeof(ValueSmoother),
            new PropertyMetadata(2000L));

    public static readonly DependencyProperty IsSuppressedProperty =
        DependencyProperty.RegisterAttached("IsSuppressed", typeof(bool), typeof(ValueSmoother),
            new PropertyMetadata(false));

    public static bool GetEnabled(DependencyObject o) => (bool)o.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject o, bool v) => o.SetValue(EnabledProperty, v);
    public static int GetDurationMs(DependencyObject o) => (int)o.GetValue(DurationMsProperty);
    public static void SetDurationMs(DependencyObject o, int v) => o.SetValue(DurationMsProperty, v);
    public static long GetJumpThresholdMs(DependencyObject o) => (long)o.GetValue(JumpThresholdMsProperty);
    public static void SetJumpThresholdMs(DependencyObject o, long v) => o.SetValue(JumpThresholdMsProperty, v);
    public static bool GetIsSuppressed(DependencyObject o) => (bool)o.GetValue(IsSuppressedProperty);
    public static void SetIsSuppressed(DependencyObject o, bool v) => o.SetValue(IsSuppressedProperty, v);

    private static readonly DependencyProperty LastValueProperty =
        DependencyProperty.RegisterAttached("LastValue", typeof(double), typeof(ValueSmoother),
            new PropertyMetadata(double.NaN));

    private static readonly DependencyProperty IsAnimatingProperty =
        DependencyProperty.RegisterAttached("IsAnimating", typeof(bool), typeof(ValueSmoother),
            new PropertyMetadata(false));

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RangeBase rb) return;
        if (e.NewValue is true)
        {
            rb.SetValue(LastValueProperty, rb.Value);
            rb.ValueChanged += OnValueChanged;
        }
        else
        {
            rb.ValueChanged -= OnValueChanged;
        }
    }

    private static void OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> args)
    {
        var rb = (RangeBase)sender;
        if ((bool)rb.GetValue(IsAnimatingProperty)) return;

        double lastValue = (double)rb.GetValue(LastValueProperty);
        if (double.IsNaN(lastValue))
        {
            rb.SetValue(LastValueProperty, args.NewValue);
            return;
        }

        double delta = Math.Abs(args.NewValue - args.OldValue);
        long threshold = GetJumpThresholdMs(rb);
        bool isSuppressed = GetIsSuppressed(rb);

        if (delta > threshold && !isSuppressed)
            AnimateSmooth(rb, args.OldValue, args.NewValue);

        rb.SetValue(LastValueProperty, args.NewValue);
    }

    private static void AnimateSmooth(RangeBase rb, double from, double to)
    {
        // 到 0 是视频结束/停止的自然状态，无需动画
        if (to == 0)
            return;

        int durationMs = GetDurationMs(rb);
        rb.SetValue(IsAnimatingProperty, true);

        rb.BeginAnimation(RangeBase.ValueProperty, null);
        rb.SetCurrentValue(RangeBase.ValueProperty, from);

        var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = AnimationHelper.EaseInOut
        };
        anim.Completed += (_, _) =>
        {
            rb.BeginAnimation(RangeBase.ValueProperty, null);
            rb.SetCurrentValue(RangeBase.ValueProperty, to); // 覆盖 from，防止回弹
            rb.SetValue(IsAnimatingProperty, false);
        };
        rb.BeginAnimation(RangeBase.ValueProperty, anim);
    }
}
