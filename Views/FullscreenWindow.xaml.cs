using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LocalPlayer.Services;

// 消歧义：UseWindowsForms 隐式导入与 WPF 类型冲突
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Point = System.Windows.Point;

namespace LocalPlayer.Views;

public partial class FullscreenWindow : Window
{
    // ========== 外部依赖 ==========

    private MediaPlayerController? mediaController;
    private PlayerInputHandler? inputHandler;

    // ========== 事件 ==========

    /// <summary>退出全屏按钮点击 或 Escape 键触发</summary>
    public event EventHandler? ExitRequested;

    /// <summary>鼠标靠近底部边缘 → PlayerPage 显示控制栏</summary>
    public event EventHandler? ControlBarShowRequested;

    /// <summary>鼠标靠近右侧边缘 → PlayerPage 显示选集面板</summary>
    public event EventHandler? PlaylistShowRequested;

    // ========== 状态 ==========

    private bool isAnimating;
    private bool isClosing;
    private Rect originalVideoRect;

    // 单击/双击检测
    private readonly DispatcherTimer singleClickTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };

    public FullscreenWindow()
    {
        InitializeComponent();
        singleClickTimer.Tick += (_, _) =>
        {
            singleClickTimer.Stop();
            mediaController?.TogglePlayPause();
        };
        Loaded += (_, _) =>
        {
            VideoImage.MouseLeftButtonDown += VideoImage_MouseLeftButtonDown;
            Keyboard.Focus(this);
        };
    }

    private void VideoImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            singleClickTimer.Stop();
            ExitRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }
        singleClickTimer.Stop();
        singleClickTimer.Start();
    }

    // ========== 初始化 ==========

    public void Setup(MediaPlayerController mediaCtrl, PlayerInputHandler input)
    {
        mediaController = mediaCtrl;
        inputHandler = input;
    }

    // ========== 进入全屏动画 ==========

    public void ShowWithAnimation(Rect fromRect)
    {
        if (isAnimating) return;

        originalVideoRect = fromRect;
        isAnimating = true;

        // 绑定 Owner，确保同屏同 DPI
        var mainWindow = System.Windows.Application.Current.MainWindow;
        Owner = mainWindow;

        // WinForms Graphics 获取目标屏幕真实 DPI，像素 → DIP
        var hwnd = new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle;
        var screen = System.Windows.Forms.Screen.FromHandle(hwnd);
        using var g = System.Drawing.Graphics.FromHwnd(hwnd);
        float targetDpiX = g.DpiX;
        float targetDpiY = g.DpiY;

        Left   = screen.Bounds.Left   * 96.0 / targetDpiX;
        Top    = screen.Bounds.Top    * 96.0 / targetDpiY;
        Width  = screen.Bounds.Width  * 96.0 / targetDpiX;
        Height = screen.Bounds.Height * 96.0 / targetDpiY;

        // 计算"退回到原始位置"的变换
        double origCenterX = fromRect.Left + fromRect.Width / 2;
        double origCenterY = fromRect.Top + fromRect.Height / 2;
        double finalCenterX = Left + Width / 2;
        double finalCenterY = Top + Height / 2;

        double scaleX = fromRect.Width / Width;
        double scaleY = fromRect.Height / Height;
        double transX = origCenterX - finalCenterX;
        double transY = origCenterY - finalCenterY;

        var scale = new ScaleTransform(scaleX, scaleY);
        var translate = new TranslateTransform(transX, transY);
        var group = new TransformGroup();
        group.Children.Add(scale);
        group.Children.Add(translate);
        VideoImage.RenderTransformOrigin = new Point(0.5, 0.5);
        VideoImage.RenderTransform = group;

        VideoImage.Source = mediaController!.VideoBitmap;
        Show();
        Keyboard.Focus(this);

        var duration = TimeSpan.FromMilliseconds(380);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

        var sx = new DoubleAnimation(scaleX, 1.0, duration) { EasingFunction = ease };
        var sy = new DoubleAnimation(scaleY, 1.0, duration) { EasingFunction = ease };
        var tx = new DoubleAnimation(transX, 0.0, duration) { EasingFunction = ease };
        var ty = new DoubleAnimation(transY, 0.0, duration) { EasingFunction = ease };

        sx.Completed += (_, _) =>
        {
            VideoImage.BeginAnimation(UIElement.RenderTransformProperty, null);
            VideoImage.RenderTransform = Transform.Identity;
            isAnimating = false;
        };

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
        translate.BeginAnimation(TranslateTransform.XProperty, tx);
        translate.BeginAnimation(TranslateTransform.YProperty, ty);
    }

    // ========== 退出全屏动画 ==========

    public void HideWithAnimation()
    {
        if (isAnimating || isClosing) return;

        isAnimating = true;
        isClosing = true;

        double finalW = Width;
        double finalH = Height;
        double finalCenterX = Left + finalW / 2;
        double finalCenterY = Top + finalH / 2;

        double targetW = originalVideoRect.Width;
        double targetH = originalVideoRect.Height;
        double targetCenterX = originalVideoRect.Left + targetW / 2;
        double targetCenterY = originalVideoRect.Top + targetH / 2;

        double scaleX = targetW / finalW;
        double scaleY = targetH / finalH;
        double transX = targetCenterX - finalCenterX;
        double transY = targetCenterY - finalCenterY;

        var scale = new ScaleTransform(1.0, 1.0);
        var translate = new TranslateTransform(0.0, 0.0);
        var group = new TransformGroup();
        group.Children.Add(scale);
        group.Children.Add(translate);
        VideoImage.RenderTransformOrigin = new Point(0.5, 0.5);
        VideoImage.RenderTransform = group;

        var duration = TimeSpan.FromMilliseconds(350);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

        var sx = new DoubleAnimation(1.0, scaleX, duration) { EasingFunction = ease };
        var sy = new DoubleAnimation(1.0, scaleY, duration) { EasingFunction = ease };
        var tx = new DoubleAnimation(0.0, transX, duration) { EasingFunction = ease };
        var ty = new DoubleAnimation(0.0, transY, duration) { EasingFunction = ease };

        sx.Completed += (_, _) =>
        {
            VideoImage.BeginAnimation(UIElement.RenderTransformProperty, null);
            VideoImage.RenderTransform = Transform.Identity;
            Hide();
            VideoImage.Source = null;
            isAnimating = false;
            isClosing = false;
        };

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
        translate.BeginAnimation(TranslateTransform.XProperty, tx);
        translate.BeginAnimation(TranslateTransform.YProperty, ty);
    }

    // ========== 鼠标边缘检测 ==========

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (isAnimating || isClosing) return;
        var pos = e.GetPosition(this);
        if (pos.Y > ActualHeight - 10) ControlBarShowRequested?.Invoke(this, EventArgs.Empty);
        if (pos.X > ActualWidth - 10) PlaylistShowRequested?.Invoke(this, EventArgs.Empty);
    }

    // ========== 键盘 ==========

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (isAnimating) { e.Handled = true; return; }
        if (inputHandler?.HandleKeyDown(e, isFullscreen: true) == true)
            e.Handled = true;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (isAnimating) { e.Handled = true; return; }
        if (inputHandler?.HandleKeyDown(e, isFullscreen: true) == true)
            e.Handled = true;
    }

    // ========== 控制栏/选集按钮回调 ==========

    /// <summary>由 PlayerPage 注入，全屏内按钮点击通过 inputHandler 事件链回到 PlayerPage</summary>
    public void TriggerExitFullscreen() => ExitRequested?.Invoke(this, EventArgs.Empty);

    // ========== 清理 ==========

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        VideoImage.Source = null;
    }
}
