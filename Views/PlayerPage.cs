using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LibVLCSharp.WinForms;
using LocalPlayer.Services;
using LocalPlayer.Views.Controls;

namespace LocalPlayer.Views;

public class PlayerPage : UserControl
{
    private VideoView? videoView;
    private Panel? videoContainer;
    private PlaylistPanel? playlistPanel;

    // 控制栏
    private PotPlayerControlBar? controlBar;

    private readonly MediaPlayerController mediaController = new();
    private readonly FullscreenManager fullscreenManager = new();
    private readonly PlayerInputHandler inputHandler = new();
    private readonly SettingsService settingsService = new();
    private readonly System.Windows.Forms.Timer saveProgressTimer;

    private string currentFolderPath = "";
    private string currentFolderName = "";
    private long pendingSeekTime = -1;

    public event EventHandler? BackRequested;
    public event KeyEventHandler? KeyDownHandler;

    [DllImport("user32.dll")]
    private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

    public PlayerPage()
    {
        Console.WriteLine("[PlayerPage] 构造函数开始");

        this.BackColor = Color.FromArgb(20, 20, 20);
        this.Dock = DockStyle.Fill;

        SetupControlBar();
        SetupUI();
        SetupVLC();
        SetupFullscreenManager();
        SetupInputHandler();
        SetupMouseDetection();

        // 定时保存播放进度（每5秒）
        saveProgressTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        saveProgressTimer.Tick += SaveProgressTimer_Tick;
        saveProgressTimer.Start();

        Console.WriteLine("[PlayerPage] 初始化完成");
    }

    private void SetupUI()
    {
        videoView = new VideoView
        {
            Dock = DockStyle.None,
            BackColor = Color.Black
        };

        videoContainer = new Panel
        {
            Dock = DockStyle.None,
            BackColor = Color.Black
        };

        videoContainer.Controls.Add(videoView);
        videoView.Dock = DockStyle.Fill;

        playlistPanel = new PlaylistPanel();
        playlistPanel.BackClicked += (s, e) => SaveCurrentProgressAndGoBack();
        playlistPanel.EpisodeChanged += (s, filePath) =>
        {
            Console.WriteLine($"[PlayerPage] EpisodeChanged: {Path.GetFileName(filePath)}");
            SaveCurrentVideoProgress();
            PlayVideo(filePath, true);
        };

        this.Controls.Add(videoContainer);
        this.Controls.Add(playlistPanel);

        if (controlBar != null)
        {
            videoContainer.Controls.Add(controlBar);
            controlBar.Dock = DockStyle.Bottom;
            controlBar.Height = 70;
            controlBar.BringToFront();
            controlBar.Visible = true;
        }

        this.Resize += PlayerPage_Resize;
        PlayerPage_Resize(this, EventArgs.Empty);
    }

