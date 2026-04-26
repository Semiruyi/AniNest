using System;
using System.Drawing;
using System.Windows.Forms;
using LibVLCSharp.WinForms;
using LocalPlayer.Services;
using LocalPlayer.Views.Controls;

namespace LocalPlayer.Views;

public class PlayerPage : UserControl
{
    private VideoView? videoView;
    private Panel? rightPanel;
    private ListBox? episodeList;
    private Button? backButton;

    // 控制栏
    private PotPlayerControlBar? controlBar;
    private System.Windows.Forms.Timer? hideControlBarTimer;

    // 双击检测相关
    private DateTime lastClickTime = DateTime.MinValue;
    private readonly TimeSpan doubleClickInterval = TimeSpan.FromMilliseconds(500);
    private bool wasMouseDown = false;
    private System.Windows.Forms.Timer? mouseCheckTimer;

    private Panel? videoContainer;

    private readonly MediaPlayerController mediaController = new();
    private readonly FullscreenManager fullscreenManager = new();

    private string[] videoFiles = Array.Empty<string>();
    private string currentFolderPath = "";
    private int lastSelectedIndex = -1;

    public event EventHandler? BackRequested;
    public event KeyEventHandler? KeyDownHandler;

    public PlayerPage()
    {
        Console.WriteLine("[PlayerPage] 构造函数开始");

        this.BackColor = Color.FromArgb(20, 20, 20);
        this.Dock = DockStyle.Fill;

        SetupControlBar();
        SetupUI();
        SetupVLC();
        SetupMouseDetection();
        SetupTimers();
        SetupFullscreenManager();

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

        rightPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 300,
            BackColor = Color.FromArgb(30, 30, 30)
        };

        backButton = new Button
        {
            Text = "← 返回",
            Font = new Font("微软雅黑", 12),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(0, 122, 204),
            FlatStyle = FlatStyle.Flat,
            Location = new Point(10, 10),
            Size = new Size(100, 35),
            Cursor = Cursors.Hand
        };
        backButton.FlatAppearance.BorderSize = 0;
        backButton.Click += (s, e) => BackRequested?.Invoke(this, EventArgs.Empty);

        episodeList = new ListBox
        {
            Location = new Point(10, 60),
            Size = new Size(rightPanel.Width - 20, 400),
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White,
            Font = new Font("微软雅黑", 11),
            BorderStyle = BorderStyle.None,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        episodeList.SelectedIndexChanged += EpisodeList_SelectedIndexChanged;

        rightPanel.Controls.Add(backButton);
        rightPanel.Controls.Add(episodeList);

        this.Controls.Add(videoContainer);
        this.Controls.Add(rightPanel);

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

        controlBar.PlayPauseClicked += (s, e) => { mediaController.TogglePlayPause(); ShowControlBar(); };
        controlBar.StopClicked += (s, e) => { mediaController.Stop(); ShowControlBar(); };
        controlBar.PreviousClicked += (s, e) => { PlayPreviousEpisode(); ShowControlBar(); };
        controlBar.NextClicked += (s, e) => { PlayNextEpisode(); ShowControlBar(); };
        controlBar.FullscreenClicked += (s, e) => fullscreenManager.ToggleFullscreen(videoContainer!, ShowControlBar);
        controlBar.SettingsClicked += (s, e) => Console.WriteLine("[控制栏] 设置按钮点击");
        controlBar.PlaylistClicked += (s, e) =>
        {
            if (rightPanel != null)
            {
                rightPanel.Visible = !rightPanel.Visible;
                PlayerPage_Resize(this, EventArgs.Empty);
            }
        };
        controlBar.VolumeChanged += (s, volume) => mediaController.SetVolume(volume);
        controlBar.MuteChanged += (s, muted) => mediaController.SetMuted(muted);
        controlBar.ProgressChanged += (s, e) => mediaController.SeekTo(e.NewTime);
    }

