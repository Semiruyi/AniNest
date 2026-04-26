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
    private readonly DispatcherTimer saveProgressTimer;

    private string currentFolderPath = "";
    private string currentFolderName = "";
    private string[] videoFiles = Array.Empty<string>();
    private string? pendingLoadFolderPath;
    private string? pendingLoadFolderName;
    private bool isProgressDragging = false;
    private bool pendingSeek = false;
    private long pendingSeekTime = 0;

    public event EventHandler? BackRequested;

    public PlayerPage()
    {
        InitializeComponent();
        Loaded += PlayerPage_Loaded;
        Unloaded += PlayerPage_Unloaded;

        saveProgressTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        saveProgressTimer.Tick += SaveProgressTimer_Tick;
    }

    private void PlayerPage_Loaded(object sender, RoutedEventArgs e)
    {
        Log("Loaded 事件触发");
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
        mediaController.TogglePlayPause();
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e)
    {
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

    public void HandleKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        var handler = new PlayerInputHandler();
        handler.TogglePlayPause += (_, _) => mediaController.TogglePlayPause();
        handler.SeekForward += (_, _) => mediaController.SeekForward(5000);
        handler.SeekBackward += (_, _) => mediaController.SeekBackward(5000);
        handler.Back += (_, _) =>
        {
            SaveCurrentProgress();
            BackRequested?.Invoke(this, EventArgs.Empty);
        };
        handler.NextEpisode += (_, _) => PlayNext();
        handler.PreviousEpisode += (_, _) => PlayPrevious();

        if (handler.HandleKeyDown(e, false))
        {
            e.Handled = true;
        }
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
        SaveCurrentProgress();
        mediaController.Dispose();
    }
}
