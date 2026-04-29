using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LocalPlayer.Helpers;
using LocalPlayer.Models;
using LocalPlayer.Services;

namespace LocalPlayer.Views;

public partial class PlayerPage : System.Windows.Controls.UserControl, IDisposable
{
    private static void Log(string message) => AppLog.Info(nameof(PlayerPage), message);
    private static void LogError(string message, Exception? ex = null) => AppLog.Error(nameof(PlayerPage), message, ex);

    private readonly MediaPlayerController mediaController = new();
    private readonly SettingsService settingsService = SettingsService.Instance;
    private readonly PlayerInputHandler inputHandler = new();
    private readonly DispatcherTimer saveProgressTimer;
    private readonly DispatcherTimer controlBarHideTimer;
    private readonly DispatcherTimer singleClickTimer;
    private readonly DispatcherTimer playlistHideTimer;

    private string currentFolderPath = "";
    private string currentFolderName = "";
    private string[] videoFiles = Array.Empty<string>();
    private string? pendingLoadFolderPath;
    private string? pendingLoadFolderName;
    private bool isProgressDragging = false;

    private float currentSpeed = 1.0f;
    private float speedBeforeHold = 1.0f;
    private readonly DispatcherTimer rightHoldTimer;
    private bool isRightHolding;

    // 提取的控制器
    private readonly ThumbnailGenerator _thumbnailGenerator = ThumbnailGenerator.Instance;
    private SpeedPopupController? _speedPopupController;
    private ThumbnailPreviewController? _thumbnailPreviewController;

    private Window? parentWindow;
    private bool isFullscreen = false;
    private FullscreenWindow? fullscreenWindow;
    private Grid? controlBarOriginalParent;
    private int controlBarOriginalIndex = -1;
    private Grid? playlistOriginalParent;
    private int playlistOriginalIndex = -1;

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

        controlBarHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        controlBarHideTimer.Tick += ControlBarHideTimer_Tick;

        singleClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        singleClickTimer.Tick += SingleClickTimer_Tick;

        playlistHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        playlistHideTimer.Tick += PlaylistHideTimer_Tick;

        rightHoldTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        rightHoldTimer.Tick += RightHoldTimer_Tick;

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
        UpdateButtonTooltips();

        // 倍速弹窗控制器
        _speedPopupController = new SpeedPopupController(
            SpeedPopup, SpeedBtn, SpeedPopupScale, SpeedOptionsPanel, PageRoot,
            rate => mediaController.Rate = rate);
        _speedPopupController.SpeedChanged += speed => currentSpeed = speed;

        // 进度条悬浮缩略图控制器
        _thumbnailPreviewController = new ThumbnailPreviewController(
            ProgressSlider, ProgressPopup, ProgressPopupScale,
            ThumbnailImage, ThumbnailTimeText,
            _thumbnailGenerator,
            () => mediaController.Length);

