using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using LocalPlayer.Model;

namespace LocalPlayer.View.Animations;

public static class CardAnimation
{
    private static readonly Logger Log = AppLog.For(nameof(CardAnimation));
    // ── Enabled ──────────────────────────────────────────────────

    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(CardAnimation),
            new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject o) => (bool)o.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject o, bool v) => o.SetValue(EnabledProperty, v);

    // ── Hover Scale ──────────────────────────────────────────────

    public static readonly DependencyProperty HoverScaleProperty =
        DependencyProperty.RegisterAttached("HoverScale", typeof(double), typeof(CardAnimation),
            new PropertyMetadata(1.03));

    public static double GetHoverScale(DependencyObject o) => (double)o.GetValue(HoverScaleProperty);
    public static void SetHoverScale(DependencyObject o, double v) => o.SetValue(HoverScaleProperty, v);

    // ── Cover Scale ──────────────────────────────────────────────

    public static readonly DependencyProperty CoverScaleProperty =
        DependencyProperty.RegisterAttached("CoverScale", typeof(double), typeof(CardAnimation),
            new PropertyMetadata(1.08));

    public static double GetCoverScale(DependencyObject o) => (double)o.GetValue(CoverScaleProperty);
    public static void SetCoverScale(DependencyObject o, double v) => o.SetValue(CoverScaleProperty, v);

    // ── Cover Shift Y ────────────────────────────────────────────

    public static readonly DependencyProperty CoverShiftYProperty =
        DependencyProperty.RegisterAttached("CoverShiftY", typeof(double), typeof(CardAnimation),
            new PropertyMetadata(-6.0));

    public static double GetCoverShiftY(DependencyObject o) => (double)o.GetValue(CoverShiftYProperty);
    public static void SetCoverShiftY(DependencyObject o, double v) => o.SetValue(CoverShiftYProperty, v);

    // ── Hover Duration ───────────────────────────────────────────

    public static readonly DependencyProperty HoverDurationMsProperty =
        DependencyProperty.RegisterAttached("HoverDurationMs", typeof(int), typeof(CardAnimation),
            new PropertyMetadata(200));

    public static int GetHoverDurationMs(DependencyObject o) => (int)o.GetValue(HoverDurationMsProperty);
    public static void SetHoverDurationMs(DependencyObject o, int v) => o.SetValue(HoverDurationMsProperty, v);

    // ── Leave Duration ───────────────────────────────────────────

    public static readonly DependencyProperty LeaveDurationMsProperty =
        DependencyProperty.RegisterAttached("LeaveDurationMs", typeof(int), typeof(CardAnimation),
            new PropertyMetadata(300));

    public static int GetLeaveDurationMs(DependencyObject o) => (int)o.GetValue(LeaveDurationMsProperty);
    public static void SetLeaveDurationMs(DependencyObject o, int v) => o.SetValue(LeaveDurationMsProperty, v);

    // ── Cover Duration ───────────────────────────────────────────

    public static readonly DependencyProperty CoverDurationMsProperty =
        DependencyProperty.RegisterAttached("CoverDurationMs", typeof(int), typeof(CardAnimation),
            new PropertyMetadata(600));

    public static int GetCoverDurationMs(DependencyObject o) => (int)o.GetValue(CoverDurationMsProperty);
    public static void SetCoverDurationMs(DependencyObject o, int v) => o.SetValue(CoverDurationMsProperty, v);

    // ── Press Scale ──────────────────────────────────────────────

    public static readonly DependencyProperty PressScaleProperty =
        DependencyProperty.RegisterAttached("PressScale", typeof(double), typeof(CardAnimation),
            new PropertyMetadata(0.96));

    public static double GetPressScale(DependencyObject o) => (double)o.GetValue(PressScaleProperty);
    public static void SetPressScale(DependencyObject o, double v) => o.SetValue(PressScaleProperty, v);

    // ── Press Dip Duration ───────────────────────────────────────

    public static readonly DependencyProperty PressDipMsProperty =
        DependencyProperty.RegisterAttached("PressDipMs", typeof(int), typeof(CardAnimation),
            new PropertyMetadata(80));

    public static int GetPressDipMs(DependencyObject o) => (int)o.GetValue(PressDipMsProperty);
    public static void SetPressDipMs(DependencyObject o, int v) => o.SetValue(PressDipMsProperty, v);

    // ── Press Recover Duration ───────────────────────────────────

    public static readonly DependencyProperty PressRecoverMsProperty =
        DependencyProperty.RegisterAttached("PressRecoverMs", typeof(int), typeof(CardAnimation),
            new PropertyMetadata(300));

    public static int GetPressRecoverMs(DependencyObject o) => (int)o.GetValue(PressRecoverMsProperty);
    public static void SetPressRecoverMs(DependencyObject o, int v) => o.SetValue(PressRecoverMsProperty, v);

    // ── Shadow Blur ──────────────────────────────────────────────

    public static readonly DependencyProperty ShadowBlurNormalProperty =
        DependencyProperty.RegisterAttached("ShadowBlurNormal", typeof(double), typeof(CardAnimation),
            new PropertyMetadata(10.0));

    public static double GetShadowBlurNormal(DependencyObject o) => (double)o.GetValue(ShadowBlurNormalProperty);
    public static void SetShadowBlurNormal(DependencyObject o, double v) => o.SetValue(ShadowBlurNormalProperty, v);

    public static readonly DependencyProperty ShadowBlurHoverProperty =
        DependencyProperty.RegisterAttached("ShadowBlurHover", typeof(double), typeof(CardAnimation),
            new PropertyMetadata(20.0));

    public static double GetShadowBlurHover(DependencyObject o) => (double)o.GetValue(ShadowBlurHoverProperty);
    public static void SetShadowBlurHover(DependencyObject o, double v) => o.SetValue(ShadowBlurHoverProperty, v);

    // ── Shadow Opacity ───────────────────────────────────────────

    public static readonly DependencyProperty ShadowOpacityNormalProperty =
        DependencyProperty.RegisterAttached("ShadowOpacityNormal", typeof(double), typeof(CardAnimation),
            new PropertyMetadata(0.35));

    public static double GetShadowOpacityNormal(DependencyObject o) => (double)o.GetValue(ShadowOpacityNormalProperty);
    public static void SetShadowOpacityNormal(DependencyObject o, double v) => o.SetValue(ShadowOpacityNormalProperty, v);

    public static readonly DependencyProperty ShadowOpacityHoverProperty =
        DependencyProperty.RegisterAttached("ShadowOpacityHover", typeof(double), typeof(CardAnimation),
            new PropertyMetadata(0.6));

    public static double GetShadowOpacityHover(DependencyObject o) => (double)o.GetValue(ShadowOpacityHoverProperty);
    public static void SetShadowOpacityHover(DependencyObject o, double v) => o.SetValue(ShadowOpacityHoverProperty, v);

    // ── Setup ────────────────────────────────────────────────────

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Border border) return;
        Log.Debug($"OnEnabledChanged: {(bool)e.NewValue}");
        border.Loaded -= OnLoaded;
        if (e.NewValue is true)
            border.Loaded += OnLoaded;
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        var border = (Border)sender;
        border.Loaded -= OnLoaded;

        // DataTemplate 中的 Effect 可能被 WPF 冻结，动画无法修改属性，需要替换为未冻结副本
        if (border.Effect is DropShadowEffect frozenShadow && frozenShadow.IsFrozen)
        {
            border.Effect = new DropShadowEffect
            {
                Color = frozenShadow.Color,
                BlurRadius = frozenShadow.BlurRadius,
                ShadowDepth = frozenShadow.ShadowDepth,
                Opacity = frozenShadow.Opacity,
                Direction = frozenShadow.Direction
            };
        }

        border.RenderTransformOrigin = new Point(0.5, 0.5);
        border.RenderTransform = new ScaleTransform(1, 1);

        var image = FindChild<Image>(border);
        Log.Debug($"OnLoaded: Image found={image != null}, Visible={image?.Visibility}, Effect frozen={border.Effect is DropShadowEffect s && s.IsFrozen}");
        if (image != null)
        {
            var group = new TransformGroup();
            group.Children.Add(new ScaleTransform(1, 1));
            group.Children.Add(new TranslateTransform(0, 0));
            image.RenderTransformOrigin = new Point(0.5, 0.5);
            image.RenderTransform = group;
        }

        border.MouseEnter += OnMouseEnter;
        border.MouseLeave += OnMouseLeave;
        border.PreviewMouseLeftButtonDown += OnMouseDown;
        border.PreviewMouseLeftButtonUp += OnMouseUp;
    }

    // ── Hover Enter ──────────────────────────────────────────────

    private static void OnMouseEnter(object sender, MouseEventArgs e)
    {
        var border = (Border)sender;
        double hoverScale = GetHoverScale(border);
        double coverScale = GetCoverScale(border);
        double coverShiftY = GetCoverShiftY(border);
        double blurHover = GetShadowBlurHover(border);
        double opHover = GetShadowOpacityHover(border);
        int hoverMs = GetHoverDurationMs(border);
        int coverMs = GetCoverDurationMs(border);
        var ease = AnimationHelper.EaseOut;

        Log.Debug($"MouseEnter: hoverScale={hoverScale}, coverScale={coverScale}, coverShiftY={coverShiftY}");

        AnimationHelper.AnimateScaleTransform(
            (ScaleTransform)border.RenderTransform, hoverScale, hoverMs, ease);

        if (border.Effect is DropShadowEffect shadow)
        {
            AnimationHelper.AnimateFromCurrent(shadow, DropShadowEffect.BlurRadiusProperty, blurHover, hoverMs, ease);
            AnimationHelper.AnimateFromCurrent(shadow, DropShadowEffect.OpacityProperty, opHover, hoverMs, ease);
        }

        var g = EnsureImageTransform(border);
        Log.Debug($"MouseEnter: ImageTransform={g != null}, CoverScale={coverScale}, CoverShiftY={coverShiftY}, CoverMs={coverMs}");
        if (g != null)
        {
            AnimationHelper.AnimateScaleTransform((ScaleTransform)g.Children[0], coverScale, coverMs, ease);
            AnimationHelper.AnimateFromCurrent(g.Children[1], TranslateTransform.YProperty, coverShiftY, coverMs, ease);
        }
    }

    // ── Hover Leave ──────────────────────────────────────────────

    private static void OnMouseLeave(object sender, MouseEventArgs e)
    {
        var border = (Border)sender;
        double blurNormal = GetShadowBlurNormal(border);
        double opNormal = GetShadowOpacityNormal(border);
        int leaveMs = GetLeaveDurationMs(border);
        int coverMs = GetCoverDurationMs(border);
        var ease = AnimationHelper.EaseInOut;

        AnimationHelper.AnimateScaleTransform(
            (ScaleTransform)border.RenderTransform, 1.0, leaveMs, ease);

        if (border.Effect is DropShadowEffect shadow)
        {
            AnimationHelper.AnimateFromCurrent(shadow, DropShadowEffect.BlurRadiusProperty, blurNormal, leaveMs, ease);
            AnimationHelper.AnimateFromCurrent(shadow, DropShadowEffect.OpacityProperty, opNormal, leaveMs, ease);
        }

        var g = EnsureImageTransform(border);
        if (g != null)
        {
            AnimationHelper.AnimateScaleTransform((ScaleTransform)g.Children[0], 1.0, coverMs, ease);
            AnimationHelper.AnimateFromCurrent(g.Children[1], TranslateTransform.YProperty, 0, coverMs, ease);
        }
    }

    // ── Press Flash ──────────────────────────────────────────────

    private static void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        var border = (Border)sender;
        if (e.OriginalSource is DependencyObject d && IsInsideButton(d, border))
            return;

        double pressScale = GetPressScale(border);
        int dipMs = GetPressDipMs(border);

        var scale = (ScaleTransform)border.RenderTransform;
        double current = scale.ScaleX;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        scale.ScaleX = current;
        scale.ScaleY = current;

        var ease = AnimationHelper.EaseIn;
        AnimationHelper.AnimateScaleTransform(scale, pressScale, dipMs, ease);
    }

    private static void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        var border = (Border)sender;
        if (e.OriginalSource is DependencyObject d && IsInsideButton(d, border))
            return;

        double hoverScale = GetHoverScale(border);
        int recoverMs = GetPressRecoverMs(border);

        var scale = (ScaleTransform)border.RenderTransform;
        double current = scale.ScaleX;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        scale.ScaleX = current;
        scale.ScaleY = current;

        var ease = AnimationHelper.EaseOut;
        AnimationHelper.AnimateScaleTransform(scale, hoverScale, recoverMs, ease);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static TransformGroup? EnsureImageTransform(Border border)
    {
        var image = FindChild<Image>(border);
        Log.Debug($"EnsureImageTransform: Image found={image != null}, Visibility={image?.Visibility}");
        if (image == null || image.Visibility != Visibility.Visible)
            return null;
        if (image.RenderTransform is TransformGroup g)
            return g;
        var group = new TransformGroup();
        group.Children.Add(new ScaleTransform(1, 1));
        group.Children.Add(new TranslateTransform(0, 0));
        image.RenderTransformOrigin = new Point(0.5, 0.5);
        image.RenderTransform = group;
        return group;
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result) return result;
            var desc = FindChild<T>(child);
            if (desc != null) return desc;
        }
        return null;
    }

    private static bool IsInsideButton(DependencyObject source, DependencyObject boundary)
    {
        var current = source;
        while (current != null && current != boundary)
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase)
                return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }
}
