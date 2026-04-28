using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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

    private string currentFolderPath = "";
    private string currentFolderName = "";
    private string[] videoFiles = Array.Empty<string>();
    private string? pendingLoadFolderPath;
    private string? pendingLoadFolderName;
    private bool isProgressDragging = false;

    private Window? parentWindow;
    private WindowState savedWindowState;
    private WindowStyle savedWindowStyle;
    private ResizeMode savedResizeMode;
    private bool isFullscreen = false;

    private Grid? controlBarOriginalParent;
    private int controlBarOriginalIndex = -1;

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

        // 配置输入处理器
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

        controlBarOriginalParent = ControlBar.Parent as Grid;
        if (controlBarOriginalParent != null)
            controlBarOriginalIndex = controlBarOriginalParent.Children.IndexOf(ControlBar);

        // 夺取焦点到 WPF 元素
        Log($"Loaded 前 FocusedElement={FocusManager.GetFocusedElement(this)}");
        Keyboard.Focus(this);
        FocusManager.SetFocusedElement(this, this);
        Log($"Loaded 后设置焦点到 PlayerPage");

        // 入场动画
        var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut };
        var duration = TimeSpan.FromMilliseconds(300);

        var anim = new System.Windows.Media.Animation.DoubleAnimation(0, 1, duration) { EasingFunction = ease };
        anim.Completed += (_, _) =>
        {
            PageRoot.BeginAnimation(OpacityProperty, null);
            PageRoot.Opacity = 1;
        };
        PageRoot.BeginAnimation(OpacityProperty, anim);

        mediaController.Initialize();
        VideoImage.Source = mediaController.VideoBitmap;

        OverlayGrid.MouseMove += OverlayGrid_MouseMove;

        VideoContainer.MouseLeftButtonDown += VideoContainer_MouseLeftButtonDown;
        VideoContainer.MouseRightButtonDown += VideoContainer_MouseRightButtonDown;

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

    private class PlaylistItem : System.ComponentModel.INotifyPropertyChanged
    {
        public int Number { get; set; }
        public string Title { get; set; } = "";
        public string FilePath { get; set; } = "";

        private bool _isPlayed;
        public bool IsPlayed
        {
            get => _isPlayed;
            set
            {
                if (_isPlayed != value)
                {
                    _isPlayed = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsPlayed)));
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
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

    private void OverlayGrid_MouseDown(object sender, MouseButtonEventArgs e)
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

        // 点击轨道（非 Thumb）时，直接跳转到对应位置
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

    private void ToggleFullscreen()
    {
        Log($"ToggleFullscreen 被调用，当前状态 isFullscreen={isFullscreen}");
        if (isFullscreen)
            ExitFullscreen();
        else
            EnterFullscreen();
    }

    private void EnterFullscreen()
    {
        if (parentWindow == null)
        {
            Log("EnterFullscreen 失败: parentWindow 为 null");
            return;
        }

        Log("进入全屏模式");
        savedWindowState = parentWindow.WindowState;
        savedWindowStyle = parentWindow.WindowStyle;
        savedResizeMode = parentWindow.ResizeMode;

        parentWindow.WindowStyle = WindowStyle.None;
        parentWindow.ResizeMode = ResizeMode.NoResize;
        parentWindow.WindowState = WindowState.Maximized;

        PlaylistBorder.Visibility = Visibility.Collapsed;

        // 将控制栏移到视频覆盖层内，才能在视频上方显示
        if (controlBarOriginalParent != null)
            controlBarOriginalParent.Children.Remove(ControlBar);
        OverlayGrid.Children.Add(ControlBar);

        ControlBar.VerticalAlignment = VerticalAlignment.Bottom;
        ControlBar.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
        ControlBar.Height = 63;

        HideFullscreenControlBar();

        OverlayGrid.MouseMove -= OverlayGrid_MouseMove;
        OverlayGrid.MouseMove += OverlayGrid_MouseMove;

        FullscreenIcon.Source = new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/Resources/Icons/exitFullScreen.png"));

        isFullscreen = true;

        Keyboard.Focus(ControlBar);
        Log("✓ 已进入全屏");
    }

    private void ExitFullscreen()
    {
        if (!isFullscreen || parentWindow == null)
        {
            Log($"ExitFullscreen 提前返回: isFullscreen={isFullscreen}, parentWindow={parentWindow}");
            return;
        }

        Log("退出全屏模式");
        parentWindow.WindowState = savedWindowState;
        parentWindow.WindowStyle = savedWindowStyle;
        parentWindow.ResizeMode = savedResizeMode;

        PlaylistBorder.Visibility = Visibility.Visible;

        // 从覆盖层移除并恢复到原来的父容器
        OverlayGrid.Children.Remove(ControlBar);
        if (controlBarOriginalParent != null)
        {
            if (controlBarOriginalIndex >= 0 && controlBarOriginalIndex <= controlBarOriginalParent.Children.Count)
                controlBarOriginalParent.Children.Insert(controlBarOriginalIndex, ControlBar);
            else
                controlBarOriginalParent.Children.Add(ControlBar);
        }

        // 恢复布局属性
        Grid.SetRow(ControlBar, 1);
        Grid.SetRowSpan(ControlBar, 1);
        ControlBar.VerticalAlignment = VerticalAlignment.Stretch;
        System.Windows.Controls.Panel.SetZIndex(ControlBar, 0);

        ControlBar.Visibility = Visibility.Visible;
        ControlBar.Opacity = 1;
        ControlBar.IsHitTestVisible = true;

        controlBarHideTimer.Stop();
        OverlayGrid.MouseMove -= OverlayGrid_MouseMove;

        FullscreenIcon.Source = new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/Resources/Icons/fullScreen.png"));

        isFullscreen = false;
        Log("✓ 已退出全屏");
    }

    private void VideoContainer_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.Handled) return;
        Log($"VideoContainer MouseLeftButtonDown (WPF): ClickCount={e.ClickCount}");
        if (e.ClickCount >= 2)
        {
            Log("VideoContainer 左键双击 -> TogglePlayPause");
            mediaController.TogglePlayPause();
            e.Handled = true;
        }
    }

    private void VideoContainer_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.Handled) return;

        var now = DateTime.Now;
        var position = e.GetPosition(this);
        Log($"VideoContainer MouseRightButtonDown (WPF): ClickCount={e.ClickCount}, Time={now:HH:mm:ss.fff}");

        if (e.ClickCount >= 2)
        {
            Log("VideoContainer 右键双击 -> ToggleFullscreen");
            ToggleFullscreen();
            e.Handled = true;
            return;
        }

        // 备选：如果 ClickCount 不可靠，用时间差检测（要求间隔 >50ms 且 <500ms，避免同一毫秒的重复事件误判）
        var elapsed = now - lastRightClickTime;
        var dx = position.X - lastRightClickPosition.X;
        var dy = position.Y - lastRightClickPosition.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (elapsed.TotalMilliseconds > 50 && elapsed.TotalMilliseconds < 500 && distance < 10)
        {
            Log("VideoContainer 右键双击(备选检测) -> ToggleFullscreen");
            ToggleFullscreen();
            e.Handled = true;
            return;
        }

        lastRightClickTime = now;
        lastRightClickPosition = position;
    }

    private DateTime lastRightClickTime = DateTime.MinValue;
    private System.Windows.Point lastRightClickPosition;

    private void OverlayGrid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.Handled) return;
        Log($"OverlayGrid MouseLeftButtonDown: ClickCount={e.ClickCount}");

        // 强制将焦点拉回 PlayerPage，防止 ForegroundWindow 吃掉后续键盘事件
        Keyboard.Focus(this);

        if (e.ClickCount >= 2)
        {
            Log("OverlayGrid 左键双击 -> TogglePlayPause");
            mediaController.TogglePlayPause();
            e.Handled = true;
        }
    }

    private void OverlayGrid_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.Handled) return;
        Log($"OverlayGrid MouseRightButtonDown: ClickCount={e.ClickCount}");

        // 强制将焦点拉回 PlayerPage，防止 ForegroundWindow 吃掉后续键盘事件
        Keyboard.Focus(this);

        if (e.ClickCount >= 2)
        {
            Log("OverlayGrid 右键双击 -> ToggleFullscreen");
            ToggleFullscreen();
            e.Handled = true;
        }
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

    public async System.Threading.Tasks.Task FadeOutUIAsync(int durationMs = 250)
    {
        var duration = TimeSpan.FromMilliseconds(durationMs);
        var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut };

        var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
        var anim = new System.Windows.Media.Animation.DoubleAnimation(1, 0, duration) { EasingFunction = ease };
        anim.Completed += (_, _) => tcs.TrySetResult(true);

        PageRoot.BeginAnimation(OpacityProperty, anim);

        await tcs.Task;
    }

    #region 选集按钮入场动画

    private static List<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        var result = new List<T>();
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t)
                result.Add(t);
            result.AddRange(FindVisualChildren<T>(child));
        }
        return result;
    }

    private async void AnimateEpisodeButtonsEntrance()
    {
        await System.Threading.Tasks.Task.Delay(100);

        if (!IsLoaded) return;

        var buttons = FindVisualChildren<System.Windows.Controls.Button>(PlaylistBox);
        for (int i = 0; i < buttons.Count; i++)
        {
            var btn = buttons[i];
            var delay = TimeSpan.FromMilliseconds(i * 35);

            btn.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            var st = new ScaleTransform(0.88, 0.88);
            btn.RenderTransform = st;
            btn.Opacity = 0;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var dur = TimeSpan.FromMilliseconds(420);

            var scaleAnimX = new DoubleAnimation(0.88, 1.0, dur)
            {
                BeginTime = delay,
                EasingFunction = ease
            };
            var scaleAnimY = new DoubleAnimation(0.88, 1.0, dur)
            {
                BeginTime = delay,
                EasingFunction = ease
            };
            var opacityAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320))
            {
                BeginTime = delay
            };

            st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimX);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimY);
            btn.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
        }
    }

    #endregion

    public void Dispose()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Log("Dispose 开始");
        saveProgressTimer.Stop();
        Log($"saveProgressTimer.Stop 耗时 {sw.ElapsedMilliseconds}ms");
        SaveCurrentProgress();
        Log($"SaveCurrentProgress 完成，耗时 {sw.ElapsedMilliseconds}ms");

        OverlayGrid.MouseMove -= OverlayGrid_MouseMove;
        Log($"MouseMove 取消订阅 耗时 {sw.ElapsedMilliseconds}ms");

        mediaController.Dispose();
        Log($"mediaController.Dispose 完成，总耗时 {sw.ElapsedMilliseconds}ms");
    }

    private void OverlayGrid_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!isFullscreen) return;
        var pos = e.GetPosition(OverlayGrid);
        if (pos.Y > OverlayGrid.ActualHeight - 10)
        {
            ShowFullscreenControlBar();
        }
    }

    private void ControlBar_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!isFullscreen) return;
        controlBarHideTimer.Stop();
        ControlBar.Opacity = 1;
    }

    private void ControlBar_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!isFullscreen) return;
        controlBarHideTimer.Start();
    }

    private void ControlBarHideTimer_Tick(object? sender, EventArgs e)
    {
        if (!ControlBar.IsMouseOver)
        {
            HideFullscreenControlBar();
        }
    }

    private void ShowFullscreenControlBar()
    {
        controlBarHideTimer.Stop();
        ControlBar.Visibility = Visibility.Visible;
        ControlBar.Opacity = 1;
        ControlBar.IsHitTestVisible = true;
        Keyboard.Focus(ControlBar);
    }

    private void HideFullscreenControlBar()
    {
        ControlBar.Opacity = 0;
        ControlBar.IsHitTestVisible = false;
    }
}