        _thumbnailGenerator.VideoReady += OnVideoThumbnailReady;
        _thumbnailGenerator.VideoProgress += OnVideoThumbnailProgress;
    }

    private void OnVideoThumbnailProgress(string videoPath, int percent)
    {
        Dispatcher.Invoke(() =>
        {
            foreach (var item in PlaylistBox.Items)
            {
                if (item is PlaylistItem pi &&
                    string.Equals(pi.FilePath, videoPath, StringComparison.OrdinalIgnoreCase))
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
            foreach (var item in PlaylistBox.Items)
            {
                if (item is PlaylistItem pi &&
                    string.Equals(pi.FilePath, videoPath, StringComparison.OrdinalIgnoreCase))
                {
                    pi.IsThumbnailReady = true;
                    Log($"[Thumbnail] 选集按钮显示对勾: {pi.Title}");
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
            Log($"获取父窗口: {parentWindow?.GetType().Name}");

            // 保存原始父容器和位置，全屏 reparent 后恢复用
            controlBarOriginalParent = ControlBar.Parent as Grid;
            if (controlBarOriginalParent != null)
                controlBarOriginalIndex = controlBarOriginalParent.Children.IndexOf(ControlBar);

            playlistOriginalParent = PlaylistBorder.Parent as Grid;
            if (playlistOriginalParent != null)
                playlistOriginalIndex = playlistOriginalParent.Children.IndexOf(PlaylistBorder);

            // 创建全屏窗口（只创建一次，复用）
            if (fullscreenWindow == null)
            {
                fullscreenWindow = new FullscreenWindow();
                fullscreenWindow.Setup(mediaController, inputHandler);
                fullscreenWindow.ExitRequested += (_, _) => ExitFullscreen();
                fullscreenWindow.ControlBarShowRequested += (_, _) => ShowFullscreenControlBar();
                fullscreenWindow.PlaylistShowRequested += (_, _) => ShowFullscreenPlaylist();
                fullscreenWindow.RightHoldStarted += (_, _) =>
                {
                    speedBeforeHold = currentSpeed;
                    isRightHolding = true;
                    mediaController.Rate = 3.0f;
                    UpdateSpeedButtonText(3.0f);
                };
                fullscreenWindow.RightHoldEnded += (_, _) =>
                {
                    isRightHolding = false;
                    mediaController.Rate = speedBeforeHold;
                    UpdateSpeedButtonText(speedBeforeHold);
                };
            }

            VideoContainer.MouseMove += VideoContainer_MouseMove;
            VideoContainer.MouseRightButtonDown += VideoContainer_MouseRightButtonDown;
            VideoContainer.MouseRightButtonUp += VideoContainer_MouseRightButtonUp;

            Keyboard.Focus(this);
            FocusManager.SetFocusedElement(this, this);
            Log($"Loaded 后设置焦点到 PlayerPage");

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

            mediaController.Playing += (s, ev) =>
            {
                Dispatcher.Invoke(() =>
                {
                    PlayPauseIcon.Source = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri("pack://application:,,,/Resources/Icons/pause.png"));
                    AnimatePauseBigOut();
                });
            };
            mediaController.Paused += (s, ev) =>
                Dispatcher.Invoke(() =>
                {
                    PlayPauseIcon.Source = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri("pack://application:,,,/Resources/Icons/play.png"));
                    AnimatePauseBigIn();
                });
            mediaController.Stopped += (s, ev) =>
                Dispatcher.Invoke(() =>
                {
                    PlayPauseIcon.Source = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri("pack://application:,,,/Resources/Icons/play.png"));
                    AnimatePauseBigOut();
                });
            mediaController.ProgressUpdated += (s, ev) =>
            {
                if (!isProgressDragging)
                {
                    Dispatcher.Invoke(() =>
                    {
                        ProgressSlider.Maximum = ev.TotalTime;
                        ProgressSlider.Value = ev.CurrentTime;
                        CurrentTimeText.Text = MediaPlayerController.FormatTime(ev.CurrentTime);
                        TotalTimeText.Text = MediaPlayerController.FormatTime(ev.TotalTime);
                    });
                }
            };

            saveProgressTimer.Start();

            if (pendingLoadFolderPath != null)
            {
                var path = pendingLoadFolderPath;
                var name = pendingLoadFolderName ?? "";
                pendingLoadFolderPath = null;
                pendingLoadFolderName = null;
                Log("Loaded 后执行暂存的 LoadFolder");
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
        Log($"LoadFolder 被调用: {folderPath}, IsLoaded={IsLoaded}");

        if (!IsLoaded)
        {
            pendingLoadFolderPath = folderPath;
            pendingLoadFolderName = folderName;
            Log("页面尚未 Loaded，暂存文件夹信息");
            return;
        }

        currentFolderPath = folderPath;
        currentFolderName = folderName;

        videoFiles = VideoScanner.GetVideoFiles(folderPath);
        Log($"扫描到 {videoFiles.Length} 个视频文件");
        foreach (var f in videoFiles)
        {
            Log($"  - {f}");
        }
        PlaylistBox.Items.Clear();
        for (int i = 0; i < videoFiles.Length; i++)
        {
            var filePath = videoFiles[i];
            PlaylistBox.Items.Add(new PlaylistItem
            {
                Number = i + 1,
                Title = Path.GetFileName(filePath),
                FilePath = filePath,
                IsPlayed = settingsService.IsVideoPlayed(filePath),
                IsThumbnailReady = _thumbnailGenerator.GetState(filePath) == ThumbnailState.Ready
            });
        }
        EpisodeCountText.Text = videoFiles.Length > 0 ? $"{videoFiles.Length} 集" : "";

        AnimateEpisodeButtonsEntrance();

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
                PlaylistBox.SelectedIndex = index;
            }
            else
            {
                PlayVideo(targetVideo);
            }
        }
    }

    private void PlaylistBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PlaylistBox.SelectedIndex >= 0 && PlaylistBox.SelectedIndex < videoFiles.Length)
        {
            SaveCurrentProgress();
            PlayVideo(videoFiles[PlaylistBox.SelectedIndex]);
        }
    }

    private void EpisodeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.DataContext is PlaylistItem item)
        {
            int index = item.Number - 1;
            if (index >= 0 && index < videoFiles.Length)
            {
                if (index == PlaylistBox.SelectedIndex) return;

                var oldItem = PlaylistBox.SelectedItem as PlaylistItem;
                if (oldItem != null)
                {
                    oldItem.IsPlayed = true;
                }

                PlaylistBox.SelectedIndex = index;
            }
        }
    }

    private void PlayVideo(string filePath)
    {
        Log($"[PlayVideo] 开始: {Path.GetFileName(filePath)}");
        Log($"[PlayVideo] 当前SelectedIndex={PlaylistBox.SelectedIndex}");

        _thumbnailPreviewController?.SetCurrentVideo(filePath);
        var thumbState = _thumbnailGenerator.GetState(filePath);
        Log($"[PlayVideo] 缩略图状态: {thumbState}, ffmpeg可用: {_thumbnailGenerator.IsFfmpegAvailable}");

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

    private void PlayPauseBtn_Click(object sender, RoutedEventArgs e)
    {
        Log("PlayPauseBtn 被点击");
        mediaController.TogglePlayPause();
    }

    private void PreviousBtn_Click(object sender, RoutedEventArgs e)
    {
        Log("PreviousBtn 被点击");
        PlayPrevious();
    }

    private void NextBtn_Click(object sender, RoutedEventArgs e)
    {
        Log("NextBtn 被点击");
        PlayNext();
    }

    private void FullscreenBtn_Click(object sender, RoutedEventArgs e)
    {
        Log("FullscreenBtn 被点击");
        ToggleFullscreen();
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        Log("StopBtn 被点击");
        mediaController.Stop();
    }

    private void PlaylistToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        PlaylistBorder.Visibility = PlaylistBorder.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        var window = new KeyBindingsWindow(inputHandler)
        {
            Owner = Window.GetWindow(this)
        };
        window.ShowDialog();
        UpdateButtonTooltips();
    }

    private string KeyToDisplayString(System.Windows.Input.Key key)
    {
        return key.ToString()
            .Replace("Left", "←").Replace("Right", "→")
            .Replace("Space", "空格").Replace("Escape", "Esc")
            .Replace("PageUp", "PgUp").Replace("PageDown", "PgDn")
            .Replace("Return", "Enter");
    }

    private void UpdateButtonTooltips()
    {
        var bindings = inputHandler.GetCurrentBindings();
        PlayPauseBtn.ToolTip = $"播放/暂停 ({KeyToDisplayString(bindings["TogglePlayPause"])})";
        PreviousBtn.ToolTip = $"上一集 ({KeyToDisplayString(bindings["PreviousEpisode"])})";
        NextBtn.ToolTip = $"下一集 ({KeyToDisplayString(bindings["NextEpisode"])})";
        FullscreenBtn.ToolTip = $"全屏 ({KeyToDisplayString(bindings["ToggleFullscreen"])})";
    }

    private void VideoContainer_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.XButton1)
        {
            if (isFullscreen)
            {
                ExitFullscreen();
            }
            else
            {
                SaveCurrentProgress();
                BackRequested?.Invoke(this, EventArgs.Empty);
            }
            e.Handled = true;
        }
    }

    private void ProgressSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        if (e.OriginalSource is not System.Windows.Controls.Primitives.Thumb && ProgressSlider.ActualWidth > 0)
        {
            double ratio;
            if (ProgressSlider.Template.FindName("PART_Track", ProgressSlider) is System.Windows.Controls.Primitives.Track track && track.ActualWidth > 0)
            {
                System.Windows.Point trackPos = e.GetPosition(track);
                ratio = Math.Max(0, Math.Min(1, trackPos.X / track.ActualWidth));
            }
            else
            {
                System.Windows.Point pos = e.GetPosition(ProgressSlider);
                ratio = pos.X / ProgressSlider.ActualWidth;
            }
            double newValue = ProgressSlider.Minimum + ratio * (ProgressSlider.Maximum - ProgressSlider.Minimum);
            ProgressSlider.Value = Math.Max(ProgressSlider.Minimum, Math.Min(ProgressSlider.Maximum, newValue));
        }

        isProgressDragging = true;
    }

    private void ProgressSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        isProgressDragging = false;
        mediaController.SeekTo((long)ProgressSlider.Value);
    }

    private void ProgressSlider_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        isProgressDragging = false;
        mediaController.SeekTo((long)ProgressSlider.Value);
    }

    private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        CurrentTimeText.Text = MediaPlayerController.FormatTime((long)ProgressSlider.Value);
    }

    // ========== 键盘事件处理 (统一入口) ==========

    private void ProcessKeyboardEvent(System.Windows.Input.KeyEventArgs e, string source)
    {
        Log($"KeyDown({source}): Key={e.Key}, OriginalSource={e.OriginalSource?.GetType().Name}, Handled={e.Handled}");
        if (inputHandler.HandleKeyDown(e, isFullscreen))
        {
            e.Handled = true;
        }
    }

    public void HandlePreviewKeyDown(System.Windows.Input.KeyEventArgs e) => ProcessKeyboardEvent(e, "PreviewKeyDown (MainWindow)");
    public void HandleKeyDown(System.Windows.Input.KeyEventArgs e) => ProcessKeyboardEvent(e, "KeyDown (MainWindow)");

    private void PlayerPage_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Handled) return;
        ProcessKeyboardEvent(e, "PP_PreviewKeyDown");
    }

    private void PlayerPage_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Handled) return;
        ProcessKeyboardEvent(e, "PP_KeyDown");
    }

    private void ControlBar_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        => ProcessKeyboardEvent(e, "CB_PreviewKeyDown");

    private void ControlBar_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Handled) return;
        ProcessKeyboardEvent(e, "CB_KeyDown");
    }

    private void VideoContainer_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.Handled) return;
        Log($"VideoContainer MouseLeftButtonDown (WPF): ClickCount={e.ClickCount}");
        Keyboard.Focus(this);

        if (e.ClickCount >= 2)
        {
            Log("VideoContainer 左键双击 -> ToggleFullscreen");
            singleClickTimer.Stop();
            ToggleFullscreen();
            e.Handled = true;
            return;
        }

        singleClickTimer.Stop();
        singleClickTimer.Start();
    }

    private void SingleClickTimer_Tick(object? sender, EventArgs e)
    {
        singleClickTimer.Stop();
        mediaController.TogglePlayPause();
    }

    private void PlayerPage_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        Log($"GotKeyboardFocus: NewFocus={e.NewFocus?.GetType().Name}, OldFocus={e.OldFocus?.GetType().Name}");
    }

    private void PlayerPage_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        Log($"LostKeyboardFocus: NewFocus={e.NewFocus?.GetType().Name}, OldFocus={e.OldFocus?.GetType().Name}");
    }

    private void PlayNext()
    {
        if (PlaylistBox.SelectedIndex < PlaylistBox.Items.Count - 1)
        {
            var oldItem = PlaylistBox.SelectedItem as PlaylistItem;
            if (oldItem != null)
            {
                oldItem.IsPlayed = true;
            }
            PlaylistBox.SelectedIndex++;
        }
    }

    private void PlayPrevious()
    {
        if (PlaylistBox.SelectedIndex > 0)
        {
            var oldItem = PlaylistBox.SelectedItem as PlaylistItem;
            if (oldItem != null)
            {
                oldItem.IsPlayed = true;
            }
            PlaylistBox.SelectedIndex--;
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

    // ========== 通用按钮动画（hover / press / release 统一由代码控制） ==========

    // iOS 默认曲线 (0.25, 0.1, 0.25, 1.0)
    private static readonly Helpers.CubicBezierEase _btnEase = new()
    {
        X1 = 0.25, Y1 = 0.1, X2 = 0.25, Y2 = 1.0,
        EasingMode = EasingMode.EaseIn
    };
    private static readonly TimeSpan _hoverEnterDuration = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan _hoverExitDuration = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan _pressDuration = TimeSpan.FromMilliseconds(130);
    private static readonly TimeSpan _releaseDuration = TimeSpan.FromMilliseconds(280);

    private void CommonButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        if (!btn.IsPressed)
            AnimateScale(btn, 1.2, _hoverEnterDuration, _btnEase);
    }

    private void CommonButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        if (!btn.IsPressed)
            AnimateScale(btn, 1.0, _hoverExitDuration, _btnEase);
    }

    private void CommonButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        AnimateScale(btn, 0.85, _pressDuration, _btnEase);
    }

    private void CommonButton_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        double target = btn.IsMouseOver ? 1.2 : 1.0;
        AnimateScale(btn, target, _releaseDuration, _btnEase);
    }

    private static void AnimateScale(System.Windows.Controls.Button btn, double target,
        TimeSpan duration, IEasingFunction ease)
    {
        if (btn.Template.FindName("AnimScale", btn) is ScaleTransform st)
        {
            AnimationHelper.AnimateScaleTransform(st, target, (int)duration.TotalMilliseconds, ease);
        }
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

    // ========== 倍速 ==========

    // ========== 倍速弹窗 (转发到 SpeedPopupController) ==========

    private void SpeedBtn_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        => _speedPopupController?.OnSpeedBtnMouseEnter();

    private void SpeedBtn_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        => _speedPopupController?.OnSpeedBtnMouseLeave();

    private void SpeedPopup_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        => _speedPopupController?.OnSpeedPopupMouseEnter();

    private void SpeedPopup_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        => _speedPopupController?.OnSpeedPopupMouseLeave();

    private void SpeedOption_Click(object sender, RoutedEventArgs e)
        => _speedPopupController?.OnSpeedOptionClick(sender);

    private void PageRoot_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => _speedPopupController?.OnPageRootPreviewMouseLeftButtonDown(e);

    private void SetSpeed(float speed)
        => _speedPopupController?.SetSpeed(speed);

    private void UpdateSpeedButtonText(float speed)
        => _speedPopupController?.UpdateButtonText(speed);

    // ========== 右键长按三倍速 ==========

    private void RightHoldTimer_Tick(object? sender, EventArgs e)
    {
        rightHoldTimer.Stop();
        speedBeforeHold = currentSpeed;
        isRightHolding = true;
        mediaController.Rate = 3.0f;
        UpdateSpeedButtonText(3.0f);
    }

    private void VideoContainer_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        rightHoldTimer.Stop();
        rightHoldTimer.Start();
        e.Handled = true;
    }

    private void VideoContainer_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        rightHoldTimer.Stop();
        if (isRightHolding)
        {
            isRightHolding = false;
            mediaController.Rate = speedBeforeHold;
            UpdateSpeedButtonText(speedBeforeHold);
        }
        e.Handled = true;
    }

    // ========== 进度条悬浮预览 (转发到 ThumbnailPreviewController) ==========

    private void ProgressSlider_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        => _thumbnailPreviewController?.OnSliderMouseEnter();

    private void ProgressSlider_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        => _thumbnailPreviewController?.OnSliderMouseLeave();

    private void ProgressSlider_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        => _thumbnailPreviewController?.OnSliderMouseMove(e);

    private void ProgressPopup_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        => _thumbnailPreviewController?.OnPopupMouseEnter();

    private void ProgressPopup_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        => _thumbnailPreviewController?.OnPopupMouseLeave();

    public void Dispose()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Log("Dispose 开始");
        saveProgressTimer.Stop();
        controlBarHideTimer.Stop();
        singleClickTimer.Stop();
        playlistHideTimer.Stop();
        rightHoldTimer.Stop();
        Log($"saveProgressTimer.Stop 耗时 {sw.ElapsedMilliseconds}ms");
        SaveCurrentProgress();
        Log($"SaveCurrentProgress 完成，耗时 {sw.ElapsedMilliseconds}ms");

        _speedPopupController?.Dispose();
        _thumbnailPreviewController?.Dispose();

        fullscreenWindow?.Close();
        fullscreenWindow = null;

        mediaController.Dispose();
        Log($"mediaController.Dispose 完成，总耗时 {sw.ElapsedMilliseconds}ms");
    }
}
