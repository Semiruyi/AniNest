using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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
    private bool pendingSeek = false;
    private long pendingSeekTime = 0;

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

        // 尝试夺取焦点，防止 VideoView (WindowsFormsHost) 吃掉键盘事件
        Log($"Loaded 前 FocusedElement={FocusManager.GetFocusedElement(this)}");
        Keyboard.Focus(this);
        FocusManager.SetFocusedElement(this, this);
        Log($"Loaded 后尝试设置焦点到 PlayerPage");

        mediaController.Initialize(VideoView);

        OverlayGrid.MouseMove += OverlayGrid_MouseMove;

        // 在 WPF 层面试试（大概率被 VideoView 的 Airspace 拦截，仅用于日志对比）
        VideoContainer.MouseLeftButtonDown += VideoContainer_MouseLeftButtonDown;
        VideoContainer.MouseRightButtonDown += VideoContainer_MouseRightButtonDown;

        mediaController.Playing += (s, ev) =>
        {
            Dispatcher.Invoke(() =>
            {
                PlayPauseIcon.Source = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/Resources/Icons/pause.png"));
                if (pendingSeek && pendingSeekTime > 0)
                {
                    var seekTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                    seekTimer.Tick += (_, _) =>
                    {
                        seekTimer.Stop();
                        mediaController.SeekTo(pendingSeekTime);
                        pendingSeek = false;
                        pendingSeekTime = 0;
                    };
                    seekTimer.Start();
                }
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

            // 阻止 ForegroundWindow 被点击激活，防止它抢走键盘焦点
            FixForegroundWindowNoActivate();
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

    #region ForegroundWindow 无激活修复

    private static class NativeMethods
    {
        public const int GWL_EXSTYLE = -20;
        public const uint WS_EX_NOACTIVATE = 0x08000000;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        public const uint SWP_FRAMECHANGED = 0x0020;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
    }

    private void FixForegroundWindowNoActivate()
    {
        try
        {
            var foregroundWindowField = typeof(LibVLCSharp.WPF.VideoView).GetField(
                "ForegroundWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (foregroundWindowField == null)
            {
                Log("FixForegroundWindowNoActivate: 未找到 ForegroundWindow 字段");
                return;
            }

            var foregroundWindow = foregroundWindowField.GetValue(VideoView) as Window;
            if (foregroundWindow == null)
            {
                Log("FixForegroundWindowNoActivate: ForegroundWindow 为 null");
                return;
            }

            if (foregroundWindow.IsLoaded)
            {
                ApplyNoActivate(foregroundWindow);
            }
            else
            {
                foregroundWindow.Loaded += (s, e) => ApplyNoActivate(foregroundWindow);
            }
        }
        catch (Exception ex)
        {
            Log($"FixForegroundWindowNoActivate 异常: {ex.Message}");
        }
    }

    private void ApplyNoActivate(Window foregroundWindow)
    {
        try
        {
            var helper = new WindowInteropHelper(foregroundWindow);
            var hwnd = helper.Handle;
            if (hwnd == IntPtr.Zero)
            {
                Log("ApplyNoActivate: HWND 为 0");
                return;
            }

            var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_NOACTIVATE);
            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_NOTOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_FRAMECHANGED |
                NativeMethods.SWP_NOMOVE |
                NativeMethods.SWP_NOSIZE |
                NativeMethods.SWP_NOZORDER |
                NativeMethods.SWP_NOACTIVATE);

            Log("ApplyNoActivate: 已设置 WS_EX_NOACTIVATE");
        }
        catch (Exception ex)
        {
            Log($"ApplyNoActivate 异常: {ex.Message}");
        }
    }

    #endregion

    private class PlaylistItem
    {
        public int Number { get; set; }
        public string Title { get; set; } = "";
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
            PlaylistBox.Items.Add(new PlaylistItem { Number = i + 1, Title = Path.GetFileName(videoFiles[i]) });
        }
        EpisodeCountText.Text = videoFiles.Length > 0 ? $"共 {videoFiles.Length} 集" : "";

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
                PlaylistBox.SelectedIndex = index;
            }
        }
    }

    private void PlayVideo(string filePath)
    {
        Log($"PlayVideo 被调用: {filePath}");
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

        pendingSeek = startTime > 0;
        pendingSeekTime = startTime;

        mediaController.Play(filePath);
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

        FullscreenIcon.Source = new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/Resources/Icons/exitFullScreen.png"));

        isFullscreen = true;
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

    private void VideoContainer_GotFocus(object sender, RoutedEventArgs e)
    {
        Log("VideoContainer GotFocus");
    }

    private void VideoContainer_LostFocus(object sender, RoutedEventArgs e)
    {
        Log("VideoContainer LostFocus");
    }

    private void VideoView_GotFocus(object sender, RoutedEventArgs e)
    {
        Log("VideoView GotFocus — 尝试将焦点移回 PlayerPage 以避免事件被吞");
        // WindowsFormsHost 获得焦点后会吞掉键盘事件，尝试将焦点移回 WPF 元素
        Dispatcher.BeginInvoke(new Action(() =>
        {
            Keyboard.Focus(this);
            Log("VideoView GotFocus 后尝试设置焦点到 PlayerPage");
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void VideoView_LostFocus(object sender, RoutedEventArgs e)
    {
        Log("VideoView LostFocus");
    }

    private void PlayNext()
    {
        if (PlaylistBox.SelectedIndex < PlaylistBox.Items.Count - 1)
        {
            PlaylistBox.SelectedIndex++;
        }
    }

    private void PlayPrevious()
    {
        if (PlaylistBox.SelectedIndex > 0)
        {
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
    }

    private void HideFullscreenControlBar()
    {
        ControlBar.Opacity = 0;
        ControlBar.IsHitTestVisible = false;
    }
}
