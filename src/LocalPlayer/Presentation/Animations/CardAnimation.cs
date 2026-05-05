using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Paths;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Thumbnails;
using LocalPlayer.Infrastructure.Interop;

namespace LocalPlayer.Presentation.Animations;

public static class CardAnimation
{
    private static readonly Logger Log = AppLog.For(nameof(CardAnimation));
    // 鈹€鈹€ Enabled 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(CardAnimation),
            new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject o) => (bool)o.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject o, bool v) => o.SetValue(EnabledProperty, v);

    // 鈹€鈹€ Hover Scale 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

    public static readonly DependencyProperty HoverScaleProperty =
        DependencyProperty.RegisterAttached("HoverScale", typeof(double), typeof(CardAnimation),
            new PropertyMetadata(1.03));

    public static double GetHoverScale(DependencyObject o) => (double)o.GetValue(HoverScaleProperty);
    public static void SetHoverScale(DependencyObject o, double v) => o.SetValue(HoverScaleProperty, v);

    // 鈹€鈹€ Cover Scale 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

    public static readonly DependencyProperty CoverScaleProperty =
        DependencyProperty.RegisterAttached("CoverScale", typeof(double), typeof(CardAnimation),
            new PropertyMetadata(1.08));

    public static double GetCoverScale(DependencyObject o) => (double)o.GetValue(CoverScaleProperty);
    public static void SetCoverScale(DependencyObject o, double v) => o.SetValue(CoverScaleProperty, v);

    // 鈹€鈹€ Cover Shift Y 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

    public static readonly DependencyProperty CoverShiftYProperty =
        DependencyProperty.RegisterAttached("CoverShiftY", typeof(double), typeof(CardAnimation),
            new PropertyMetadata(-6.0));

    public static double GetCoverShiftY(DependencyObject o) => (double)o.GetValue(CoverShiftYProperty);
    public static void SetCoverShiftY(DependencyObject o, double v) => o.SetValue(CoverShiftYProperty, v);

    // 鈹€鈹€ Hover Duration 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

    public static readonly DependencyProperty HoverDurationMsProperty =
        DependencyProperty.RegisterAttached("HoverDurationMs", typeof(int), typeof(CardAnimation),
            new PropertyMetadata(200));

    public static int GetHoverDurationMs(DependencyObject o) => (int)o.GetValue(HoverDurationMsProperty);
    public static void SetHoverDurationMs(DependencyObject o, int v) => o.SetValue(HoverDurationMsProperty, v);

    // 鈹€鈹€ Leave Duration 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

    public static readonly DependencyProperty LeaveDurationMsProperty =
        DependencyProperty.RegisterAttached("LeaveDurationMs", typeof(int), typeof(CardAnimation),
            new PropertyMetadata(300));

    public static int GetLeaveDurationMs(DependencyObject o) => (int)o.GetValue(LeaveDurationMsProperty);
    public static void SetLeaveDurationMs(DependencyObject o, int v) => o.SetValue(LeaveDurationMsProperty, v);

    // 鈹€鈹€ Cover Duration 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

    public static readonly DependencyProperty CoverDurationMsProperty =
        DependencyProperty.RegisterAttached("CoverDurationMs", typeof(int), typeof(CardAnimation),
            new PropertyMetadata(600));

    public static int GetCoverDurationMs(DependencyObject o) => (int)o.GetValue(CoverDurationMsProperty);
    public static void SetCoverDurationMs(DependencyObject o, int v) => o.SetValue(CoverDurationMsProperty, v);

    // 鈹€鈹€ Press Scale 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

    public static readonly DependencyProperty PressScaleProperty =
        DependencyProperty.RegisterAttached("PressScale", typeof(double), typeof(CardAnimation),
            new PropertyMetadata(0.96));

    public static double GetPressScale(DependencyObject o) => (double)o.GetValue(PressScaleProperty);
    public static void SetPressScale(DependencyObject o, double v) => o.SetValue(PressScaleProperty, v);

    // 鈹€鈹€ Press Dip Duration 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

    public static readonly DependencyProperty PressDipMsProperty =
        DependencyProperty.RegisterAttached("PressDipMs", typeof(int), typeof(CardAnimation),
            new PropertyMetadata(80));

    public static int GetPressDipMs(DependencyObject o) => (int)o.GetValue(PressDipMsProperty);
    public static void SetPressDipMs(DependencyObject o, int v) => o.SetValue(PressDipMsProperty, v);

    // 鈹€鈹€ Press Recover Duration 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

    public static readonly DependencyProperty PressRecoverMsProperty =
        DependencyProperty.RegisterAttached("PressRecoverMs", typeof(int), typeof(CardAnimation),
            new PropertyMetadata(300));

    public static int GetPressRecoverMs(DependencyObject o) => (int)o.GetValue(PressRecoverMsProperty);
    public static void SetPressRecoverMs(DependencyObject o, int v) => o.SetValue(PressRecoverMsProperty, v);

    // 鈹€鈹€ Shadow Blur 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

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

    // 鈹€鈹€ Shadow Opacity 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

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

    // 鈹€鈹€ IsExiting (閫€鍑哄姩鐢讳腑锛屽喕缁?hover 鐘舵€? 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

    private static readonly DependencyProperty IsExitingProperty =
        DependencyProperty.RegisterAttached("IsExiting", typeof(bool), typeof(CardAnimation),
            new PropertyMetadata(false));

    private static bool GetIsExiting(DependencyObject o) => (bool)o.GetValue(IsExitingProperty);
    private static void SetIsExiting(DependencyObject o, bool v) => o.SetValue(IsExitingProperty, v);

    // 鈹€鈹€ Setup 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

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
            // 椤甸潰鍒囧洖鏃堕噸璁炬粸鐣欑殑 hover/press 鐘舵€?
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

        // 鍒犻櫎鎸夐挳鍒濆闅愯棌 (Opacity=0 + Scale=0)
        var deleteBtn = FindChild<Button>(border);
        if (deleteBtn != null)
        {
            deleteBtn.Opacity = 0;
            deleteBtn.IsHitTestVisible = false;
            deleteBtn.RenderTransformOrigin = new Point(0.5, 0.5);
            deleteBtn.RenderTransform = new ScaleTransform(0, 0);
            deleteBtn.PreviewMouseDown += OnDeleteBtnDown;
            deleteBtn.PreviewMouseUp += OnDeleteBtnUp;

            // 鎷︽埅鍒犻櫎鎸夐挳鐐瑰嚮锛氬厛鎾€€鍑哄姩鐢伙紝鍔ㄧ敾瀹屾垚鍚庡啀鎵ц鍛戒护銆?
            // 鍏堟妸 Command 淇濆瓨涓嬫潵鐒跺悗娓呯┖锛岄槻姝?Button 鑷姩鎵ц鍛戒护銆?
            var savedCmd = deleteBtn.Command;
            var savedParam = deleteBtn.CommandParameter;
            deleteBtn.Command = null;
            deleteBtn.Click += (s, ce) =>
            {
                // 鏍囪閫€鍑轰腑锛屽喕缁撳崱鐗?hover 鐘舵€?
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

    /// <summary>鍒囬〉杩斿洖鏃堕噸缃粸鐣欑殑 hover/press/鍒犻櫎鎸夐挳鐘舵€?/summary>
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

    // 鈹€鈹€ Hover Enter 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

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

        // 鍒犻櫎鎸夐挳鏄剧ず (scale + opacity, 鍙傝€?PopupAnimator)
        var deleteBtn = FindChild<Button>(border);
        if (deleteBtn != null && deleteBtn.RenderTransform is ScaleTransform btnScale)
        {
            deleteBtn.IsHitTestVisible = true;
            AnimationHelper.AnimateFromCurrent(deleteBtn, UIElement.OpacityProperty, 1, hoverMs, ease);
            AnimationHelper.AnimateScaleTransform(btnScale, 1, hoverMs, ease);
        }
    }

    // 鈹€鈹€ Hover Leave 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

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

        // 鍒犻櫎鎸夐挳闅愯棌 (scale + opacity, 鍙傝€?PopupAnimator)
        var deleteBtn = FindChild<Button>(border);
        if (deleteBtn != null && deleteBtn.RenderTransform is ScaleTransform btnScale)
        {
            var pos = e.GetPosition(border);
            // 濡傛灉榧犳爣浠嶅湪 Border 鑼冨洿鍐咃紙鍥犲瓙鍏冪礌鎹曡幏榧犳爣瀵艰嚧鐨勪吉 Leave锛夛紝璺宠繃闅愯棌
            if (pos.X >= 0 && pos.Y >= 0 && pos.X <= border.ActualWidth && pos.Y <= border.ActualHeight)
                return;
            deleteBtn.IsHitTestVisible = false;
            // 鍏堝浐鍖栧綋鍓嶅姩鐢诲€硷紝閬垮厤 AnimateScaleTransform 鍐呴儴 BeginAnimation(null) 鍥為€€鍒?base value
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

    // 鈹€鈹€ Press Flash 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

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

    // 鈹€鈹€ Delete Button Press (鍙傝€?ButtonScaleHover / CardAnimation press) 鈹€鈹€

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

    // 鈹€鈹€ Container Exit Animation 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

    private static void AnimateContainerExit(FrameworkElement container, int durationMs, Action onCompleted)
    {
        AnimationHelper.ApplyExit(container, ExitEffect.Default, onCompleted);
    }

    // 鈹€鈹€ Helpers 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

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




