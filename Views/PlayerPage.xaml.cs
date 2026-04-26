using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    private DispatcherTimer? controlBarHideTimer;

    public event EventHandler? BackRequested;

    public PlayerPage()
    {
        InitializeComponent();
        Loaded += PlayerPage_Loaded;
        Unloaded += PlayerPage_Unloaded;
        GotKeyboardFocus += PlayerPage_GotKeyboardFocus;
        LostKeyboardFocus += PlayerPage_LostKeyboardFocus;
        MouseMove += PlayerPage_MouseMove;

        saveProgressTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        saveProgressTimer.Tick += SaveProgressTimer_Tick;

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
        Log("Loaded 事件触发");

        parentWindow = Window.GetWindow(this);
        Log($"获取父窗口: {parentWindow?.GetType().Name}");

        // 尝试夺取焦点，防止 VideoView (WindowsFormsHost) 吃掉键盘事件
        Log($"Loaded 前 FocusedElement={FocusManager.GetFocusedElement(this)}");
        Keyboard.Focus(this);
        FocusManager.SetFocusedElement(this, this);
        Log($"Loaded 后尝试设置焦点到 PlayerPage");

        mediaController.Initialize(VideoView);
        mediaController.Playing += (s, ev) =>
        {
            Dispatcher.Invoke(() =>
            {
                PlayPauseBtn.Content = "⏸";
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
            Dispatcher.Invoke(() => PlayPauseBtn.Content = "▶");
        mediaController.Stopped += (s, ev) =>
            Dispatcher.Invoke(() => PlayPauseBtn.Content = "▶");
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
        foreach (var file in videoFiles)
        {
            PlaylistBox.Items.Add(Path.GetFileName(file));
        }

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

    private void BackBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentProgress();
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ProgressSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        isProgressDragging = true;
    }

    private void ProgressSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
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
        ControlBar.Visibility = Visibility.Collapsed;

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
        ControlBar.Visibility = Visibility.Visible;

        isFullscreen = false;
        Log("✓ 已退出全屏");
    }

    private void PlayerPage_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!isFullscreen) return;

        ControlBar.Visibility = Visibility.Visible;

        controlBarHideTimer?.Stop();
        controlBarHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        controlBarHideTimer.Tick += (_, _) =>
        {
            ControlBar.Visibility = Visibility.Collapsed;
            controlBarHideTimer?.Stop();
        };
        controlBarHideTimer.Start();
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
        saveProgressTimer.Stop();
        controlBarHideTimer?.Stop();
        SaveCurrentProgress();
        mediaController.Dispose();
    }
}
