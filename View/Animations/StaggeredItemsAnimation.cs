using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GeneratorStatus = System.Windows.Controls.Primitives.GeneratorStatus;

namespace LocalPlayer.View.Animations;

public static class StaggeredItemsAnimation
{
    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(StaggeredItemsAnimation),
            new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject o) => (bool)o.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject o, bool v) => o.SetValue(EnabledProperty, v);

    public static readonly DependencyProperty StaggerMsProperty =
        DependencyProperty.RegisterAttached("StaggerMs", typeof(int), typeof(StaggeredItemsAnimation),
            new PropertyMetadata(50));

    public static int GetStaggerMs(DependencyObject o) => (int)o.GetValue(StaggerMsProperty);
    public static void SetStaggerMs(DependencyObject o, int v) => o.SetValue(StaggerMsProperty, v);

    public static readonly DependencyProperty DurationMsProperty =
        DependencyProperty.RegisterAttached("DurationMs", typeof(int), typeof(StaggeredItemsAnimation),
            new PropertyMetadata(300));

    public static int GetDurationMs(DependencyObject o) => (int)o.GetValue(DurationMsProperty);
    public static void SetDurationMs(DependencyObject o, int v) => o.SetValue(DurationMsProperty, v);

    private static readonly DependencyProperty NextIndexProperty =
        DependencyProperty.RegisterAttached("NextIndex", typeof(int), typeof(StaggeredItemsAnimation),
            new PropertyMetadata(0));

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl ic) return;
        ic.Loaded -= OnLoaded;
        if (e.NewValue is true)
            ic.Loaded += OnLoaded;
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        var ic = (ItemsControl)sender;
        ic.Loaded -= OnLoaded;
        ic.SetValue(NextIndexProperty, 0);
        ic.ItemContainerGenerator.StatusChanged += (_, _) => AnimatePending(ic);
        AnimatePending(ic);
    }

    private static void AnimatePending(ItemsControl ic)
    {
        if (ic.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
            return;

        int stagger = GetStaggerMs(ic);
        int duration = GetDurationMs(ic);
        var ease = AnimationHelper.EaseOut;
        var durationSpan = TimeSpan.FromMilliseconds(duration);
        int index = (int)ic.GetValue(NextIndexProperty);

        while (index < ic.Items.Count)
        {
            var container = ic.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
            if (container == null) break;
            AnimateContainer(container, index * stagger, durationSpan, ease);
            index++;
        }

        ic.SetValue(NextIndexProperty, index);
    }

    private static void AnimateContainer(FrameworkElement container, int delayMs,
        TimeSpan duration, IEasingFunction ease)
    {
        var group = new TransformGroup();
        var scale = new ScaleTransform(0.6, 0.6);
        group.Children.Add(scale);
        container.RenderTransformOrigin = new Point(0.5, 0.5);
        container.RenderTransform = group;
        container.Opacity = 0;

        var scaleAnim = new DoubleAnimation(0.6, 1.0, duration)
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = ease
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

        var opacityAnim = new DoubleAnimation(0, 1.0, duration)
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = ease
        };
        opacityAnim.Completed += (_, _) => container.Opacity = 1;
        container.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
    }
}
