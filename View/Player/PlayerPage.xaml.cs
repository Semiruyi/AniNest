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
using LocalPlayer.Media;
using LocalPlayer.Controls;
using LocalPlayer.Interaction;
using LocalPlayer.View.Player;
using LocalPlayer.View.Settings;


namespace LocalPlayer.View.Player;

public partial class PlayerPage : System.Windows.Controls.UserControl, IDisposable
{
    private static void Log(string message) => AppLog.Info(nameof(PlayerPage), message);
    private static void LogError(string message, Exception? ex = null) => AppLog.Error(nameof(PlayerPage), message, ex);

    private readonly IMediaPlayerController mediaController;
    private readonly ISettingsService settingsService;
    private readonly PlayerInputHandler inputHandler;
    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly PlaylistManager playlistManager;
    private readonly DispatcherTimer saveProgressTimer;

    private PauseOverlayController _pauseOverlay = null!;
    private RightHoldSpeedController _rightHold = null!;
    private ClickRouter _clickRouter = null!;

    private string? pendingLoadFolderPath;
    private string? pendingLoadFolderName;
    private bool _updatingSelection;

    private float currentSpeed = 1.0f;

    private Window? parentWindow;
    private bool isFullscreen = false;
    private FullscreenWindow? fullscreenWindow;

    public event EventHandler? BackRequested;

    public PlayerPage(ISettingsService settings, IMediaPlayerController media,
                      IThumbnailGenerator thumbnailGenerator)
    {
        settingsService = settings;
        mediaController = media;
        _thumbnailGenerator = thumbnailGenerator;
        inputHandler = new PlayerInputHandler(settings);

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

        playlistManager = new PlaylistManager(
            settingsService, mediaController,
            path => _thumbnailGenerator.GetState(path));

        saveProgressTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        saveProgressTimer.Tick += (_, _) => playlistManager.SaveProgress();

        _pauseOverlay = new PauseOverlayController(PauseBigIconScale, PauseBigIcon);
        _rightHold = new RightHoldSpeedController(
            mediaController,
            () => currentSpeed,
            speed => ControlBar.UpdateSpeedButtonText(speed));
        _clickRouter = new ClickRouter(
            () => mediaController.TogglePlayPause(),
            () => ToggleFullscreen());

        inputHandler.TogglePlayPause += (_, _) => mediaController.TogglePlayPause();
        inputHandler.SeekForward += (_, _) => mediaController.SeekForward(5000);
        inputHandler.SeekBackward += (_, _) => mediaController.SeekBackward(5000);
        inputHandler.Back += (_, _) =>
        {
            playlistManager.SaveProgress();
            BackRequested?.Invoke(this, EventArgs.Empty);
        };
        inputHandler.NextEpisode += (_, _) => PlayNext();
        inputHandler.PreviousEpisode += (_, _) => PlayPrevious();
        inputHandler.ToggleFullscreen += (_, _) => ToggleFullscreen();
        inputHandler.ExitFullscreen += (_, _) => ExitFullscreen();

        inputHandler.ReloadBindings();

        _thumbnailGenerator.VideoReady += path =>
            Dispatcher.Invoke(() => playlistManager.UpdateThumbnailReady(path));
        _thumbnailGenerator.VideoProgress += (path, percent) =>
            Dispatcher.Invoke(() => playlistManager.UpdateThumbnailProgress(path, percent));

        playlistManager.VideoPlayed += filePath =>
        {
            ControlBar.SetCurrentVideo(filePath);
            fullscreenWindow?.ControlBar?.SetCurrentVideo(filePath);
        };
    }

    private void PlayerPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Log("Loaded 事件触发");

            parentWindow = Window.GetWindow(this);

            ControlBar.Setup(mediaController, inputHandler, _thumbnailGenerator);
            ControlBar.IsFullscreen = false;
            ControlBar.UpdateButtonTooltips();

            ControlBar.PlayPauseClicked += (_, _) => mediaController.TogglePlayPause();
            ControlBar.PreviousClicked += (_, _) => PlayPrevious();
            ControlBar.NextClicked += (_, _) => PlayNext();
            ControlBar.StopClicked += (_, _) => mediaController.Stop();
            ControlBar.FullscreenClicked += (_, _) => ToggleFullscreen();
            ControlBar.PlaylistToggleClicked += (_, _) =>
            {
                PlaylistPanel.Visibility = PlaylistPanel.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };
            ControlBar.SettingsClicked += (_, _) =>
            {
                var window = new KeyBindingsWindow(inputHandler)
                {
                    Owner = Window.GetWindow(this)
                };
                window.ShowDialog();
                ControlBar.UpdateButtonTooltips();
            };
            ControlBar.SpeedChanged += speed => currentSpeed = speed;
            ControlBar.SeekRequested += time => mediaController.SeekTo(time);

