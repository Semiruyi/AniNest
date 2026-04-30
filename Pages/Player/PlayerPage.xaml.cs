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
using LocalPlayer.UI.Primitives;
using LocalPlayer.Domain;
using LocalPlayer.Infrastructure;
using LocalPlayer.PlayerKit;
using LocalPlayer.Pages.Player;
using LocalPlayer.Pages.Settings;

// 消歧义：UseWindowsForms 隐式导入与 WPF 类型冲突
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Point = System.Windows.Point;
using Panel = System.Windows.Controls.Panel;

namespace LocalPlayer.Pages.Player;

public partial class PlayerPage : System.Windows.Controls.UserControl, IDisposable
{
    private static void Log(string message) => AppLog.Info(nameof(PlayerPage), message);
    private static void LogError(string message, Exception? ex = null) => AppLog.Error(nameof(PlayerPage), message, ex);

    private readonly MediaPlayerController mediaController = new();
    private readonly SettingsService settingsService = SettingsService.Instance;
    private readonly PlayerInputHandler inputHandler = new();
    private readonly ThumbnailGenerator _thumbnailGenerator = ThumbnailGenerator.Instance;
    private readonly DispatcherTimer saveProgressTimer;

    private PauseOverlayController _pauseOverlay = null!;
    private RightHoldSpeedController _rightHold = null!;
    private ClickRouter _clickRouter = null!;

    private string currentFolderPath = "";
    private string currentFolderName = "";
    private string[] videoFiles = Array.Empty<string>();
    private List<PlaylistItem> _playlistItems = new();
    private string? pendingLoadFolderPath;
    private string? pendingLoadFolderName;

    private float currentSpeed = 1.0f;

    private Window? parentWindow;
    private bool isFullscreen = false;
    private FullscreenWindow? fullscreenWindow;

    public event EventHandler? BackRequested;

    public PlayerPage()
    {
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

        saveProgressTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        saveProgressTimer.Tick += SaveProgressTimer_Tick;

        // 三个共享控制器
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
            SaveCurrentProgress();
            BackRequested?.Invoke(this, EventArgs.Empty);
        };
        inputHandler.NextEpisode += (_, _) => PlayNext();
        inputHandler.PreviousEpisode += (_, _) => PlayPrevious();
        inputHandler.ToggleFullscreen += (_, _) => ToggleFullscreen();
        inputHandler.ExitFullscreen += (_, _) => ExitFullscreen();

        inputHandler.ReloadBindings();

