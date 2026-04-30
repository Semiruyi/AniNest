using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LocalPlayer.Primitives;
using LocalPlayer.Model;
using LocalPlayer.View.Player.Interaction;
using LocalPlayer.View.Player;
using LocalPlayer.View.Settings;
using LocalPlayer.ViewModel;

namespace LocalPlayer.View.Player;

public partial class PlayerPage : System.Windows.Controls.UserControl, IDisposable
{
    private static void Log(string message) => AppLog.Info(nameof(PlayerPage), message);
    private static void LogError(string message, Exception? ex = null) => AppLog.Error(nameof(PlayerPage), message, ex);

    private readonly PlayerViewModel _vm;
    private readonly IMediaPlayerController _media;
    private readonly IThumbnailGenerator _thumbnailGenerator;

    private PauseOverlayController _pauseOverlay = null!;
    private RightHoldSpeedController _rightHold = null!;
    private ClickRouter _clickRouter = null!;

    private Window? parentWindow;
    private FullscreenWindow? fullscreenWindow;

    public event EventHandler? BackRequested;

    public PlayerPage(PlayerViewModel vm, IMediaPlayerController media,
                      IThumbnailGenerator thumbnailGenerator)
    {
        _vm = vm;
        _media = media;
        _thumbnailGenerator = thumbnailGenerator;

        // 将 PlayerPage 的 IMediaPlayerController 实例注入 ViewModel，确保共享同一实例
        _vm.Initialize(_media);

        DataContext = _vm;

        try
        {
            Log("PlayerPage 构造函数开始");
            InitializeComponent();
            Log("InitializeComponent 完成");
        }
        catch (Exception ex)
        {
            LogError("构造函数异常", ex);
            throw;
        }
        Loaded += PlayerPage_Loaded;
        Unloaded += PlayerPage_Unloaded;
        GotKeyboardFocus += PlayerPage_GotKeyboardFocus;
        LostKeyboardFocus += PlayerPage_LostKeyboardFocus;

        _pauseOverlay = new PauseOverlayController(PauseBigIconScale, PauseBigIcon);
        _rightHold = new RightHoldSpeedController(
            _media,
            () => _vm.Rate,
            speed => ControlBar.UpdateSpeedButtonText(speed));
        _clickRouter = new ClickRouter(
            () => _media.TogglePlayPause(),
            () => _vm.ToggleFullscreenCommand.Execute(null));

        _vm.BackRequested += () =>
        {
            _vm.SaveProgress();
            BackRequested?.Invoke(this, EventArgs.Empty);
        };
        _vm.BindingsChanged += () => ControlBar.UpdateButtonTooltips();
        _vm.FullscreenToggled += () =>
        {
            if (_vm.IsFullscreen)
                ExitFullscreen();
            else
                EnterFullscreen();
        };
        _vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PlayerViewModel.CurrentVideoPath) && _vm.CurrentVideoPath != null)
            {
                ControlBar.SetCurrentVideo(_vm.CurrentVideoPath);
                fullscreenWindow?.ControlBar?.SetCurrentVideo(_vm.CurrentVideoPath);
            }
        };
    }

    private void PlayerPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Log("Loaded 事件触发");

            parentWindow = Window.GetWindow(this);

            ControlBar.Setup(_media, _vm.InputHandler, _thumbnailGenerator);
            ControlBar.IsFullscreen = false;
            ControlBar.UpdateButtonTooltips();

            ControlBar.SpeedChanged += speed => _vm.ChangeSpeedCommand.Execute(speed);

            PlaylistPanel.EpisodeSelected += (_, item) =>
            {
                _vm.SelectEpisode(item.Number - 1);
            };

            if (fullscreenWindow == null)
            {
                fullscreenWindow = new FullscreenWindow(_vm, _media, _thumbnailGenerator);
                fullscreenWindow.ExitRequested += (_, _) => ExitFullscreen();
                fullscreenWindow.EpisodeSelected += (_, item) =>
                {
                    _vm.SelectEpisode(item.Number - 1);
                };
            }

            VideoContainer.MouseMove += VideoContainer_MouseMove;
            VideoContainer.MouseRightButtonDown += VideoContainer_MouseRightButtonDown;
            VideoContainer.MouseRightButtonUp += VideoContainer_MouseRightButtonUp;

            Keyboard.Focus(this);
            FocusManager.SetFocusedElement(this, this);

            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
            var duration = TimeSpan.FromMilliseconds(300);

            var anim = new DoubleAnimation(0, 1, duration) { EasingFunction = ease };
            anim.Completed += (_, _) =>
            {
                PageRoot.BeginAnimation(OpacityProperty, null);
                PageRoot.Opacity = 1;
            };
            PageRoot.BeginAnimation(OpacityProperty, anim);

            _media.Initialize();
            VideoImage.Source = _media.VideoBitmap;

            _pauseOverlay.WireMediaEvents(_media, Dispatcher);

            // 延迟 LoadFolder 在此执行
            if (_pendingFolderPath != null)
            {
                var path = _pendingFolderPath;
                var name = _pendingFolderName ?? "";
                _pendingFolderPath = null;
                _pendingFolderName = null;
                LoadFolder(path, name);
            }
        }
        catch (Exception ex)
        {
            LogError("Loaded 异常", ex);
            throw;
        }
    }

    private string? _pendingFolderPath;
    private string? _pendingFolderName;

    private void PlayerPage_Unloaded(object sender, RoutedEventArgs e)
    {
        Dispose();
    }

    public void LoadFolder(string folderPath, string folderName)
    {
        if (!IsLoaded)
        {
            _pendingFolderPath = folderPath;
            _pendingFolderName = folderName;
            return;
        }

        _vm.LoadFolder(folderPath, folderName);

        _ = PlaylistPanel.AnimateEpisodeButtonsEntrance();
        PlaylistPanel.SelectedIndex = _vm.CurrentIndex;
    }

    // ========== 键盘事件 ==========

    private void ProcessKeyboardEvent(KeyEventArgs e, string source)
    {
        Log($"KeyDown({source}): Key={e.Key}");
        if (_vm.HandleKeyDown(e, _vm.IsFullscreen))
            e.Handled = true;
    }

    public void HandlePreviewKeyDown(KeyEventArgs e) => ProcessKeyboardEvent(e, "PreviewKeyDown (MainWindow)");
    public void HandleKeyDown(KeyEventArgs e) => ProcessKeyboardEvent(e, "KeyDown (MainWindow)");

    private void PlayerPage_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        ProcessKeyboardEvent(e, "PP_PreviewKeyDown");
    }

    private void PlayerPage_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        ProcessKeyboardEvent(e, "PP_KeyDown");
    }

    // ========== 鼠标事件 ==========

    private void VideoContainer_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.XButton1)
        {
            if (_vm.IsFullscreen)
                ExitFullscreen();
            else
            {
                _vm.SaveProgress();
                BackRequested?.Invoke(this, EventArgs.Empty);
            }
            e.Handled = true;
        }
    }

    private void VideoContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled) return;
        Keyboard.Focus(this);
        _clickRouter.OnMouseDown(e);
    }

    private void PlayerPage_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        Log($"GotKeyboardFocus: NewFocus={e.NewFocus?.GetType().Name}");
    }

    private void PlayerPage_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        Log($"LostKeyboardFocus: NewFocus={e.NewFocus?.GetType().Name}");
    }

    // ========== 全屏切换 ==========

    private void EnterFullscreen()
    {
        if (parentWindow == null || fullscreenWindow == null) return;
        if (_vm.IsFullscreen) return;

        ControlBar.CloseSpeedPopup();

        fullscreenWindow.SetPlaylistItems(_vm.CurrentIndex);
        fullscreenWindow.SetSpeed(_vm.Rate);

        var source = PresentationSource.FromVisual(VideoContainer);
        var dpiX = source!.CompositionTarget!.TransformToDevice.M11;
        var dpiY = source!.CompositionTarget!.TransformToDevice.M22;

        Point screenPos = VideoContainer.PointToScreen(new Point(0, 0));
        var fromRect = new Rect(
            screenPos.X / dpiX, screenPos.Y / dpiY,
            VideoContainer.ActualWidth, VideoContainer.ActualHeight);

        VideoImage.Source = null;
        fullscreenWindow.ShowWithAnimation(fromRect);

        _vm.IsFullscreen = true;
        ControlBar.IsFullscreen = true;

        ControlBar.Visibility = Visibility.Collapsed;
        PlaylistPanel.Visibility = Visibility.Collapsed;
    }

    private void ExitFullscreen()
    {
        if (!_vm.IsFullscreen || fullscreenWindow == null) return;

        _vm.IsFullscreen = false;
        ControlBar.IsFullscreen = false;

        fullscreenWindow.StopAutoHideTimers();

        fullscreenWindow.HideWithAnimation();

        VideoImage.Source = _media.VideoBitmap;

        ControlBar.Visibility = Visibility.Visible;
        PlaylistPanel.Visibility = Visibility.Visible;
    }

    // ========== 非全屏时无操作 ==========
    private void VideoContainer_MouseMove(object sender, MouseEventArgs e) { }

    // ========== 右键长按三倍速 ==========

    private void VideoContainer_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        => _rightHold.OnMouseDown(e);

    private void VideoContainer_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        => _rightHold.OnMouseUp(e);

    public void Dispose()
    {
        Log("Dispose 开始");
        _vm.SaveProgress();

        ControlBar.Dispose();

        fullscreenWindow?.Close();
        fullscreenWindow = null;

        _media.Dispose();
        Log("Dispose 完成");
    }
}
