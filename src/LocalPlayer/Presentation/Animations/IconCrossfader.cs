using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LocalPlayer.Presentation.Animations;

public static class IconCrossfader
{
    private static readonly HashSet<Panel> _initialized = new();
    private static readonly Dictionary<Button, Panel> _buttonToPanel = new();
    private static readonly HashSet<Panel> _clickOutDone = new();


    public static bool GetIsActive(DependencyObject obj) => (bool)obj.GetValue(IsActiveProperty);
    public static void SetIsActive(DependencyObject obj, bool value) => obj.SetValue(IsActiveProperty, value);

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.RegisterAttached("IsActive", typeof(bool), typeof(IconCrossfader),
            new PropertyMetadata(false, OnIsActiveChanged));


    public static int GetDurationMs(DependencyObject obj) => (int)obj.GetValue(DurationMsProperty);
    public static void SetDurationMs(DependencyObject obj, int value) => obj.SetValue(DurationMsProperty, value);

    public static readonly DependencyProperty DurationMsProperty =
        DependencyProperty.RegisterAttached("DurationMs", typeof(int), typeof(IconCrossfader),
            new PropertyMetadata(300));


    private static bool GetSuppressScale(DependencyObject obj) => (bool)obj.GetValue(SuppressScaleProperty);
    private static void SetSuppressScale(DependencyObject obj, bool value) => obj.SetValue(SuppressScaleProperty, value);

    private static readonly DependencyProperty SuppressScaleProperty =
        DependencyProperty.RegisterAttached("SuppressScale", typeof(bool), typeof(IconCrossfader),
            new PropertyMetadata(false));


    public static Button? GetListenButton(DependencyObject obj) => (Button?)obj.GetValue(ListenButtonProperty);
    public static void SetListenButton(DependencyObject obj, Button? value) => obj.SetValue(ListenButtonProperty, value);

    public static readonly DependencyProperty ListenButtonProperty =
        DependencyProperty.RegisterAttached("ListenButton", typeof(Button), typeof(IconCrossfader),
            new PropertyMetadata(null, OnListenButtonChanged));

    private static void OnListenButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Panel panel) return;

        if (e.OldValue is Button oldBtn)
        {
            oldBtn.PreviewMouseLeftButtonDown -= OnButtonMouseDown;
            oldBtn.PreviewMouseLeftButtonUp -= OnButtonMouseUp;
            oldBtn.LostMouseCapture -= OnButtonLostCapture;
            _buttonToPanel.Remove(oldBtn);
        }

        if (e.NewValue is Button newBtn)
        {
            _buttonToPanel[newBtn] = panel;
            newBtn.PreviewMouseLeftButtonDown += OnButtonMouseDown;
            newBtn.PreviewMouseLeftButtonUp += OnButtonMouseUp;
            newBtn.LostMouseCapture += OnButtonLostCapture;
        }
    }

    private static void OnButtonMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button btn || !_buttonToPanel.TryGetValue(btn, out var panel)) return;
        if (panel.Children.Count < 2) return;

        SetSuppressScale(panel, true);

        bool isActive = GetIsActive(panel);
        var currentElement = isActive ? panel.Children[1] as UIElement : panel.Children[0] as UIElement;
        if (currentElement is null) return;

        var scale = currentElement.RenderTransform as ScaleTransform;
        if (scale != null)
        {
            scale.ScaleX = 1;
            scale.ScaleY = 1;
        }
        AnimationHelper.AnimateFromCurrent(currentElement, UIElement.OpacityProperty, 0, GetDurationMs(panel));

        _clickOutDone.Add(panel);
    }

    private static void OnButtonMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button btn && _buttonToPanel.TryGetValue(btn, out var panel))
            SetSuppressScale(panel, false);
    }

    private static void OnButtonLostCapture(object sender, MouseEventArgs e)
    {
        if (sender is Button btn && _buttonToPanel.TryGetValue(btn, out var panel))
            SetSuppressScale(panel, false);
    }


    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Panel panel || panel.Children.Count < 2) return;
        if (panel.Children[0] is not FrameworkElement offElement || panel.Children[1] is not FrameworkElement onElement)
            return;

        bool isActive = (bool)e.NewValue;
        int durationMs = GetDurationMs(panel);
        bool noScale = GetSuppressScale(panel);
        bool outWasDone = _clickOutDone.Remove(panel);

        EnsureScale(offElement);
        EnsureScale(onElement);

        if (!_initialized.Contains(panel))
        {
            _initialized.Add(panel);
            SnapState(offElement, isActive ? 0 : 1);
            SnapState(onElement, isActive ? 1 : 0);
            return;
        }

        if (isActive)
        {
            if (!outWasDone)
                AnimateOut(offElement, durationMs, noScale: false);
            AnimateIn(onElement, durationMs, noScale);
        }
        else
        {
            if (!outWasDone)
                AnimateOut(onElement, durationMs, noScale: false);
            AnimateIn(offElement, durationMs, noScale);
        }
    }

    private static void EnsureScale(FrameworkElement element)
    {
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        if (element.RenderTransform is not ScaleTransform)
            element.RenderTransform = new ScaleTransform(1, 1);
    }

    private static void SnapState(FrameworkElement element, double scale)
    {
        var st = (ScaleTransform)element.RenderTransform;
        st.ScaleX = st.ScaleY = scale;
        element.Opacity = scale;
    }

    private static void AnimateIn(FrameworkElement element, int durationMs, bool noScale)
    {
        element.Visibility = Visibility.Visible;
        var st = (ScaleTransform)element.RenderTransform;

        if (noScale)
        {
            st.ScaleX = 1;
            st.ScaleY = 1;
        }
        else
        {
            st.ScaleX = 0;
            st.ScaleY = 0;
            AnimationHelper.AnimateScaleTransform(st, 1, durationMs);
        }

        element.Opacity = 0;
        AnimationHelper.AnimateFromCurrent(element, UIElement.OpacityProperty, 1, durationMs);
    }

    private static void AnimateOut(FrameworkElement element, int durationMs, bool noScale)
    {
        if (!noScale)
        {
            var scale = (ScaleTransform)element.RenderTransform;
            AnimationHelper.AnimateScaleTransform(scale, 0, durationMs);
        }
        AnimationHelper.AnimateFromCurrent(element, UIElement.OpacityProperty, 0, durationMs);
    }
}

