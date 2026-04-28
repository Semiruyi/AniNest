using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
    private readonly DispatcherTimer rightHoldTimer;
    private bool isRightHolding;

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
                });
            };
            mediaController.Paused += (s, ev) =>
                Dispatcher.Invoke(() =>
                {
                    PlayPauseIcon.Source = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri("pack://application:,,,/Resources/Icons/play.png"));
                });
            mediaController.Stopped += (s, ev) =>
                Dispatcher.Invoke(() =>
                {
                    PlayPauseIcon.Source = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri("pack://application:,,,/Resources/Icons/play.png"));
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
                IsPlayed = settingsService.IsVideoPlayed(filePath)
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
    }

    private void ProgressSlider_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        isProgressDragging = false;
    }

    private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (isProgressDragging)
        {
            mediaController.SeekTo((long)e.NewValue);
        }
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

    // ========== 倍速 ==========

    private void SpeedPopupCloseTimer_Tick(object? sender, EventArgs e)
    {
        speedPopupCloseTimer.Stop();

        // 鼠标仍在按钮或弹窗上方，不关闭
        if (IsMouseOverSafeZone())
        {
            speedPopupCloseTimer.Start();
            return;
        }

        SpeedPopup.IsOpen = false;
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
            SpeedPopup.IsOpen = false;
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
        SpeedPopup.IsOpen = true;
        _ = Dispatcher.BeginInvoke(new Action(() => HighlightSpeedOption(currentSpeed)), DispatcherPriority.Loaded);
    }

    private void SpeedBtn_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        speedPopupCloseTimer.Stop();
        speedPopupCloseTimer.Start();
    }

    private void SpeedPopup_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        speedPopupCloseTimer.Stop();
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
        Log($"saveProgressTimer.Stop 耗时 {sw.ElapsedMilliseconds}ms");
        SaveCurrentProgress();
        Log($"SaveCurrentProgress 完成，耗时 {sw.ElapsedMilliseconds}ms");

        fullscreenWindow?.Close();
        fullscreenWindow = null;

        mediaController.Dispose();
        Log($"mediaController.Dispose 完成，总耗时 {sw.ElapsedMilliseconds}ms");
    }
}
