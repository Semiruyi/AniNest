using System;
using System.Linq;
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

    // ── IsExiting (退出动画中，冻结 hover 状态) ──────────────────

    private static readonly DependencyProperty IsExitingProperty =
        DependencyProperty.RegisterAttached("IsExiting", typeof(bool), typeof(CardAnimation),
            new PropertyMetadata(false));

    private static bool GetIsExiting(DependencyObject o) => (bool)o.GetValue(IsExitingProperty);
    private static void SetIsExiting(DependencyObject o, bool v) => o.SetValue(IsExitingProperty, v);

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
        bool alreadySetUp = border.RenderTransform is ScaleTransform;

        if (alreadySetUp)
        {
            // 页面切回时重设滞留的 hover/press 状态
            ResetVisualState(border);
            return;
        }

        border.RenderTransformOrigin = new Point(0.5, 0.5);
        border.RenderTransform = new ScaleTransform(1, 1);

        var image = FindChild<Image>(border);

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

        // 删除按钮初始隐藏 (Opacity=0 + Scale=0)
        var deleteBtn = FindChild<Button>(border);
        if (deleteBtn != null)
        {
            deleteBtn.Opacity = 0;
            deleteBtn.IsHitTestVisible = false;
            deleteBtn.RenderTransformOrigin = new Point(0.5, 0.5);
            deleteBtn.RenderTransform = new ScaleTransform(0, 0);
            deleteBtn.PreviewMouseDown += OnDeleteBtnDown;
            deleteBtn.PreviewMouseUp += OnDeleteBtnUp;

            // 拦截删除按钮点击：先播退出动画，动画完成后再执行命令。
            // 先把 Command 保存下来然后清空，防止 Button 自动执行命令。
            var savedCmd = deleteBtn.Command;
            var savedParam = deleteBtn.CommandParameter;
            deleteBtn.Command = null;
            deleteBtn.Click += (s, ce) =>
            {
                // 标记退出中，冻结卡片 hover 状态
                border.SetValue(IsExitingProperty, true);

                var container = FindAncestor<ContentPresenter>(deleteBtn);
                if (container != null)
                {
                    AnimateContainerExit(container, 400, () =>
                    {
                        deleteBtn.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                if (savedCmd?.CanExecute(savedParam) == true)
                                    savedCmd.Execute(savedParam);
                            }));
                    });
                }
                else
                {
                    if (savedCmd?.CanExecute(savedParam) == true)
                        savedCmd.Execute(savedParam);
                }
            };
        }
    }

    /// <summary>切页返回时重置滞留的 hover/press/删除按钮状态</summary>
    private static void ResetVisualState(Border border)
    {
        if (border.RenderTransform is ScaleTransform scale)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scale.ScaleX = 1;
            scale.ScaleY = 1;
        }

        var image = FindChild<Image>(border);
        if (image?.RenderTransform is TransformGroup g)
        {
            if (g.Children.Count >= 2)
            {
                if (g.Children[0] is ScaleTransform imgScale)
                {
                    imgScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    imgScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    imgScale.ScaleX = 1;
                    imgScale.ScaleY = 1;
                }
                if (g.Children[1] is TranslateTransform imgTrans)
                {
                    imgTrans.BeginAnimation(TranslateTransform.YProperty, null);
                    imgTrans.Y = 0;
                }
            }
        }

        var deleteBtn = FindChild<Button>(border);
        if (deleteBtn != null)
        {
            deleteBtn.BeginAnimation(UIElement.OpacityProperty, null);
            deleteBtn.Opacity = 0;
            deleteBtn.IsHitTestVisible = false;
            if (deleteBtn.RenderTransform is ScaleTransform btnScale)
            {
                btnScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                btnScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                btnScale.ScaleX = 0;
                btnScale.ScaleY = 0;
            }
        }
    }

    // ── Hover Enter ──────────────────────────────────────────────

    private static void OnMouseEnter(object sender, MouseEventArgs e)
    {
        var border = (Border)sender;
        if (GetIsExiting(border)) return;
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

        var g = EnsureImageTransform(border);
        Log.Debug($"MouseEnter: ImageTransform={g != null}, CoverScale={coverScale}, CoverShiftY={coverShiftY}, CoverMs={coverMs}");
        if (g != null)
        {
            AnimationHelper.AnimateScaleTransform((ScaleTransform)g.Children[0], coverScale, coverMs, ease);
            AnimationHelper.AnimateFromCurrent(g.Children[1], TranslateTransform.YProperty, coverShiftY, coverMs, ease);
        }

        // 删除按钮显示 (scale + opacity, 参考 PopupAnimator)
        var deleteBtn = FindChild<Button>(border);
        if (deleteBtn != null && deleteBtn.RenderTransform is ScaleTransform btnScale)
        {
            deleteBtn.IsHitTestVisible = true;
            AnimationHelper.AnimateFromCurrent(deleteBtn, UIElement.OpacityProperty, 1, hoverMs, ease);
            AnimationHelper.AnimateScaleTransform(btnScale, 1, hoverMs, ease);
        }
    }

    // ── Hover Leave ──────────────────────────────────────────────

    private static void OnMouseLeave(object sender, MouseEventArgs e)
    {
        var border = (Border)sender;
        if (GetIsExiting(border)) return;
        double blurNormal = GetShadowBlurNormal(border);
        double opNormal = GetShadowOpacityNormal(border);
        int leaveMs = GetLeaveDurationMs(border);
        int coverMs = GetCoverDurationMs(border);
        var ease = AnimationHelper.EaseInOut;

        AnimationHelper.AnimateScaleTransform(
            (ScaleTransform)border.RenderTransform, 1.0, leaveMs, ease);

        var g = EnsureImageTransform(border);
        if (g != null)
        {
            AnimationHelper.AnimateScaleTransform((ScaleTransform)g.Children[0], 1.0, coverMs, ease);
            AnimationHelper.AnimateFromCurrent(g.Children[1], TranslateTransform.YProperty, 0, coverMs, ease);
        }

        // 删除按钮隐藏 (scale + opacity, 参考 PopupAnimator)
        var deleteBtn = FindChild<Button>(border);
        if (deleteBtn != null && deleteBtn.RenderTransform is ScaleTransform btnScale)
        {
            var pos = e.GetPosition(border);
            // 如果鼠标仍在 Border 范围内（因子元素捕获鼠标导致的伪 Leave），跳过隐藏
            if (pos.X >= 0 && pos.Y >= 0 && pos.X <= border.ActualWidth && pos.Y <= border.ActualHeight)
                return;
            deleteBtn.IsHitTestVisible = false;
            // 先固化当前动画值，避免 AnimateScaleTransform 内部 BeginAnimation(null) 回退到 base value
            double curX = btnScale.ScaleX;
            double curY = btnScale.ScaleY;
            btnScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            btnScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            btnScale.ScaleX = curX;
            btnScale.ScaleY = curY;
            AnimationHelper.AnimateFromCurrent(deleteBtn, UIElement.OpacityProperty, 0, leaveMs, ease);
            AnimationHelper.AnimateScaleTransform(btnScale, 0, leaveMs, ease);
        }
    }

    // ── Press Flash ──────────────────────────────────────────────

    private static void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        var border = (Border)sender;
        if (GetIsExiting(border)) return;
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
        if (GetIsExiting(border)) return;
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

    // ── Delete Button Press (参考 ButtonScaleHover / CardAnimation press) ──

    private static void OnDeleteBtnDown(object sender, MouseButtonEventArgs e)
    {
        var btn = (Button)sender;
        var scale = (ScaleTransform)btn.RenderTransform;
        double currentX = scale.ScaleX;
        double currentY = scale.ScaleY;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        scale.ScaleX = currentX;
        scale.ScaleY = currentY;
        AnimationHelper.AnimateScaleTransform(scale, 0.85, 100, AnimationHelper.EaseIn);
    }

    private static void OnDeleteBtnUp(object sender, MouseButtonEventArgs e)
    {
        var btn = (Button)sender;
        var scale = (ScaleTransform)btn.RenderTransform;
        double currentX = scale.ScaleX;
        double currentY = scale.ScaleY;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        scale.ScaleX = currentX;
        scale.ScaleY = currentY;
        AnimationHelper.AnimateScaleTransform(scale, 1.0, 250, AnimationHelper.EaseOut);
    }

    // ── Container Exit Animation ─────────────────────────────────

    private static void AnimateContainerExit(FrameworkElement container, int durationMs, Action onCompleted)
    {
        AnimationHelper.ApplyExit(container, ExitEffect.Default, onCompleted);
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

    private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T result) return result;
            parent = VisualTreeHelper.GetParent(parent);
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