            PlaylistPanel.EpisodeSelected += (_, item) =>
            {
                if (_updatingSelection)
                {
                    _updatingSelection = false;
                    return;
                }
                playlistManager.PlayEpisode(item.Number - 1);
            };

            if (fullscreenWindow == null)
            {
                fullscreenWindow = new FullscreenWindow();
                fullscreenWindow.Setup(mediaController, inputHandler, _thumbnailGenerator);
                fullscreenWindow.ExitRequested += (_, _) => ExitFullscreen();
                fullscreenWindow.EpisodeSelected += (_, item) =>
                {
                    if (_updatingSelection)
                    {
                        _updatingSelection = false;
                        return;
                    }
                    playlistManager.PlayEpisode(item.Number - 1);
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

            mediaController.Initialize();
            VideoImage.Source = mediaController.VideoBitmap;

            _pauseOverlay.WireMediaEvents(mediaController, Dispatcher);

            saveProgressTimer.Start();

            if (pendingLoadFolderPath != null)
            {
                var path = pendingLoadFolderPath;
                var name = pendingLoadFolderName ?? "";
                pendingLoadFolderPath = null;
                pendingLoadFolderName = null;
                LoadFolder(path, name);
            }
        }
        catch (Exception ex)
        {
            LogError("Loaded 异常", ex);
            throw;
        }
    }

    private void PlayerPage_Unloaded(object sender, RoutedEventArgs e)
    {
        Dispose();
    }

    public void LoadFolder(string folderPath, string folderName)
    {
        if (!IsLoaded)
        {
            pendingLoadFolderPath = folderPath;
            pendingLoadFolderName = folderName;
            return;
        }

        playlistManager.LoadFolder(folderPath, folderName);

        PlaylistPanel.SetItems(playlistManager.Items);
        _ = PlaylistPanel.AnimateEpisodeButtonsEntrance();
        PlaylistPanel.SelectedIndex = playlistManager.CurrentIndex;
    }

    private void PlayNext()
    {
        if (playlistManager.PlayNext())
        {
            _updatingSelection = true;
            PlaylistPanel.SelectedIndex = playlistManager.CurrentIndex;
        }
    }

    private void PlayPrevious()
    {
        if (playlistManager.PlayPrevious())
        {
            _updatingSelection = true;
            PlaylistPanel.SelectedIndex = playlistManager.CurrentIndex;
        }
    }

    // ========== 键盘事件 ==========

    private void ProcessKeyboardEvent(KeyEventArgs e, string source)
    {
        Log($"KeyDown({source}): Key={e.Key}");
        if (inputHandler.HandleKeyDown(e, isFullscreen))
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
            if (isFullscreen)
                ExitFullscreen();
            else
            {
                playlistManager.SaveProgress();
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

    private void ToggleFullscreen()
    {
        if (isFullscreen)
            ExitFullscreen();
        else
            EnterFullscreen();
    }

    private void EnterFullscreen()
    {
        if (parentWindow == null || fullscreenWindow == null) return;
        if (isFullscreen) return;

        ControlBar.CloseSpeedPopup();

        fullscreenWindow.SetPlaylistItems(playlistManager.Items, PlaylistPanel.SelectedIndex);
        fullscreenWindow.SetSpeed(ControlBar.CurrentSpeed);

        var source = PresentationSource.FromVisual(VideoContainer);
        var dpiX = source!.CompositionTarget!.TransformToDevice.M11;
        var dpiY = source!.CompositionTarget!.TransformToDevice.M22;

        Point screenPos = VideoContainer.PointToScreen(new Point(0, 0));
        var fromRect = new Rect(
            screenPos.X / dpiX, screenPos.Y / dpiY,
            VideoContainer.ActualWidth, VideoContainer.ActualHeight);

        VideoImage.Source = null;
        fullscreenWindow.ShowWithAnimation(fromRect);

        isFullscreen = true;
        ControlBar.IsFullscreen = true;

        ControlBar.Visibility = Visibility.Collapsed;
        PlaylistPanel.Visibility = Visibility.Collapsed;
    }

    private void ExitFullscreen()
    {
        if (!isFullscreen || fullscreenWindow == null) return;

        isFullscreen = false;
        ControlBar.IsFullscreen = false;

        fullscreenWindow.StopAutoHideTimers();

        fullscreenWindow.HideWithAnimation();

        VideoImage.Source = mediaController.VideoBitmap;

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
        saveProgressTimer.Stop();
        playlistManager.SaveProgress();

        ControlBar.Dispose();

        fullscreenWindow?.Close();
        fullscreenWindow = null;

        mediaController.Dispose();
        Log("Dispose 完成");
    }
}
