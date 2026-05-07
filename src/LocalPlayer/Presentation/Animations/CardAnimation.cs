using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Paths;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Thumbnails;
using AniNest.Infrastructure.Interop;

namespace AniNest.Presentation.Animations;

public static class CardAnimation
{
    private static readonly Logger Log = AppLog.For(nameof(CardAnimation));

    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(CardAnimation),
            new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject o) => (bool)o.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject o, bool v) => o.SetValue(EnabledProperty, v);


    public static readonly DependencyProperty HoverScaleProperty =
        DependencyProperty.RegisterAttached("HoverScale", typeof(double), typeof(CardAnimation),
            new PropertyMetadata(1.03));

    public static double GetHoverScale(DependencyObject o) => (double)o.GetValue(HoverScaleProperty);
    public static void SetHoverScale(DependencyObject o, double v) => o.SetValue(HoverScaleProperty, v);


    public static readonly DependencyProperty CoverScaleProperty =
        DependencyProperty.RegisterAttached("CoverScale", typeof(double), typeof(CardAnimation),
            new PropertyMetadata(1.08));

    public static double GetCoverScale(DependencyObject o) => (double)o.GetValue(CoverScaleProperty);
    public static void SetCoverScale(DependencyObject o, double v) => o.SetValue(CoverScaleProperty, v);


    public static readonly DependencyProperty CoverShiftYProperty =
        DependencyProperty.RegisterAttached("CoverShiftY", typeof(double), typeof(CardAnimation),
            new PropertyMetadata(-6.0));

    public static double GetCoverShiftY(DependencyObject o) => (double)o.GetValue(CoverShiftYProperty);
    public static void SetCoverShiftY(DependencyObject o, double v) => o.SetValue(CoverShiftYProperty, v);


    public static readonly DependencyProperty HoverDurationMsProperty =
        DependencyProperty.RegisterAttached("HoverDurationMs", typeof(int), typeof(CardAnimation),
            new PropertyMetadata(200));

    public static int GetHoverDurationMs(DependencyObject o) => (int)o.GetValue(HoverDurationMsProperty);
    public static void SetHoverDurationMs(DependencyObject o, int v) => o.SetValue(HoverDurationMsProperty, v);


    public static readonly DependencyProperty LeaveDurationMsProperty =
        DependencyProperty.RegisterAttached("LeaveDurationMs", typeof(int), typeof(CardAnimation),
            new PropertyMetadata(300));

    public static int GetLeaveDurationMs(DependencyObject o) => (int)o.GetValue(LeaveDurationMsProperty);
    public static void SetLeaveDurationMs(DependencyObject o, int v) => o.SetValue(LeaveDurationMsProperty, v);


    public static readonly DependencyProperty CoverDurationMsProperty =
        DependencyProperty.RegisterAttached("CoverDurationMs", typeof(int), typeof(CardAnimation),
            new PropertyMetadata(600));

    public static int GetCoverDurationMs(DependencyObject o) => (int)o.GetValue(CoverDurationMsProperty);
    public static void SetCoverDurationMs(DependencyObject o, int v) => o.SetValue(CoverDurationMsProperty, v);


    public static readonly DependencyProperty PressScaleProperty =
        DependencyProperty.RegisterAttached("PressScale", typeof(double), typeof(CardAnimation),
            new PropertyMetadata(0.96));

    public static double GetPressScale(DependencyObject o) => (double)o.GetValue(PressScaleProperty);
    public static void SetPressScale(DependencyObject o, double v) => o.SetValue(PressScaleProperty, v);


    public static readonly DependencyProperty PressDipMsProperty =
        DependencyProperty.RegisterAttached("PressDipMs", typeof(int), typeof(CardAnimation),
            new PropertyMetadata(80));

    public static int GetPressDipMs(DependencyObject o) => (int)o.GetValue(PressDipMsProperty);
    public static void SetPressDipMs(DependencyObject o, int v) => o.SetValue(PressDipMsProperty, v);


    public static readonly DependencyProperty PressRecoverMsProperty =
        DependencyProperty.RegisterAttached("PressRecoverMs", typeof(int), typeof(CardAnimation),
            new PropertyMetadata(300));

    public static int GetPressRecoverMs(DependencyObject o) => (int)o.GetValue(PressRecoverMsProperty);
    public static void SetPressRecoverMs(DependencyObject o, int v) => o.SetValue(PressRecoverMsProperty, v);


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


    private static readonly DependencyProperty IsExitingProperty =
        DependencyProperty.RegisterAttached("IsExiting", typeof(bool), typeof(CardAnimation),
            new PropertyMetadata(false));

    private static bool GetIsExiting(DependencyObject o) => (bool)o.GetValue(IsExitingProperty);
    private static void SetIsExiting(DependencyObject o, bool v) => o.SetValue(IsExitingProperty, v);

    private static readonly DependencyProperty DeleteButtonCommandProperty =
        DependencyProperty.RegisterAttached("DeleteButtonCommand", typeof(ICommand), typeof(CardAnimation));

    private static readonly DependencyProperty DeleteButtonCommandParameterProperty =
        DependencyProperty.RegisterAttached("DeleteButtonCommandParameter", typeof(object), typeof(CardAnimation));


    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Border border) return;
        border.Loaded -= OnLoaded;
        border.Unloaded -= OnUnloaded;
        if (e.NewValue is true)
        {
            border.Loaded += OnLoaded;
            border.Unloaded += OnUnloaded;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        var border = (Border)sender;
        bool alreadySetUp = border.RenderTransform is ScaleTransform;

        if (alreadySetUp)
        {
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

        var deleteBtn = FindChild<Button>(border);
        if (deleteBtn != null)
        {
            deleteBtn.Opacity = 0;
            deleteBtn.IsHitTestVisible = false;
            deleteBtn.RenderTransformOrigin = new Point(0.5, 0.5);
            deleteBtn.RenderTransform = new ScaleTransform(0, 0);
            deleteBtn.PreviewMouseDown += OnDeleteBtnDown;
            deleteBtn.PreviewMouseUp += OnDeleteBtnUp;
            if (deleteBtn.ReadLocalValue(DeleteButtonCommandProperty) == DependencyProperty.UnsetValue)
            {
                deleteBtn.SetValue(DeleteButtonCommandProperty, deleteBtn.Command);
                deleteBtn.SetValue(DeleteButtonCommandParameterProperty, deleteBtn.CommandParameter);
                deleteBtn.Command = null;
            }

            deleteBtn.Click -= OnDeleteBtnClick;
            deleteBtn.Click += OnDeleteBtnClick;
        }
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var border = (Border)sender;
        border.MouseEnter -= OnMouseEnter;
        border.MouseLeave -= OnMouseLeave;
        border.PreviewMouseLeftButtonDown -= OnMouseDown;
        border.PreviewMouseLeftButtonUp -= OnMouseUp;

        var deleteBtn = FindChild<Button>(border);
        if (deleteBtn != null)
        {
            deleteBtn.PreviewMouseDown -= OnDeleteBtnDown;
            deleteBtn.PreviewMouseUp -= OnDeleteBtnUp;
            deleteBtn.Click -= OnDeleteBtnClick;
            deleteBtn.ClearValue(DeleteButtonCommandProperty);
            deleteBtn.ClearValue(DeleteButtonCommandParameterProperty);
        }

        border.ClearValue(IsExitingProperty);
    }

    private static void OnDeleteBtnClick(object sender, RoutedEventArgs e)
    {
        var deleteBtn = (Button)sender;
        var border = FindAncestor<Border>(deleteBtn);
        if (border == null)
            return;

        border.SetValue(IsExitingProperty, true);

        var savedCmd = deleteBtn.GetValue(DeleteButtonCommandProperty) as ICommand;
        var savedParam = deleteBtn.GetValue(DeleteButtonCommandParameterProperty);
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
                    }),
                    DispatcherPriority.Background);
            });
            return;
        }

        if (savedCmd?.CanExecute(savedParam) == true)
            savedCmd.Execute(savedParam);
    }

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

        AnimationHelper.AnimateScaleTransform(
            (ScaleTransform)border.RenderTransform, hoverScale, hoverMs, ease);

        var g = EnsureImageTransform(border);
        if (g != null)
        {
            AnimationHelper.AnimateScaleTransform((ScaleTransform)g.Children[0], coverScale, coverMs, ease);
            AnimationHelper.AnimateFromCurrent(g.Children[1], TranslateTransform.YProperty, coverShiftY, coverMs, ease);
        }

        var deleteBtn = FindChild<Button>(border);
        if (deleteBtn != null && deleteBtn.RenderTransform is ScaleTransform btnScale)
        {
            deleteBtn.IsHitTestVisible = true;
            AnimationHelper.AnimateFromCurrent(deleteBtn, UIElement.OpacityProperty, 1, hoverMs, ease);
            AnimationHelper.AnimateScaleTransform(btnScale, 1, hoverMs, ease);
        }
    }


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

        var deleteBtn = FindChild<Button>(border);
        if (deleteBtn != null && deleteBtn.RenderTransform is ScaleTransform btnScale)
        {
            var pos = e.GetPosition(border);
            if (pos.X >= 0 && pos.Y >= 0 && pos.X <= border.ActualWidth && pos.Y <= border.ActualHeight)
                return;
            deleteBtn.IsHitTestVisible = false;
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


    private static void AnimateContainerExit(FrameworkElement container, int durationMs, Action onCompleted)
    {
        AnimationHelper.ApplyExit(container, ExitEffect.Default, onCompleted);
    }


    private static TransformGroup? EnsureImageTransform(Border border)
    {
        var image = FindChild<Image>(border);
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




