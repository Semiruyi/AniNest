using System.Threading.Tasks;
using System.Windows;

namespace AniNest.Presentation.Animations;

public enum FadeVisibilityPreset
{
    Default,
    Emphasis
}

public static class FadeVisibilityAnimator
{
    public static readonly DependencyProperty BindVisibleProperty =
        DependencyProperty.RegisterAttached("BindVisible", typeof(bool), typeof(FadeVisibilityAnimator),
            new PropertyMetadata(true, OnBindVisibleChanged));

    public static readonly DependencyProperty PresetProperty =
        DependencyProperty.RegisterAttached("Preset", typeof(FadeVisibilityPreset), typeof(FadeVisibilityAnimator),
            new PropertyMetadata(FadeVisibilityPreset.Default));

    public static bool GetBindVisible(DependencyObject obj) => (bool)obj.GetValue(BindVisibleProperty);
    public static void SetBindVisible(DependencyObject obj, bool value) => obj.SetValue(BindVisibleProperty, value);

    public static FadeVisibilityPreset GetPreset(DependencyObject obj) => (FadeVisibilityPreset)obj.GetValue(PresetProperty);
    public static void SetPreset(DependencyObject obj, FadeVisibilityPreset value) => obj.SetValue(PresetProperty, value);

    private static async void OnBindVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;

        var duration = ResolveDurationMs(GetPreset(element));
        if ((bool)e.NewValue)
            await ShowAsync(element, duration);
        else
            await HideAsync(element, duration);
    }

    private static int ResolveDurationMs(FadeVisibilityPreset preset)
        => preset switch
        {
            FadeVisibilityPreset.Emphasis => 420,
            _ => 220
        };

    private static async Task ShowAsync(FrameworkElement element, int durationMs)
    {
        element.Visibility = Visibility.Visible;
        element.IsHitTestVisible = true;
        await AnimationHelper.FadeInAsync(element, durationMs, AnimationHelper.EaseOut);
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.Opacity = 1;
    }

    private static async Task HideAsync(FrameworkElement element, int durationMs)
    {
        element.IsHitTestVisible = false;
        await AnimationHelper.FadeOutAsync(element, durationMs, AnimationHelper.EaseIn);
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.Opacity = 0;
        element.Visibility = Visibility.Collapsed;
    }
}
