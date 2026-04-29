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
using LocalPlayer.Models;
using LocalPlayer.Services;

namespace LocalPlayer.Views;

public partial class PlayerPage : System.Windows.Controls.UserControl, IDisposable
{
    private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "player.log");

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [PlayerPage] {message}{Environment.NewLine}");
        }
        catch { }
    }

    private readonly MediaPlayerController mediaController = new();
    private readonly SettingsService settingsService = new();
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
    private readonly DispatcherTimer speedPopupCloseTimer;
    private bool isSpeedPopupClosing;
    private readonly DispatcherTimer rightHoldTimer;
    private bool isRightHolding;

    // 进度条悬浮缩略图预览
    private readonly ThumbnailGenerator _thumbnailGenerator = ThumbnailGenerator.Instance;
    private readonly Dictionary<int, BitmapSource> _thumbnailCache = new(); // second → image, 最多20张
    private readonly DispatcherTimer progressPopupShowTimer = new();
    private readonly DispatcherTimer progressPopupHideTimer = new();
    private bool _isProgressHovering;
    private bool _isProgressPopupVisible;
    private bool _isProgressPopupClosing;
    private int _lastRequestedSecond = -1;
    private string? _currentThumbVideoPath; // 当前视频的缩略图目录就绪路径

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
            Log($"PlayerPage 构造函数异常: {ex.GetType().Name}: {ex.Message}");
            Log($"StackTrace: {ex.StackTrace}");
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

        speedPopupCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        speedPopupCloseTimer.Tick += SpeedPopupCloseTimer_Tick;

        rightHoldTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        rightHoldTimer.Tick += RightHoldTimer_Tick;

        progressPopupShowTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        progressPopupShowTimer.Tick += ProgressPopupShowTimer_Tick;
        progressPopupHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        progressPopupHideTimer.Tick += ProgressPopupHideTimer_Tick;

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

        SpeedPopup.CustomPopupPlacementCallback = (_, targetSize, _2) =>
        {
            // WPF passes targetSize in device pixels (not DIPs). Scale our DIP-based
            // popup dimensions by the same factor so the math is consistent.
            double scale = targetSize.Width / SpeedBtn.ActualWidth;
            double pw = 90 * scale;
            double ph = 274 * scale;
            double x = (targetSize.Width - pw) / 2;
            double y = -ph - 10 * scale;
            Log(string.Format("[PlacementCallback] scale={0:F3}, targetSize={1:F1}x{2:F1}, pw={3:F1}, ph={4:F1}, x={5:F1}, y={6:F1}",
                scale, targetSize.Width, targetSize.Height, pw, ph, x, y));
            return new[] { new System.Windows.Controls.Primitives.CustomPopupPlacement(
                new System.Windows.Point(x, y),
                System.Windows.Controls.Primitives.PopupPrimaryAxis.Vertical) };
        };
        SpeedPopup.PlacementTarget = SpeedBtn;
        ProgressPopup.PlacementTarget = ProgressSlider;

        _thumbnailGenerator.VideoReady += OnVideoThumbnailReady;
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
            Log($"PlayerPage_Loaded 异常: {ex.GetType().Name}: {ex.Message}");
            Log($"StackTrace: {ex.StackTrace}");
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

        _thumbnailCache.Clear();
        _lastRequestedSecond = -1;
        _currentThumbVideoPath = filePath;
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

    public void HandlePreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        Log($"HandlePreviewKeyDown 被调用: Key={e.Key}, Source={e.Source?.GetType().Name}, OriginalSource={e.OriginalSource?.GetType().Name}");

        if (inputHandler.HandleKeyDown(e, isFullscreen))
        {
            e.Handled = true;
            Log($"按键已处理并标记 Handled: {e.Key}");
        }
        else
        {
            Log($"按键未被处理: {e.Key}");
        }
    }

    public void HandleKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        Log($"HandleKeyDown (冒泡) 被调用: Key={e.Key}, Source={e.Source?.GetType().Name}, OriginalSource={e.OriginalSource?.GetType().Name}");

        if (inputHandler.HandleKeyDown(e, isFullscreen))
        {
            e.Handled = true;
            Log($"冒泡按键已处理并标记 Handled: {e.Key}");
        }
    }

    private void PlayerPage_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Handled) return;
        Log($"PlayerPage_PreviewKeyDown: Key={e.Key}, OriginalSource={e.OriginalSource?.GetType().Name}, Handled={e.Handled}");
        if (inputHandler.HandleKeyDown(e, isFullscreen))
        {
            e.Handled = true;
        }
    }

    private void PlayerPage_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Handled) return;
        Log($"PlayerPage_KeyDown: Key={e.Key}, OriginalSource={e.OriginalSource?.GetType().Name}, Handled={e.Handled}");
        if (inputHandler.HandleKeyDown(e, isFullscreen))
        {
            e.Handled = true;
        }
    }

    private void ControlBar_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        Log($"ControlBar_PreviewKeyDown: Key={e.Key}, OriginalSource={e.OriginalSource?.GetType().Name}, Handled={e.Handled}");
        if (inputHandler.HandleKeyDown(e, isFullscreen))
        {
            e.Handled = true;
        }
    }

    private void ControlBar_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Handled) return;
        Log($"ControlBar_KeyDown: Key={e.Key}, OriginalSource={e.OriginalSource?.GetType().Name}, Handled={e.Handled}");
        if (inputHandler.HandleKeyDown(e, isFullscreen))
        {
            e.Handled = true;
        }
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
            st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            var ax = new DoubleAnimation(target, duration) { EasingFunction = ease };
            var ay = new DoubleAnimation(target, duration) { EasingFunction = ease };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, ax);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, ay);
        }
    }

    // ========== 暂停大图标动画 ==========

    private void AnimatePauseBigIn()
    {
        PauseBigIconScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        PauseBigIconScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        PauseBigIcon.BeginAnimation(OpacityProperty, null);

        PauseBigIconScale.ScaleX = 0;
        PauseBigIconScale.ScaleY = 0;
        PauseBigIcon.Opacity = 0;

        var duration = TimeSpan.FromMilliseconds(250);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        PauseBigIconScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        PauseBigIconScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        PauseBigIcon.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
    }

    private void AnimatePauseBigOut()
    {
        double fromScaleX = PauseBigIconScale.ScaleX;
        double fromScaleY = PauseBigIconScale.ScaleY;
        double fromOpacity = PauseBigIcon.Opacity;

        PauseBigIconScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        PauseBigIconScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        PauseBigIcon.BeginAnimation(OpacityProperty, null);

        PauseBigIconScale.ScaleX = fromScaleX;
        PauseBigIconScale.ScaleY = fromScaleY;
        PauseBigIcon.Opacity = fromOpacity;

        var duration = TimeSpan.FromMilliseconds(180);
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        PauseBigIconScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(fromScaleX, 0, duration) { EasingFunction = ease });
        PauseBigIconScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(fromScaleY, 0, duration) { EasingFunction = ease });
        PauseBigIcon.BeginAnimation(OpacityProperty,
            new DoubleAnimation(fromOpacity, 0, duration) { EasingFunction = ease });
    }

    // ========== 倍速 ==========

    private void AnimateSpeedPopupIn()
    {
        isSpeedPopupClosing = false;
        var border = SpeedPopup.Child as Border;
        if (border == null) return;

        SpeedPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        SpeedPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        border.BeginAnimation(UIElement.OpacityProperty, null);

        SpeedPopupScale.ScaleX = 0;
        SpeedPopupScale.ScaleY = 0;
        border.Opacity = 0;

        var duration = TimeSpan.FromMilliseconds(250);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        SpeedPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        SpeedPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        border.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
    }

    private void AnimateSpeedPopupOut()
    {
        if (isSpeedPopupClosing) return;
        isSpeedPopupClosing = true;

        var border = SpeedPopup.Child as Border;
        if (border == null)
        {
            SpeedPopup.IsOpen = false;
            isSpeedPopupClosing = false;
            return;
        }

        // 先快照当前值，再清动画（清完动画值会回落，所以要先读）
        double fromScaleX = SpeedPopupScale.ScaleX;
        double fromScaleY = SpeedPopupScale.ScaleY;
        double fromOpacity = border.Opacity;

        SpeedPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        SpeedPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        border.BeginAnimation(UIElement.OpacityProperty, null);

        SpeedPopupScale.ScaleX = fromScaleX;
        SpeedPopupScale.ScaleY = fromScaleY;
        border.Opacity = fromOpacity;

        var duration = TimeSpan.FromMilliseconds(180);
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var closeX = new DoubleAnimation(fromScaleX, 0, duration) { EasingFunction = ease };
        var closeY = new DoubleAnimation(fromScaleY, 0, duration) { EasingFunction = ease };
        var closeOpacity = new DoubleAnimation(fromOpacity, 0, duration) { EasingFunction = ease };

        closeX.Completed += (_, _) =>
        {
            SpeedPopup.IsOpen = false;
            isSpeedPopupClosing = false;
        };

        SpeedPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, closeX);
        SpeedPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, closeY);
        border.BeginAnimation(UIElement.OpacityProperty, closeOpacity);
    }

    private void SpeedPopupCloseTimer_Tick(object? sender, EventArgs e)
    {
        speedPopupCloseTimer.Stop();

        // 鼠标仍在按钮或弹窗上方，不关闭
        if (IsMouseOverSafeZone())
        {
            speedPopupCloseTimer.Start();
            return;
        }

        AnimateSpeedPopupOut();
    }

    private bool IsMouseOverSafeZone()
    {
        // 检查是否在倍速按钮上
        var btnPt = System.Windows.Input.Mouse.GetPosition(SpeedBtn);
        if (btnPt.X >= -2 && btnPt.Y >= -2 &&
            btnPt.X <= SpeedBtn.ActualWidth + 2 && btnPt.Y <= SpeedBtn.ActualHeight + 2)
            return true;

        // 检查是否在弹窗内容上
        if (SpeedPopup.IsOpen && SpeedPopup.Child != null)
        {
            try
            {
                var popupPt = System.Windows.Input.Mouse.GetPosition(SpeedPopup.Child);
                if (popupPt.X >= -2 && popupPt.Y >= -2 &&
                    popupPt.X <= SpeedPopup.Child.RenderSize.Width + 2 &&
                    popupPt.Y <= SpeedPopup.Child.RenderSize.Height + 2)
                    return true;
            }
            catch { }
        }

        return false;
    }

    private void PageRoot_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!SpeedPopup.IsOpen || SpeedPopup.Child == null) return;

        // 检查点击是否来自弹窗内部，是则不关闭
        if (e.OriginalSource is DependencyObject dep && IsDescendantOf(dep, SpeedPopup.Child))
            return;

        // 检查点击是否在倍速按钮上
        var pos = e.GetPosition(this);
        var btnBounds = SpeedBtn.TransformToAncestor(this).TransformBounds(
            new Rect(0, 0, SpeedBtn.ActualWidth, SpeedBtn.ActualHeight));
        if (!btnBounds.Contains(pos))
        {
            AnimateSpeedPopupOut();
        }
    }

    private static bool IsDescendantOf(DependencyObject? dep, DependencyObject ancestor)
    {
        while (dep != null)
        {
            if (dep == ancestor)
                return true;
            dep = VisualTreeHelper.GetParent(dep);
        }
        return false;
    }

    private void SpeedBtn_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        speedPopupCloseTimer.Stop();

        if (isSpeedPopupClosing)
        {
            SpeedPopup.IsOpen = true;
            AnimateSpeedPopupIn();
            return;
        }

        bool wasClosed = !SpeedPopup.IsOpen;
        SpeedPopup.IsOpen = true;
        if (wasClosed)
        {
            Dispatcher.BeginInvoke(() =>
            {
                HighlightSpeedOption(currentSpeed);
                AnimateSpeedPopupIn();
            }, DispatcherPriority.Loaded);
        }
    }

    private void SpeedBtn_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        speedPopupCloseTimer.Stop();
        speedPopupCloseTimer.Start();
    }

    private void SpeedPopup_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        speedPopupCloseTimer.Stop();
        if (isSpeedPopupClosing)
        {
            SpeedPopup.IsOpen = true;
            AnimateSpeedPopupIn();
        }
    }

    private void SpeedPopup_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        speedPopupCloseTimer.Stop();
        speedPopupCloseTimer.Start();
    }

    private void SpeedOption_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string tagStr &&
            float.TryParse(tagStr, out float speed))
        {
            SetSpeed(speed);
        }
    }

    private void SetSpeed(float speed)
    {
        currentSpeed = speed;
        mediaController.Rate = speed;
        SpeedBtn.Content = $"{speed:0.##}x";
        if (SpeedPopup.IsOpen)
            HighlightSpeedOption(speed);
    }

    private void UpdateSpeedButtonText(float speed)
    {
        SpeedBtn.Content = $"{speed:0.##}x";
    }

    private void HighlightSpeedOption(float speed)
    {
        var selectedColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#007AFF");
        var duration = TimeSpan.FromMilliseconds(300);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        foreach (var child in SpeedOptionsPanel.Children)
        {
            if (child is not System.Windows.Controls.Button btn) continue;

            bool isSelected = btn.Tag?.ToString() == speed.ToString("0.##");
            var targetColor = isSelected ? selectedColor : Colors.Transparent;
            var targetSize = isSelected ? 14.0 : 13.0;

            // 找到模板内的命名画刷，动画其颜色
            if (btn.Template.FindName("BgBrush", btn) is SolidColorBrush brush)
            {
                brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                var colorAnim = new ColorAnimation(targetColor, duration) { EasingFunction = ease };
                brush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
            }

            btn.BeginAnimation(System.Windows.Controls.Button.FontSizeProperty, null);
            var sizeAnim = new DoubleAnimation(targetSize, duration) { EasingFunction = ease };
            btn.BeginAnimation(System.Windows.Controls.Button.FontSizeProperty, sizeAnim);

            btn.FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

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

    // ========== 进度条悬浮预览 ==========

    private void ProgressSlider_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isProgressHovering = true;
        progressPopupHideTimer.Stop();
        if (!_isProgressPopupVisible)
            progressPopupShowTimer.Start();
    }

    private void ProgressSlider_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isProgressHovering = false;
        progressPopupShowTimer.Stop();
        progressPopupHideTimer.Start();
    }

    private void ProgressSlider_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (mediaController.Length <= 0) return;

        var pos = e.GetPosition(ProgressSlider);
        double ratio = Math.Max(0, Math.Min(1, pos.X / ProgressSlider.ActualWidth));
        long hoverTimeMs = (long)(ratio * mediaController.Length);
        int hoverSecond = (int)(hoverTimeMs / 1000);

        // 定位 + 时间
        ThumbnailTimeText.Text = MediaPlayerController.FormatTime(hoverTimeMs);
        double popupW = 160;
        double offsetX = Math.Max(0, Math.Min(pos.X - popupW / 2, ProgressSlider.ActualWidth - popupW));
        ProgressPopup.HorizontalOffset = offsetX;
        ProgressPopup.VerticalOffset = -90 - 30; // 固定位于控制栏上方
        if (_isProgressPopupVisible && ProgressPopup.IsOpen)
        {
            ProgressPopup.HorizontalOffset = offsetX + 1; // 强制刷新
            ProgressPopup.HorizontalOffset = offsetX;
        }

        // 缩略图可用性检测
        bool thumbReady = _currentThumbVideoPath != null &&
            _thumbnailGenerator.GetState(_currentThumbVideoPath) == ThumbnailState.Ready;
        ThumbnailImage.Visibility = thumbReady ? Visibility.Visible : Visibility.Collapsed;

        // 秒数未变则不重复加载
        if (hoverSecond == _lastRequestedSecond) return;
        _lastRequestedSecond = hoverSecond;

        if (thumbReady && _currentThumbVideoPath != null)
        {
            if (_thumbnailCache.TryGetValue(hoverSecond, out var cached))
            {
                ThumbnailImage.Source = cached;
            }
            else
            {
                var bmp = LoadThumbnailJpeg(_currentThumbVideoPath, hoverSecond);
                if (bmp != null)
                {
                    _thumbnailCache[hoverSecond] = bmp;
                    ThumbnailImage.Source = bmp;

                    if (_thumbnailCache.Count > 20)
                    {
                        var toRemove = _thumbnailCache.Keys.OrderBy(k => k).Take(_thumbnailCache.Count / 2).ToList();
                        foreach (var k in toRemove) _thumbnailCache.Remove(k);
                    }
                }
            }
        }
    }

    private void ProgressPopup_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        progressPopupHideTimer.Stop();
    }

    private void ProgressPopup_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        progressPopupHideTimer.Stop();
        progressPopupHideTimer.Start();
    }

    private void ProgressPopupShowTimer_Tick(object? sender, EventArgs e)
    {
        progressPopupShowTimer.Stop();
        if (!_isProgressHovering) return;
        ShowProgressPopup();
    }

    private void ProgressPopupHideTimer_Tick(object? sender, EventArgs e)
    {
        progressPopupHideTimer.Stop();
        if (_isProgressHovering) return;
        if (ProgressPopup.IsOpen && ProgressPopup.Child != null)
        {
            try
            {
                var pt = System.Windows.Input.Mouse.GetPosition(ProgressPopup.Child);
                if (pt.X >= -2 && pt.Y >= -2 &&
                    pt.X <= ProgressPopup.Child.RenderSize.Width + 2 &&
                    pt.Y <= ProgressPopup.Child.RenderSize.Height + 2)
                {
                    progressPopupHideTimer.Start();
                    return;
                }
            }
            catch { }
        }
        HideProgressPopup();
    }

    private void ShowProgressPopup()
    {
        if (_isProgressPopupVisible || _isProgressPopupClosing) return;
        _isProgressPopupVisible = true;
        var border = ProgressPopup.Child as Border;
        if (border == null) return;

        ProgressPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        ProgressPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        border.BeginAnimation(UIElement.OpacityProperty, null);
        ProgressPopupScale.ScaleX = 0.9;
        ProgressPopupScale.ScaleY = 0.9;
        border.Opacity = 0;
        ProgressPopup.IsOpen = true;

        var d = TimeSpan.FromMilliseconds(200);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        ProgressPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.9, 1, d) { EasingFunction = ease });
        ProgressPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.9, 1, d) { EasingFunction = ease });
        border.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, d) { EasingFunction = ease });
    }

    private void HideProgressPopup()
    {
        if (!_isProgressPopupVisible || _isProgressPopupClosing) return;
        _isProgressPopupClosing = true;
        var border = ProgressPopup.Child as Border;
        if (border == null) { ProgressPopup.IsOpen = false; _isProgressPopupVisible = false; _isProgressPopupClosing = false; return; }

        double sx = ProgressPopupScale.ScaleX, sy = ProgressPopupScale.ScaleY, op = border.Opacity;
        ProgressPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        ProgressPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        border.BeginAnimation(UIElement.OpacityProperty, null);
        ProgressPopupScale.ScaleX = sx; ProgressPopupScale.ScaleY = sy; border.Opacity = op;

        var d = TimeSpan.FromMilliseconds(150);
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var cx = new DoubleAnimation(sx, 0.9, d) { EasingFunction = ease };
        var cy = new DoubleAnimation(sy, 0.9, d) { EasingFunction = ease };
        var co = new DoubleAnimation(op, 0, d) { EasingFunction = ease };
        co.Completed += (_, _) => { ProgressPopup.IsOpen = false; _isProgressPopupVisible = false; _isProgressPopupClosing = false; };
        ProgressPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, cx);
        ProgressPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, cy);
        border.BeginAnimation(UIElement.OpacityProperty, co);
    }

    // ========== JPEG 加载 ==========

    private BitmapSource? LoadThumbnailJpeg(string videoPath, int second)
    {
        var path = _thumbnailGenerator.GetThumbnailPath(videoPath, second);
        if (path == null) return null;
        try
        {
            var decoder = new JpegBitmapDecoder(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            frame.Freeze();
            return frame;
        }
        catch (Exception ex)
        {
            Log($"[Thumbnail] 解码异常: second={second}, {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Log("Dispose 开始");
        saveProgressTimer.Stop();
        controlBarHideTimer.Stop();
        singleClickTimer.Stop();
        playlistHideTimer.Stop();
        speedPopupCloseTimer.Stop();
        rightHoldTimer.Stop();
        progressPopupShowTimer.Stop();
        progressPopupHideTimer.Stop();
        Log($"saveProgressTimer.Stop 耗时 {sw.ElapsedMilliseconds}ms");
        SaveCurrentProgress();
        Log($"SaveCurrentProgress 完成，耗时 {sw.ElapsedMilliseconds}ms");

        fullscreenWindow?.Close();
        fullscreenWindow = null;

        _thumbnailCache.Clear();
        mediaController.Dispose();
        Log($"mediaController.Dispose 完成，总耗时 {sw.ElapsedMilliseconds}ms");
    }
}