    private void SetupVLC()
    {
        if (videoView == null) return;

        mediaController.Initialize(videoView);
        mediaController.Playing += (s, e) => this.BeginInvoke(() => controlBar?.UpdatePlayPauseButton(true));
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
                case Keys.Up:
                    mediaController.IncreaseVolume(10);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Down:
                    mediaController.DecreaseVolume(10);
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

    private void SetupTimers()
    {
        hideControlBarTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        hideControlBarTimer.Tick += HideControlBarTimer_Tick;
    }

    private void SetupMouseDetection()
    {
        mouseCheckTimer = new System.Windows.Forms.Timer { Interval = 50 };
        mouseCheckTimer.Tick += MouseCheckTimer_Tick;
        mouseCheckTimer.Start();

        if (videoView != null)
        {
            videoView.MouseMove += (s, e) => ShowControlBar();
        }

        this.MouseMove += PlayerPage_MouseMove;

        Console.WriteLine("[鼠标检测] 定时器已启动");
    }

    private void PlayerPage_MouseMove(object? sender, MouseEventArgs e)
    {
        if (controlBar != null && e.Y > this.ClientSize.Height - 100)
        {
            ShowControlBar();
        }
    }

    private void MouseCheckTimer_Tick(object? sender, EventArgs e)
    {
        bool isMouseDown = (Control.MouseButtons & MouseButtons.Left) != 0;

        if (isMouseDown && !wasMouseDown)
        {
            if (videoView != null && IsMouseOverVideoView())
            {
                Console.WriteLine("[鼠标检测] 检测到鼠标左键按下在视频区域");
                HandleVideoClick();
            }
        }

        wasMouseDown = isMouseDown;
    }

    private bool IsMouseOverVideoView()
    {
        if (videoView == null) return false;

        try
        {
            Point screenMousePos = Control.MousePosition;
            Point clientMousePos = videoView.PointToClient(screenMousePos);
            return videoView.ClientRectangle.Contains(clientMousePos);
        }
        catch
        {
            return false;
        }
    }

    private void HandleVideoClick()
    {
        DateTime now = DateTime.Now;
        TimeSpan timeSinceLastClick = now - lastClickTime;

        if (timeSinceLastClick < doubleClickInterval && timeSinceLastClick.TotalMilliseconds > 0)
        {
            Console.WriteLine($"[视频区域] 检测到双击 (间隔 {timeSinceLastClick.TotalMilliseconds:F0}ms)");
            mediaController.TogglePlayPause();
            lastClickTime = DateTime.MinValue;
        }
        else
        {
            Console.WriteLine("[视频区域] 单击");
            ShowControlBar();
            lastClickTime = now;
        }
    }

    private void HideControlBarTimer_Tick(object? sender, EventArgs e)
    {
        if (controlBar != null && controlBar.Visible)
        {
            Point mousePos = controlBar.PointToClient(Cursor.Position);
            bool isMouseOverControlBar = controlBar.ClientRectangle.Contains(mousePos);

            bool isMouseNearBottom = false;
            if (videoContainer != null)
            {
                Point containerMousePos = videoContainer.PointToClient(Cursor.Position);
                isMouseNearBottom = containerMousePos.Y > videoContainer.Height - 100;
            }

            if (!isMouseOverControlBar && !isMouseNearBottom && !controlBar.IsProgressDragging)
            {
                controlBar.Visible = false;

                if (fullscreenManager.IsFullscreen)
                {
                    Cursor.Hide();
                }
            }
        }
        hideControlBarTimer?.Stop();
    }

    private void ShowControlBar()
    {
        if (controlBar != null && !controlBar.Visible)
        {
            controlBar.Visible = true;
            Cursor.Show();
        }
        StartHideTimer();
    }

    private void StartHideTimer()
    {
        hideControlBarTimer?.Stop();
        hideControlBarTimer?.Start();
    }

    private void PlayerPage_Resize(object? sender, EventArgs e)
    {
        if (!fullscreenManager.IsFullscreen)
        {
            if (videoContainer != null)
            {
                int rightWidth = rightPanel?.Visible == true ? rightPanel.Width : 0;
                videoContainer.Size = new Size(this.ClientSize.Width - rightWidth, this.ClientSize.Height);
                videoContainer.Location = new Point(0, 0);
            }

            if (rightPanel != null)
            {
                rightPanel.Height = this.ClientSize.Height;
                rightPanel.Location = new Point(this.ClientSize.Width - rightPanel.Width, 0);
                if (episodeList != null)
                {
                    episodeList.Height = rightPanel.Height - 120;
                }
            }
        }
    }

    public void HandleKeyDown(KeyEventArgs e)
    {
        if (IsFunctionKey(e.KeyCode))
        {
            Console.WriteLine($"[PlayerPage] 处理按键: {e.KeyCode}");
        }

        bool handled = true;

        switch (e.KeyCode)
        {
            case Keys.Space:
                mediaController.TogglePlayPause();
                ShowControlBar();
                break;
            case Keys.Left:
                mediaController.SeekBackward(5000);
                ShowControlBar();
                break;
            case Keys.Right:
                mediaController.SeekForward(5000);
                ShowControlBar();
                break;
            case Keys.Up:
                mediaController.IncreaseVolume(10);
                ShowControlBar();
                break;
            case Keys.Down:
                mediaController.DecreaseVolume(10);
                ShowControlBar();
                break;
            case Keys.F:
                fullscreenManager.ToggleFullscreen(videoContainer!, ShowControlBar);
                break;
            case Keys.Escape:
                if (fullscreenManager.IsFullscreen)
                    fullscreenManager.ExitFullscreen();
                else
                    BackRequested?.Invoke(this, EventArgs.Empty);
                break;
            case Keys.M:
                mediaController.ToggleMute();
                ShowControlBar();
                break;
            case Keys.J:
                mediaController.SeekBackward(10000);
                ShowControlBar();
                break;
            case Keys.L:
                mediaController.SeekForward(10000);
                ShowControlBar();
                break;
            case Keys.N:
            case Keys.PageDown:
                PlayNextEpisode();
                ShowControlBar();
                break;
            case Keys.P:
            case Keys.PageUp:
                PlayPreviousEpisode();
                ShowControlBar();
                break;
            default:
                handled = false;
                break;
        }

        if (handled)
        {
            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        KeyDownHandler?.Invoke(this, e);
    }

    private bool IsFunctionKey(Keys keyCode)
    {
        return keyCode == Keys.Left || keyCode == Keys.Right ||
               keyCode == Keys.Up || keyCode == Keys.Down ||
               keyCode == Keys.Space || keyCode == Keys.F ||
               keyCode == Keys.Escape || keyCode == Keys.M ||
               keyCode == Keys.J || keyCode == Keys.L ||
               keyCode == Keys.N || keyCode == Keys.P ||
               keyCode == Keys.PageUp || keyCode == Keys.PageDown;
    }

    public void LoadFolder(string folderPath, string folderName)
    {
        currentFolderPath = folderPath;
        Console.WriteLine($"[PlayerPage] 加载文件夹: {folderPath}");

        videoFiles = VideoScanner.GetVideoFiles(folderPath);
        Console.WriteLine($"[PlayerPage] 找到 {videoFiles.Length} 个视频文件");

        episodeList!.Items.Clear();
        for (int i = 0; i < videoFiles.Length; i++)
        {
            string fileName = Path.GetFileNameWithoutExtension(videoFiles[i]);
            episodeList.Items.Add($"{i + 1:00}. {fileName}");
        }

        if (videoFiles.Length > 0)
        {
            mediaController.Play(videoFiles[0]);
            episodeList.SelectedIndex = 0;
        }
    }

    private void EpisodeList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (episodeList == null) return;

        int currentIndex = episodeList.SelectedIndex;

        if (currentIndex == lastSelectedIndex)
        {
            Console.WriteLine($"[选集] 索引未变化，仍为第 {currentIndex + 1} 集");
            return;
        }

        Console.WriteLine($"[选集] 集数变化: 第 {lastSelectedIndex + 1} 集 -> 第 {currentIndex + 1} 集");

        if (currentIndex >= 0 && currentIndex < videoFiles.Length)
        {
            string fileName = Path.GetFileName(videoFiles[currentIndex]);
            Console.WriteLine($"[选集] 切换到第 {currentIndex + 1} 集: {fileName}");
            mediaController.Play(videoFiles[currentIndex]);
            lastSelectedIndex = currentIndex;
        }
        else
        {
            Console.WriteLine($"[选集] 无效的索引: {currentIndex}");
        }
    }

    private void PlayNextEpisode()
    {
        if (episodeList == null || episodeList.Items.Count == 0) return;

        int nextIndex = episodeList.SelectedIndex + 1;
        if (nextIndex < episodeList.Items.Count)
        {
            episodeList.SelectedIndex = nextIndex;
        }
    }

    private void PlayPreviousEpisode()
    {
        if (episodeList == null || episodeList.Items.Count == 0) return;

        int prevIndex = episodeList.SelectedIndex - 1;
        if (prevIndex >= 0)
        {
            episodeList.SelectedIndex = prevIndex;
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        Console.WriteLine("[PlayerPage] 正在销毁资源...");

        mouseCheckTimer?.Stop();
        mouseCheckTimer?.Dispose();
        hideControlBarTimer?.Stop();
        hideControlBarTimer?.Dispose();

        fullscreenManager.Dispose();
        mediaController.Dispose();

        base.OnHandleDestroyed(e);
    }
}
