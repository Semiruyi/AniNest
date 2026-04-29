using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LocalPlayer.Helpers;
using LocalPlayer.Models;
using LocalPlayer.Services;

// 消歧义：UseWindowsForms 隐式导入与 WPF 类型冲突
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Point = System.Windows.Point;

namespace LocalPlayer.Views;

public partial class FullscreenWindow : Window
{
    private static void Log(string message) => AppLog.Info(nameof(FullscreenWindow), message);

    private MediaPlayerController? mediaController;
    private PlayerInputHandler? inputHandler;

    public event EventHandler? ExitRequested;
    public event EventHandler<PlaylistItem>? EpisodeSelected;

    private bool isAnimating;
    private bool isClosing;
    private Rect originalVideoRect;

    // 右键长按三倍速
    private float speedBeforeHold = 1.0f;
    private readonly DispatcherTimer rightHoldTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private bool isRightHolding;

    // 单击/双击检测
    private readonly DispatcherTimer singleClickTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };

    // 控制栏/选集自动隐藏
    private readonly DispatcherTimer controlBarHideTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly DispatcherTimer playlistHideTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };

    public FullscreenWindow()
    {
        InitializeComponent();

        singleClickTimer.Tick += (_, _) =>
        {
            singleClickTimer.Stop();
            mediaController?.TogglePlayPause();
        };

        rightHoldTimer.Tick += (_, _) =>
        {
            rightHoldTimer.Stop();
            isRightHolding = true;
            speedBeforeHold = ControlBar.CurrentSpeed;
            mediaController!.Rate = 3.0f;
            ControlBar.UpdateSpeedButtonText(3.0f);
        };

        controlBarHideTimer.Tick += (_, _) =>
        {
            if (!ControlBar.IsMouseOver)
                HideControlBar();
        };

        playlistHideTimer.Tick += (_, _) =>
        {
            if (!PlaylistPanel.IsMouseOver)
                HidePlaylist();
        };

        PreviewMouseDown += (_, e) =>
        {
            if (isAnimating) return;
            if (e.ChangedButton == MouseButton.XButton1)
            {
                ExitRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        };

        Loaded += (_, _) =>
        {
            VideoImage.MouseLeftButtonDown += VideoImage_MouseLeftButtonDown;
            VideoImage.MouseRightButtonDown += VideoImage_MouseRightButtonDown;
            VideoImage.MouseRightButtonUp += VideoImage_MouseRightButtonUp;
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

    private void VideoImage_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        rightHoldTimer.Stop();
        rightHoldTimer.Start();
        e.Handled = true;
    }

    private void VideoImage_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        rightHoldTimer.Stop();
        if (isRightHolding)
        {
            isRightHolding = false;
            mediaController!.Rate = speedBeforeHold;
            ControlBar.UpdateSpeedButtonText(speedBeforeHold);
        }
        e.Handled = true;
    }

    // ========== 初始化 ==========

    public void Setup(MediaPlayerController mediaCtrl, PlayerInputHandler input,
                      ThumbnailGenerator thumbnailGenerator)
    {
        mediaController = mediaCtrl;
        inputHandler = input;

        mediaController.Playing += (_, _) => Dispatcher.Invoke(AnimatePauseBigOut);
        mediaController.Paused += (_, _) => Dispatcher.Invoke(AnimatePauseBigIn);
        mediaController.Stopped += (_, _) => Dispatcher.Invoke(AnimatePauseBigOut);

        ControlBar.Setup(mediaCtrl, input, thumbnailGenerator);
        ControlBar.IsFullscreen = true;

        // 控制栏按钮 → 本地处理
        ControlBar.PlayPauseClicked += (_, _) => mediaController.TogglePlayPause();
        ControlBar.PreviousClicked += (_, _) => { }; // PlayerPage handles via events
        ControlBar.NextClicked += (_, _) => { };
        ControlBar.StopClicked += (_, _) => mediaController.Stop();
        ControlBar.FullscreenClicked += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        ControlBar.SettingsClicked += (_, _) =>
        {
            var window = new KeyBindingsWindow(input)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            window.ShowDialog();
            ControlBar.UpdateButtonTooltips();
        };
        ControlBar.SeekRequested += time => mediaController.SeekTo(time);

        // 控制栏鼠标进入/离开 → 自动隐藏控制
        ControlBar.ControlBarMouseEnter += (_, _) =>
        {
            controlBarHideTimer.Stop();
            ShowControlBar();
        };
        ControlBar.ControlBarMouseLeave += (_, _) =>
        {
            if (!ControlBar.IsMouseOver)
                controlBarHideTimer.Start();
        };

        // 选集面板事件
        PlaylistPanel.EpisodeSelected += (_, item) => EpisodeSelected?.Invoke(this, item);
        PlaylistPanel.MouseEnterBorder += (_, _) =>
        {
            playlistHideTimer.Stop();
            ShowPlaylist();
        };
        PlaylistPanel.MouseLeaveBorder += (_, _) =>
        {
            if (!PlaylistPanel.IsMouseOver)
                playlistHideTimer.Start();
        };

        // 初始隐藏控制栏和选集
        HideControlBar(immediate: true);
        HidePlaylist(immediate: true);
    }

    public void SetPlaylistItems(IEnumerable<PlaylistItem> items, int selectedIndex)
    {
        PlaylistPanel.SetItems(items);
        PlaylistPanel.SelectedIndex = selectedIndex;
    }

    public void SetSpeed(float speed)
    {
        ControlBar.SetSpeed(speed);
    }

    public void StopAutoHideTimers()
    {
        controlBarHideTimer.Stop();
        playlistHideTimer.Stop();
        ShowControlBar();
        ShowPlaylist();
    }

    // ========== 进入全屏动画 ==========

    public void ShowWithAnimation(Rect fromRect)
    {
        if (isAnimating) return;

        originalVideoRect = fromRect;
        isAnimating = true;

        var mainWindow = System.Windows.Application.Current.MainWindow;
        Owner = mainWindow;

        var hwnd = new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle;
        var screen = System.Windows.Forms.Screen.FromHandle(hwnd);
        using var g = System.Drawing.Graphics.FromHwnd(hwnd);
        float targetDpiX = g.DpiX;
        float targetDpiY = g.DpiY;

        Left   = screen.Bounds.Left   * 96.0 / targetDpiX;
        Top    = screen.Bounds.Top    * 96.0 / targetDpiY;
        Width  = screen.Bounds.Width  * 96.0 / targetDpiX;
        Height = screen.Bounds.Height * 96.0 / targetDpiY;

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

        // 暂停大图标位移
        PauseBigIconFSTranslate.X = fromRect.Right - Left - Width;
        PauseBigIconFSTranslate.Y = fromRect.Bottom - Top - Height;

        VideoImage.Source = mediaController!.VideoBitmap;
        Show();

        bool wasPaused = mediaController?.IsPlaying == false;
        if (wasPaused)
        {
            PauseBigIconScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            PauseBigIconScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            PauseBigIcon.BeginAnimation(OpacityProperty, null);
            PauseBigIconScale.ScaleX = 1;
            PauseBigIconScale.ScaleY = 1;
            PauseBigIcon.Opacity = 1;
        }

        Keyboard.Focus(this);

        var duration = TimeSpan.FromMilliseconds(380);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

        var sx = new DoubleAnimation(scaleX, 1.0, duration) { EasingFunction = ease };
        var sy = new DoubleAnimation(scaleY, 1.0, duration) { EasingFunction = ease };
        var tx = new DoubleAnimation(transX, 0.0, duration) { EasingFunction = ease };
        var ty = new DoubleAnimation(transY, 0.0, duration) { EasingFunction = ease };

        var iconTX = new DoubleAnimation(PauseBigIconFSTranslate.X, 0.0, duration) { EasingFunction = ease };
        var iconTY = new DoubleAnimation(PauseBigIconFSTranslate.Y, 0.0, duration) { EasingFunction = ease };

        sx.Completed += (_, _) =>
        {
            VideoImage.BeginAnimation(UIElement.RenderTransformProperty, null);
            VideoImage.RenderTransform = Transform.Identity;

            PauseBigIconFSTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            PauseBigIconFSTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            PauseBigIconFSTranslate.X = 0;
            PauseBigIconFSTranslate.Y = 0;

            isAnimating = false;
        };

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
        translate.BeginAnimation(TranslateTransform.XProperty, tx);
        translate.BeginAnimation(TranslateTransform.YProperty, ty);

        PauseBigIconFSTranslate.BeginAnimation(TranslateTransform.XProperty, iconTX);
        PauseBigIconFSTranslate.BeginAnimation(TranslateTransform.YProperty, iconTY);
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

        PauseBigIconFSTranslate.X = 0;
        PauseBigIconFSTranslate.Y = 0;

        var duration = TimeSpan.FromMilliseconds(350);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

        var sx = new DoubleAnimation(1.0, scaleX, duration) { EasingFunction = ease };
        var sy = new DoubleAnimation(1.0, scaleY, duration) { EasingFunction = ease };
        var tx = new DoubleAnimation(0.0, transX, duration) { EasingFunction = ease };
        var ty = new DoubleAnimation(0.0, transY, duration) { EasingFunction = ease };

        var iconTX = new DoubleAnimation(0.0, originalVideoRect.Right - Left - Width, duration) { EasingFunction = ease };
        var iconTY = new DoubleAnimation(0.0, originalVideoRect.Bottom - Top - Height, duration) { EasingFunction = ease };

        sx.Completed += (_, _) =>
        {
            VideoImage.BeginAnimation(UIElement.RenderTransformProperty, null);
            VideoImage.RenderTransform = Transform.Identity;

            PauseBigIconFSTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            PauseBigIconFSTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            PauseBigIconFSTranslate.X = 0;
            PauseBigIconFSTranslate.Y = 0;

            Hide();
            VideoImage.Source = null;
            isAnimating = false;
            isClosing = false;
        };

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
        translate.BeginAnimation(TranslateTransform.XProperty, tx);
        translate.BeginAnimation(TranslateTransform.YProperty, ty);

        PauseBigIconFSTranslate.BeginAnimation(TranslateTransform.XProperty, iconTX);
        PauseBigIconFSTranslate.BeginAnimation(TranslateTransform.YProperty, iconTY);
    }

    // ========== 鼠标边缘检测 ==========

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (isAnimating || isClosing) return;
        var pos = e.GetPosition(this);
        if (pos.Y > ActualHeight - 10)
        {
            controlBarHideTimer.Stop();
            ShowControlBar();
        }
        if (pos.X > ActualWidth - 10)
        {
            playlistHideTimer.Stop();
            ShowPlaylist();
        }
    }

    // ========== 控制栏显隐 ==========

    private void ShowControlBar()
    {
        controlBarHideTimer.Stop();
        if (ControlBar.IsHitTestVisible) return;
        ControlBar.Visibility = Visibility.Visible;
        ControlBar.IsHitTestVisible = true;
        AnimateOpacity(ControlBar, 1);
        Keyboard.Focus(ControlBar);
    }

    private void HideControlBar(bool immediate = false)
    {
        if (immediate)
        {
            ControlBar.BeginAnimation(UIElement.OpacityProperty, null);
            ControlBar.Opacity = 0;
            ControlBar.IsHitTestVisible = false;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(200);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var anim = new DoubleAnimation(ControlBar.Opacity, 0, duration) { EasingFunction = ease };
        anim.Completed += (_, _) =>
        {
            if (!isClosing)
                ControlBar.IsHitTestVisible = false;
        };
        ControlBar.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    // ========== 选集面板显隐 ==========

    private void ShowPlaylist()
    {
        playlistHideTimer.Stop();
        if (PlaylistPanel.IsHitTestVisible) return;
        PlaylistPanel.IsHitTestVisible = true;
        AnimateOpacity(PlaylistPanel, 1);
    }

    private void HidePlaylist(bool immediate = false)
    {
        if (immediate)
        {
            PlaylistPanel.BeginAnimation(UIElement.OpacityProperty, null);
            PlaylistPanel.Opacity = 0;
            PlaylistPanel.IsHitTestVisible = false;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(200);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var anim = new DoubleAnimation(PlaylistPanel.Opacity, 0, duration) { EasingFunction = ease };
        anim.Completed += (_, _) =>
        {
            if (!isClosing)
                PlaylistPanel.IsHitTestVisible = false;
        };
        PlaylistPanel.BeginAnimation(UIElement.OpacityProperty, anim);
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

    // ========== 暂停大图标动画 ==========

    private void AnimatePauseBigIn()
    {
        PauseBigIconScale.ScaleX = 0;
        PauseBigIconScale.ScaleY = 0;
        PauseBigIcon.Opacity = 0;
        AnimationHelper.AnimateScaleTransform(PauseBigIconScale, 1, 250, AnimationHelper.EaseOut);
        AnimationHelper.Animate(PauseBigIcon, UIElement.OpacityProperty, 0, 1, 250, AnimationHelper.EaseOut);
    }

    private void AnimatePauseBigOut()
    {
        AnimationHelper.AnimateScaleTransform(PauseBigIconScale, 0, 180, AnimationHelper.EaseIn);
        AnimationHelper.AnimateFromCurrent(PauseBigIcon, UIElement.OpacityProperty, 0, 180, AnimationHelper.EaseIn);
    }

    // ========== 工具 ==========

    private static void AnimateOpacity(UIElement element, double target, int durationMs = 200)
        => AnimationHelper.AnimateFromCurrent(element, UIElement.OpacityProperty, target, durationMs);

    // ========== 清理 ==========

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        VideoImage.Source = null;
    }
}
