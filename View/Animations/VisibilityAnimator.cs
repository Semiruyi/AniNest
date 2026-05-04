using System.Threading.Tasks;
using System.Windows;

namespace LocalPlayer.View.Animations;

public static class VisibilityAnimator
{
    public static readonly DependencyProperty BindVisibleProperty =
        DependencyProperty.RegisterAttached("BindVisible", typeof(bool), typeof(VisibilityAnimator),
            new PropertyMetadata(true, OnBindVisibleChanged));

    public static bool GetBindVisible(DependencyObject o) => (bool)o.GetValue(BindVisibleProperty);
    public static void SetBindVisible(DependencyObject o, bool v) => o.SetValue(BindVisibleProperty, v);

    private static async void OnBindVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement el) return;
        if (e.NewValue is true)
            await ShowAsync(el);
        else
            await HideAsync(el);
    }

    private static async Task HideAsync(FrameworkElement el)
    {
        var width = el.ActualWidth;
        el.Tag = width;
        el.Width = width;

        var exitDone = new TaskCompletionSource<bool>();
        AnimationHelper.ApplyExit(el, ExitEffect.Default, () => exitDone.TrySetResult(true));

        var widthAnim = AnimationHelper.AnimateAsync(el, FrameworkElement.WidthProperty,
            width, 0, ExitEffect.Default.Opacity.DurationMs, ExitEffect.Default.Opacity.Easing);

        await Task.WhenAll(exitDone.Task, widthAnim);

        el.Visibility = Visibility.Collapsed;
        el.Width = double.NaN;
    }

    private static async Task ShowAsync(FrameworkElement el)
    {
        el.Visibility = Visibility.Visible;
        el.Width = 0;

        AnimationHelper.ApplyEntrance(el, EntranceEffect.Default);

        if (el.Tag is double targetWidth && targetWidth > 0)
        {
            await AnimationHelper.AnimateAsync(el, FrameworkElement.WidthProperty,
                0, targetWidth, EntranceEffect.Default.Opacity.DurationMs, EntranceEffect.Default.Opacity.Easing);
        }

        el.Width = double.NaN;
    }
}
