using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LocalPlayer.Presentation.Animations;

public static class TextSwapAnimator
{
    private static readonly HashSet<Panel> _initialized = new();

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached("Text", typeof(string), typeof(TextSwapAnimator),
            new PropertyMetadata(null, OnTextChanged));

    public static readonly DependencyProperty DurationMsProperty =
        DependencyProperty.RegisterAttached("DurationMs", typeof(int), typeof(TextSwapAnimator),
            new PropertyMetadata(220));

    public static string GetText(DependencyObject obj) => (string)obj.GetValue(TextProperty);
    public static void SetText(DependencyObject obj, string value) => obj.SetValue(TextProperty, value);

    public static int GetDurationMs(DependencyObject obj) => (int)obj.GetValue(DurationMsProperty);
    public static void SetDurationMs(DependencyObject obj, int value) => obj.SetValue(DurationMsProperty, value);

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Panel panel || panel.Children.Count < 2) return;
        if (panel.Children[0] is not TextBlock oldBlock || panel.Children[1] is not TextBlock newBlock) return;

        if (!_initialized.Contains(panel))
            panel.Unloaded += OnPanelUnloaded;

        var newText = (string?)e.NewValue ?? "";
        var oldText = (string?)e.OldValue ?? "";
        int duration = GetDurationMs(panel);

        if (!_initialized.Contains(panel))
        {
            _initialized.Add(panel);
            newBlock.Text = newText;
            SetOpacity(newBlock, 1);
            SetScale(newBlock, 1);
            SetOpacity(oldBlock, 0);
            SetScale(oldBlock, 0);
            return;
        }

        if (oldText == newText) return;

        oldBlock.Text = oldText;
        SetOpacity(oldBlock, 1);
        SetScale(oldBlock, 1);
        AnimationHelper.AnimateScaleTransform(
            (ScaleTransform)oldBlock.RenderTransform, 0, duration, AnimationHelper.EaseIn);
        AnimationHelper.AnimateFromCurrent(
            oldBlock, UIElement.OpacityProperty, 0, duration, AnimationHelper.EaseIn);

        newBlock.Text = newText;
        SetOpacity(newBlock, 0);
        SetScale(newBlock, 0);
        AnimationHelper.AnimateScaleTransform(
            (ScaleTransform)newBlock.RenderTransform, 1, duration, AnimationHelper.EaseOut);
        AnimationHelper.AnimateFromCurrent(
            newBlock, UIElement.OpacityProperty, 1, duration, AnimationHelper.EaseOut);
    }

    private static void OnPanelUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Panel panel)
            return;

        panel.Unloaded -= OnPanelUnloaded;
        _initialized.Remove(panel);
    }

    private static void SetOpacity(UIElement element, double opacity)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.Opacity = opacity;
    }

    private static void SetScale(FrameworkElement element, double scale)
    {
        if (element.RenderTransform is ScaleTransform st)
        {
            st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            st.ScaleX = scale;
            st.ScaleY = scale;
        }
    }
}

