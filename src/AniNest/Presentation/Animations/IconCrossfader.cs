using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AniNest.Presentation.Animations;

public enum IconCrossfaderPreset
{
    Default,
    Emphasis
}

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


    public static IconCrossfaderPreset GetPreset(DependencyObject obj) => (IconCrossfaderPreset)obj.GetValue(PresetProperty);
    public static void SetPreset(DependencyObject obj, IconCrossfaderPreset value) => obj.SetValue(PresetProperty, value);

    public static readonly DependencyProperty PresetProperty =
        DependencyProperty.RegisterAttached("Preset", typeof(IconCrossfaderPreset), typeof(IconCrossfader),
            new PropertyMetadata(IconCrossfaderPreset.Default));


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

        if (!_initialized.Contains(panel))
            panel.Unloaded += OnPanelUnloaded;

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

    private static void OnPanelUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Panel panel)
            return;

        panel.Unloaded -= OnPanelUnloaded;
        _initialized.Remove(panel);
        _clickOutDone.Remove(panel);

        var buttons = new List<Button>();
        foreach (var pair in _buttonToPanel)
        {
            if (pair.Value == panel)
                buttons.Add(pair.Key);
        }

        foreach (var button in buttons)
        {
            button.PreviewMouseLeftButtonDown -= OnButtonMouseDown;
            button.PreviewMouseLeftButtonUp -= OnButtonMouseUp;
            button.LostMouseCapture -= OnButtonLostCapture;
            _buttonToPanel.Remove(button);
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

        var scale = AnimationHelper.GetScaleTransform(currentElement);
        scale.ScaleX = 1;
        scale.ScaleY = 1;
        AnimationHelper.AnimateFromCurrent(currentElement, UIElement.OpacityProperty, 0, ResolveDurationMs(GetPreset(panel)));

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
        int durationMs = ResolveDurationMs(GetPreset(panel));
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
        _ = AnimationHelper.GetScaleTransform(element);
    }

    private static void SnapState(FrameworkElement element, double scale)
    {
        var st = AnimationHelper.GetScaleTransform(element);
        st.ScaleX = st.ScaleY = scale;
        element.Opacity = scale;
    }

    private static int ResolveDurationMs(IconCrossfaderPreset preset)
        => preset switch
        {
            IconCrossfaderPreset.Emphasis => 420,
            _ => 300
        };

    private static void AnimateIn(FrameworkElement element, int durationMs, bool noScale)
    {
        element.Visibility = Visibility.Visible;
        var st = AnimationHelper.GetScaleTransform(element);

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
            var scale = AnimationHelper.GetScaleTransform(element);
            AnimationHelper.AnimateScaleTransform(scale, 0, durationMs);
        }
        AnimationHelper.AnimateFromCurrent(element, UIElement.OpacityProperty, 0, durationMs);
    }
}