    private void SetupControlBar()
    {
        controlBar = new PotPlayerControlBar
        {
            Height = 70,
            Visible = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        controlBar.PlayPauseClicked += (s, e) => mediaController.TogglePlayPause();
        controlBar.StopClicked += (s, e) => mediaController.Stop();
        controlBar.PreviousClicked += (s, e) => playlistPanel?.PlayPrevious();
        controlBar.NextClicked += (s, e) => playlistPanel?.PlayNext();
        controlBar.FullscreenClicked += (s, e) =>
        {
            fullscreenManager.ToggleFullscreen(videoContainer!);
            if (controlBar != null) controlBar.Visible = !fullscreenManager.IsFullscreen;
        };
        controlBar.SettingsClicked += (s, e) => Console.WriteLine("[控制栏] 设置按钮点击");
        controlBar.PlaylistClicked += (s, e) =>
        {
            playlistPanel?.ToggleVisibility();
            PlayerPage_Resize(this, EventArgs.Empty);
        };
        controlBar.ProgressChanged += (s, e) => mediaController.SeekTo(e.NewTime);
    }

    private void SetupVLC()
    {
        if (videoView == null) return;

        // 禁用 VideoView 的鼠标/键盘输入，让事件落到父容器 videoContainer 上
        EnableWindow(videoView.Handle, false);

        mediaController.Initialize(videoView);
        mediaController.Playing += (s, e) =>
        {
            Console.WriteLine("[PlayerPage] mediaController.Playing 事件");
            this.BeginInvoke(() =>
            {
                // 如果有待seek的时间，延迟一点再执行，等 Length 准备好
                if (pendingSeekTime > 0)
                {
                    long targetTime = pendingSeekTime;
                    pendingSeekTime = -1;
                    var seekTimer = new System.Windows.Forms.Timer { Interval = 300 };
                    seekTimer.Tick += (_, _) =>
                    {
                        seekTimer.Stop();
                        seekTimer.Dispose();
                        Console.WriteLine($"[PlayerPage] 延迟 seek 到 {targetTime}ms");
                        mediaController.SeekTo(targetTime);
                    };
                    seekTimer.Start();
                }
                controlBar?.UpdatePlayPauseButton(true);
            });
        };
        mediaController.Paused += (s, e) => this.BeginInvoke(() => controlBar?.UpdatePlayPauseButton(false));
        mediaController.Stopped += (s, e) => this.BeginInvoke(() => controlBar?.UpdatePlayPauseButton(false));
        mediaController.ProgressUpdated += (s, e) =>
        {
            if (controlBar != null && !controlBar.IsProgressDragging)
            {
                this.BeginInvoke(() => controlBar.UpdateProgress(e.CurrentTime, e.TotalTime, 0));
            }
        };
    }

    private void SetupFullscreenManager()
    {
        fullscreenManager.KeyDown += (s, e) =>
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                case Keys.F:
                    fullscreenManager.ExitFullscreen();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Space:
                    mediaController.TogglePlayPause();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Left:
                    mediaController.SeekBackward(5000);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Right:
                    mediaController.SeekForward(5000);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
            }
        };
        fullscreenManager.Exited += (s, e) =>
        {
            if (controlBar != null) controlBar.Visible = true;
            Cursor.Show();
        };
    }

    private void SetupInputHandler()
    {
        inputHandler.TogglePlayPause += (s, e) => mediaController.TogglePlayPause();
        inputHandler.SeekForward += (s, e) => mediaController.SeekForward(5000);
        inputHandler.SeekBackward += (s, e) => mediaController.SeekBackward(5000);
        inputHandler.ToggleFullscreen += (s, e) =>
        {
            fullscreenManager.ToggleFullscreen(videoContainer!);
            if (controlBar != null) controlBar.Visible = !fullscreenManager.IsFullscreen;
        };
        inputHandler.ExitFullscreen += (s, e) => fullscreenManager.ExitFullscreen();
        inputHandler.Back += (s, e) => SaveCurrentProgressAndGoBack();
        inputHandler.NextEpisode += (s, e) => playlistPanel?.PlayNext();
        inputHandler.PreviousEpisode += (s, e) => playlistPanel?.PlayPrevious();
    }

    private const int ControlBarHotZoneHeight = 100;

    private void SetupMouseDetection()
    {
        if (videoContainer != null)
        {
            videoContainer.MouseDoubleClick += (s, e) => mediaController.TogglePlayPause();
            videoContainer.MouseMove += (s, e) => UpdateControlBarVisibility();
            videoContainer.MouseLeave += (s, e) => UpdateControlBarVisibility();
        }

        if (controlBar != null)
        {
            controlBar.MouseMove += (s, e) => UpdateControlBarVisibility();
            controlBar.MouseLeave += (s, e) => UpdateControlBarVisibility();
        }

        Console.WriteLine("[鼠标检测] 事件绑定完成");
    }

    private void UpdateControlBarVisibility()
    {
        if (controlBar == null || videoContainer == null) return;

        // 非全屏时控制栏始终显示
        if (!fullscreenManager.IsFullscreen)
        {
            controlBar.Visible = true;
            return;
        }

        // 全屏时使用热点逻辑
        Point barPos = controlBar.PointToClient(Cursor.Position);
        bool isOverBar = controlBar.ClientRectangle.Contains(barPos);

        Point containerPos = videoContainer.PointToClient(Cursor.Position);
        bool isOverHotZone = videoContainer.ClientRectangle.Contains(containerPos)
                          && containerPos.Y > videoContainer.Height - ControlBarHotZoneHeight;

        controlBar.Visible = isOverBar || isOverHotZone;
    }

    private void PlayerPage_Resize(object? sender, EventArgs e)
    {
        if (!fullscreenManager.IsFullscreen)
        {
            if (videoContainer != null)
            {
                int rightWidth = playlistPanel?.Visible == true ? playlistPanel.Width : 0;
                videoContainer.Size = new Size(this.ClientSize.Width - rightWidth, this.ClientSize.Height);
                videoContainer.Location = new Point(0, 0);
            }

            if (playlistPanel != null)
            {
                playlistPanel.Height = this.ClientSize.Height;
                playlistPanel.Location = new Point(this.ClientSize.Width - playlistPanel.Width, 0);
            }
        }
    }

    public void HandleKeyDown(KeyEventArgs e)
    {
        if (inputHandler.HandleKeyDown(e, fullscreenManager.IsFullscreen))
        {
            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        KeyDownHandler?.Invoke(this, e);
    }

    public void LoadFolder(string folderPath, string folderName)
    {
        Console.WriteLine($"[PlayerPage] 加载文件夹: {folderPath}");

        currentFolderPath = folderPath;
        currentFolderName = folderName;

        // 加载播放列表（不触发播放）
        playlistPanel?.LoadFolder(folderPath);

        // 获取文件夹进度，找到上次播放的视频
        var folderProgress = settingsService.GetFolderProgress(folderPath);
        string? targetVideoPath = folderProgress?.LastVideoPath;

        // 如果上次播放的视频已不存在，回退到第一个
        if (string.IsNullOrEmpty(targetVideoPath) || !File.Exists(targetVideoPath))
        {
            targetVideoPath = playlistPanel?.FirstEpisodePath;
        }

        if (string.IsNullOrEmpty(targetVideoPath))
        {
            Console.WriteLine("[PlayerPage] 没有可播放的视频");
            return;
        }

        // 选中目标视频（不触发 EpisodeChanged）
        playlistPanel?.SelectVideo(targetVideoPath);

        // 获取该视频的上次播放时间点
        long startTime = 0;
        var videoProgress = settingsService.GetVideoProgress(targetVideoPath);
        if (videoProgress != null)
        {
            startTime = videoProgress.Position;
            // 如果已经播放到结尾附近（90%以上），从头开始
            if (videoProgress.Duration > 0 && startTime > videoProgress.Duration * 0.9)
            {
                startTime = 0;
            }
        }

        Console.WriteLine($"[PlayerPage] 准备播放: {Path.GetFileName(targetVideoPath)}, startTime={startTime}ms");
        PlayVideo(targetVideoPath, false, startTime);
    }

    private void PlayVideo(string filePath, bool fromUserSelection, long startTime = 0)
    {
        Console.WriteLine($"[PlayerPage] PlayVideo 调用: {Path.GetFileName(filePath)}, fromUserSelection={fromUserSelection}, startTime={startTime}");

        // 用户手动选择时，从保存的进度恢复（如果存在）
        if (fromUserSelection)
        {
            var progress = settingsService.GetVideoProgress(filePath);
            if (progress != null)
            {
                startTime = progress.Position;
                if (progress.Duration > 0 && startTime > progress.Duration * 0.9)
                {
                    startTime = 0;
                }
            }
            else
            {
                startTime = 0;
            }
        }

        pendingSeekTime = startTime;
        mediaController.Play(filePath);
        settingsService.SetFolderProgress(currentFolderPath, filePath);
        settingsService.MarkVideoPlayed(filePath);
        playlistPanel?.RefreshPlayStatus();
    }

    private void SaveCurrentVideoProgress()
    {
        string? filePath = mediaController.CurrentFilePath;
        if (string.IsNullOrEmpty(filePath)) return;

        long time = mediaController.Time;
        long length = mediaController.Length;

        if (length > 0)
        {
            settingsService.SetVideoProgress(filePath, time, length);
            Console.WriteLine($"[PlayerPage] 保存进度: {Path.GetFileName(filePath)} - {MediaPlayerController.FormatTime(time)} / {MediaPlayerController.FormatTime(length)}");
        }
    }

    private void SaveCurrentProgressAndGoBack()
    {
        SaveCurrentVideoProgress();
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SaveProgressTimer_Tick(object? sender, EventArgs e)
    {
        SaveCurrentVideoProgress();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        Console.WriteLine("[PlayerPage] 正在销毁资源...");

        saveProgressTimer?.Stop();
        saveProgressTimer?.Dispose();

        SaveCurrentVideoProgress();

        fullscreenManager.Dispose();
        mediaController.Dispose();

        base.OnHandleDestroyed(e);
    }
}