        _thumbnailGenerator.VideoReady += OnVideoThumbnailReady;
        _thumbnailGenerator.VideoProgress += OnVideoThumbnailProgress;
    }

    private void OnVideoThumbnailProgress(string videoPath, int percent)
    {
        Dispatcher.Invoke(() =>
        {
            foreach (var pi in _playlistItems)
            {
                if (string.Equals(pi.FilePath, videoPath, StringComparison.OrdinalIgnoreCase))
                {
                    pi.ThumbnailProgress = percent;
                    break;
                }
            }
        });
    }

    private void OnVideoThumbnailReady(string videoPath)
    {
        Dispatcher.Invoke(() =>
        {
            foreach (var pi in _playlistItems)
            {
                if (string.Equals(pi.FilePath, videoPath, StringComparison.OrdinalIgnoreCase))
                {
                    pi.IsThumbnailReady = true;
                    break;
                }
            }
        });
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

            // 控制栏按钮事件
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

            // 选集面板事件
            PlaylistPanel.EpisodeSelected += (_, item) =>
            {
                int index = item.Number - 1;
                if (index >= 0 && index < videoFiles.Length)
                {
                    SaveCurrentProgress();
                    PlayVideo(videoFiles[index]);
                }
            };

            // 创建全屏窗口（只创建一次，复用）
            if (fullscreenWindow == null)
            {
                fullscreenWindow = new FullscreenWindow();
                fullscreenWindow.Setup(mediaController, inputHandler, _thumbnailGenerator);
                fullscreenWindow.ExitRequested += (_, _) => ExitFullscreen();
                fullscreenWindow.EpisodeSelected += (_, item) =>
                {
                    int index = item.Number - 1;
                    if (index >= 0 && index < videoFiles.Length)
                    {
                        SaveCurrentProgress();
                        PlayVideo(videoFiles[index]);
                    }
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

            mediaController.Playing += (_, _) => Dispatcher.Invoke(_pauseOverlay.AnimateOut);
            mediaController.Paused += (_, _) => Dispatcher.Invoke(_pauseOverlay.AnimateIn);
            mediaController.Stopped += (_, _) => Dispatcher.Invoke(_pauseOverlay.AnimateOut);

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

        currentFolderPath = folderPath;
        currentFolderName = folderName;

        videoFiles = VideoScanner.GetVideoFiles(folderPath);
        Log($"扫描到 {videoFiles.Length} 个视频文件");

        _playlistItems = new List<PlaylistItem>();
        for (int i = 0; i < videoFiles.Length; i++)
        {
            var filePath = videoFiles[i];
            _playlistItems.Add(new PlaylistItem
            {
                Number = i + 1,
                Title = Path.GetFileName(filePath),
                FilePath = filePath,
                IsPlayed = settingsService.IsVideoPlayed(filePath),
                IsThumbnailReady = _thumbnailGenerator.GetState(filePath) == ThumbnailState.Ready
            });
        }

        PlaylistPanel.SetItems(_playlistItems);
        _ = PlaylistPanel.AnimateEpisodeButtonsEntrance();

        var folderProgress = settingsService.GetFolderProgress(folderPath);
        string? targetVideo = folderProgress?.LastVideoPath;

        if (string.IsNullOrEmpty(targetVideo) || !File.Exists(targetVideo))
        {
            targetVideo = videoFiles.Length > 0 ? videoFiles[0] : null;
        }

        if (!string.IsNullOrEmpty(targetVideo))
        {
            int index = Array.IndexOf(videoFiles, targetVideo);
            if (index >= 0)
            {
                PlaylistPanel.SelectedIndex = index;
            }
            else
            {
                PlayVideo(targetVideo);
            }
        }
    }

    private void PlayVideo(string filePath)
    {
        Log($"[PlayVideo] 开始: {Path.GetFileName(filePath)}");

        ControlBar.SetCurrentVideo(filePath);
        fullscreenWindow?.ControlBar?.SetCurrentVideo(filePath);

        long startTime = 0;
        var progress = settingsService.GetVideoProgress(filePath);
        if (progress != null)
        {
            startTime = progress.Position;
            if (progress.Duration > 0 && startTime > progress.Duration * 0.9)
            {
                startTime = 0;
            }
        }

        mediaController.Play(filePath, startTime);
        settingsService.SetFolderProgress(currentFolderPath, filePath);
        settingsService.MarkVideoPlayed(filePath);
    }

    private void PlayNext()
    {
        int idx = PlaylistPanel.SelectedIndex;
        if (idx < PlaylistPanel.ItemCount - 1)
        {
            var oldItem = PlaylistPanel.SelectedItem;
            if (oldItem != null)
                oldItem.IsPlayed = true;
            PlaylistPanel.SelectedIndex = idx + 1;
        }
    }

    private void PlayPrevious()
    {
        int idx = PlaylistPanel.SelectedIndex;
        if (idx > 0)
        {
            var oldItem = PlaylistPanel.SelectedItem;
            if (oldItem != null)
                oldItem.IsPlayed = true;
            PlaylistPanel.SelectedIndex = idx - 1;
        }
    }

    private void SaveProgressTimer_Tick(object? sender, EventArgs e)
    {
        SaveCurrentProgress();
    }

    private void SaveCurrentProgress()
    {
        string? filePath = mediaController.CurrentFilePath;
        if (string.IsNullOrEmpty(filePath)) return;

        long time = mediaController.Time;
        long length = mediaController.Length;
        if (length > 0)
        {
            settingsService.SetVideoProgress(filePath, time, length);
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
                SaveCurrentProgress();
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

        // 同步选集到全屏窗口
        fullscreenWindow.SetPlaylistItems(_playlistItems, PlaylistPanel.SelectedIndex);
        fullscreenWindow.SetSpeed(ControlBar.CurrentSpeed);

        // 记录 VideoContainer 屏幕位置（DIP），用于退出回缩动画
        var source = PresentationSource.FromVisual(VideoContainer);
        var dpiX = source!.CompositionTarget!.TransformToDevice.M11;
        var dpiY = source!.CompositionTarget!.TransformToDevice.M22;

        Point screenPos = VideoContainer.PointToScreen(new Point(0, 0));
        var fromRect = new Rect(
            screenPos.X / dpiX, screenPos.Y / dpiY,
            VideoContainer.ActualWidth, VideoContainer.ActualHeight);

        // WriteableBitmap 切给全屏窗口
        VideoImage.Source = null;
        fullscreenWindow.ShowWithAnimation(fromRect);

        isFullscreen = true;
        ControlBar.IsFullscreen = true;

        // 隐藏正常模式下的控制栏和选集
        ControlBar.Visibility = Visibility.Collapsed;
        PlaylistPanel.Visibility = Visibility.Collapsed;
    }

    private void ExitFullscreen()
    {
        if (!isFullscreen || fullscreenWindow == null) return;

        isFullscreen = false;
        ControlBar.IsFullscreen = false;

        // 停止全屏窗口的自动隐藏定时器
        fullscreenWindow.StopAutoHideTimers();

        // 全屏窗口播放回缩动画并隐藏
        fullscreenWindow.HideWithAnimation();

        // WriteableBitmap 切回主窗口
        VideoImage.Source = mediaController.VideoBitmap;

        // 恢复正常模式下的控制栏和选集
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
        SaveCurrentProgress();

        ControlBar.Dispose();

        fullscreenWindow?.Close();
        fullscreenWindow = null;

        mediaController.Dispose();
        Log("Dispose 完成");
    }
}
