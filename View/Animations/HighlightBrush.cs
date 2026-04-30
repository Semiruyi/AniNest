using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LocalPlayer.View.Animations;

/// <summary>
/// 附加属性：根据 bool 值动画切换模板内指定 SolidColorBrush 的颜色。
/// 用法：&lt;Button animations:HighlightBrush.IsActive="{Binding ...}" /&gt;
/// </summary>
public static class HighlightBrush
{
    public static bool GetIsActive(DependencyObject obj) => (bool)obj.GetValue(IsActiveProperty);
    public static void SetIsActive(DependencyObject obj, bool value) => obj.SetValue(IsActiveProperty, value);

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.RegisterAttached("IsActive", typeof(bool), typeof(HighlightBrush),
            new PropertyMetadata(false, OnIsActiveChanged));

    public static Color GetActiveColor(DependencyObject obj) => (Color)obj.GetValue(ActiveColorProperty);
    public static void SetActiveColor(DependencyObject obj, Color value) => obj.SetValue(ActiveColorProperty, value);

    public static readonly DependencyProperty ActiveColorProperty =
        DependencyProperty.RegisterAttached("ActiveColor", typeof(Color), typeof(HighlightBrush),
            new PropertyMetadata((Color)ColorConverter.ConvertFromString("#007AFF")!));

    public static string GetBrushName(DependencyObject obj) => (string)obj.GetValue(BrushNameProperty);
    public static void SetBrushName(DependencyObject obj, string value) => obj.SetValue(BrushNameProperty, value);

    public static readonly DependencyProperty BrushNameProperty =
        DependencyProperty.RegisterAttached("BrushName", typeof(string), typeof(HighlightBrush),
            new PropertyMetadata("BgBrush"));

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Control control) return;
        var brushName = GetBrushName(d);
        var targetColor = (bool)e.NewValue ? GetActiveColor(d) : Colors.Transparent;
        var duration = TimeSpan.FromMilliseconds(300);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        void Animate()
        {
            if (control.Template.FindName(brushName, control) is SolidColorBrush brush)
            {
                brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                brush.BeginAnimation(SolidColorBrush.ColorProperty,
                    new ColorAnimation(targetColor, duration) { EasingFunction = ease });
            }
        }

        if (control.IsLoaded)
            Animate();
        else
            control.Loaded += (_, _) => Animate();
    }
}
