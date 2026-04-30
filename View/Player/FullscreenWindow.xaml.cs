using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LocalPlayer.Primitives;
using LocalPlayer.Model;
using LocalPlayer.Media;
using LocalPlayer.Controls;
using LocalPlayer.Interaction;
using LocalPlayer.View.Player;
using LocalPlayer.View.Settings;
using LocalPlayer.ViewModel;

using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace LocalPlayer.View.Player;

public partial class FullscreenWindow : Window
{
    private static void Log(string message) => AppLog.Info(nameof(FullscreenWindow), message);

    private readonly PlayerViewModel _vm;
    private readonly IMediaPlayerController _media;

    public event EventHandler? ExitRequested;
    public event EventHandler<PlaylistItem>? EpisodeSelected;

    private bool isAnimating;
    private bool isClosing;
    private Rect originalVideoRect;

    private PauseOverlayController _pauseOverlay = null!;
    private RightHoldSpeedController _rightHold = null!;
    private ClickRouter _clickRouter = null!;

    private readonly DispatcherTimer controlBarHideTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly DispatcherTimer playlistHideTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };

    public FullscreenWindow(PlayerViewModel vm, IMediaPlayerController media,
                             IThumbnailGenerator thumbnailGenerator)
    {
        _vm = vm;
        _media = media;
        DataContext = _vm;

        InitializeComponent();

        _pauseOverlay = new PauseOverlayController(PauseBigIconScale, PauseBigIcon);
        _clickRouter = new ClickRouter(
            () => _media.TogglePlayPause(),
            () => ExitRequested?.Invoke(this, EventArgs.Empty));

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

        SetupInternal(media, thumbnailGenerator);

        _vm.BindingsChanged += () => ControlBar.UpdateButtonTooltips();
    }

    private void SetupInternal(IMediaPlayerController mediaCtrl, IThumbnailGenerator thumbnailGenerator)
    {
        _pauseOverlay.WireMediaEvents(mediaCtrl, Dispatcher);

        ControlBar.Setup(mediaCtrl, _vm.InputHandler, thumbnailGenerator);

        _rightHold = new RightHoldSpeedController(
            mediaCtrl,
            () => ControlBar.CurrentSpeed,
            speed => ControlBar.UpdateSpeedButtonText(speed));
        ControlBar.IsFullscreen = true;

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

        HideControlBar(immediate: true);
        HidePlaylist(immediate: true);
    }

    private void VideoImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => _clickRouter.OnMouseDown(e);

    private void VideoImage_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        => _rightHold.OnMouseDown(e);

    private void VideoImage_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        => _rightHold.OnMouseUp(e);

    public void SetPlaylistItems(int selectedIndex)
    {
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

    // ========== 全屏动画 ==========

    public void ShowWithAnimation(Rect fromRect)
    {
        if (isAnimating) return;

        originalVideoRect = fromRect;
        isAnimating = true;

        var mainWindow = Application.Current.MainWindow;
        Owner = mainWindow;

        var hwnd = new WindowInteropHelper(mainWindow).Handle;
        var hwndSource = HwndSource.FromHwnd(hwnd);
        double dpiX = hwndSource.CompositionTarget.TransformToDevice.M11;
        double dpiY = hwndSource.CompositionTarget.TransformToDevice.M22;

        IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        var mi = new MONITORINFO();
        mi.cbSize = (uint)Marshal.SizeOf<MONITORINFO>();
        GetMonitorInfo(monitor, ref mi);

        Left   = mi.rcMonitor.Left   / dpiX;
        Top    = mi.rcMonitor.Top    / dpiY;
        Width  = mi.rcMonitor.Width  / dpiX;
        Height = mi.rcMonitor.Height / dpiY;

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

        PauseBigIconFSTranslate.X = fromRect.Right - Left - Width;
        PauseBigIconFSTranslate.Y = fromRect.Bottom - Top - Height;

        VideoImage.Source = _media.VideoBitmap;
        Show();

        HideControlBar(immediate: true);
        HidePlaylist(immediate: true);

        bool wasPaused = _media.IsPlaying == false;
        if (wasPaused)
            _pauseOverlay.ShowImmediate();

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
        if (_vm.HandleKeyDown(e, isFullscreen: true))
            e.Handled = true;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (isAnimating) { e.Handled = true; return; }
        if (_vm.HandleKeyDown(e, isFullscreen: true))
            e.Handled = true;
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

    // ========== P/Invoke ==========

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }
}
