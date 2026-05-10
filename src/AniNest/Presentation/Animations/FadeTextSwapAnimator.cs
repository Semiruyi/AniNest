using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace AniNest.Presentation.Animations;

public enum FadeTextSwapPreset
{
    Default,
    Emphasis
}

public static class FadeTextSwapAnimator
{
    private static readonly HashSet<Panel> Initialized = [];

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached("Text", typeof(string), typeof(FadeTextSwapAnimator),
            new PropertyMetadata(null, OnTextChanged));

    public static readonly DependencyProperty PresetProperty =
        DependencyProperty.RegisterAttached("Preset", typeof(FadeTextSwapPreset), typeof(FadeTextSwapAnimator),
            new PropertyMetadata(FadeTextSwapPreset.Default));

    public static string GetText(DependencyObject obj) => (string)obj.GetValue(TextProperty);
    public static void SetText(DependencyObject obj, string value) => obj.SetValue(TextProperty, value);

    public static FadeTextSwapPreset GetPreset(DependencyObject obj) => (FadeTextSwapPreset)obj.GetValue(PresetProperty);
    public static void SetPreset(DependencyObject obj, FadeTextSwapPreset value) => obj.SetValue(PresetProperty, value);

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Panel panel || panel.Children.Count < 2)
            return;

        if (panel.Children[0] is not TextBlock oldBlock || panel.Children[1] is not TextBlock newBlock)
            return;

        if (!Initialized.Contains(panel))
            panel.Unloaded += OnPanelUnloaded;

        var newText = (string?)e.NewValue ?? string.Empty;
        var oldText = (string?)e.OldValue ?? string.Empty;
        var duration = ResolveDurationMs(GetPreset(panel));

        if (!Initialized.Contains(panel))
        {
            Initialized.Add(panel);
            newBlock.Text = newText;
            SetOpacity(newBlock, 1);
            SetOpacity(oldBlock, 0);
            return;
        }

        if (oldText == newText)
            return;

        oldBlock.Text = oldText;
        SetOpacity(oldBlock, 1);
        AnimationHelper.AnimateFromCurrent(
            oldBlock, UIElement.OpacityProperty, 0, duration, AnimationHelper.EaseIn);

        newBlock.Text = newText;
        SetOpacity(newBlock, 0);
        AnimationHelper.AnimateFromCurrent(
            newBlock, UIElement.OpacityProperty, 1, duration, AnimationHelper.EaseOut);
    }

    private static void OnPanelUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Panel panel)
            return;

        panel.Unloaded -= OnPanelUnloaded;
        Initialized.Remove(panel);
    }

    private static void SetOpacity(UIElement element, double opacity)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.Opacity = opacity;
    }

    private static int ResolveDurationMs(FadeTextSwapPreset preset)
        => preset switch
        {
            FadeTextSwapPreset.Emphasis => 420,
            _ => 220
        };
}
