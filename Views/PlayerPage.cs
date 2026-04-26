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
    private System.Windows.Forms.Timer? hideControlBarTimer;

    private readonly MediaPlayerController mediaController = new();
    private readonly FullscreenManager fullscreenManager = new();
    private readonly PlayerInputHandler inputHandler = new();

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
        SetupTimers();
        SetupFullscreenManager();
        SetupInputHandler();
        SetupMouseDetection();

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
        playlistPanel.BackClicked += (s, e) => BackRequested?.Invoke(this, EventArgs.Empty);
        playlistPanel.EpisodeChanged += (s, filePath) => mediaController.Play(filePath);

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

        controlBar.PlayPauseClicked += (s, e) => { mediaController.TogglePlayPause(); ShowControlBar(); };
        controlBar.StopClicked += (s, e) => { mediaController.Stop(); ShowControlBar(); };
        controlBar.PreviousClicked += (s, e) => { playlistPanel?.PlayPrevious(); ShowControlBar(); };
        controlBar.NextClicked += (s, e) => { playlistPanel?.PlayNext(); ShowControlBar(); };
        controlBar.FullscreenClicked += (s, e) => fullscreenManager.ToggleFullscreen(videoContainer!, ShowControlBar);
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
        inputHandler.TogglePlayPause += (s, e) => { mediaController.TogglePlayPause(); ShowControlBar(); };
        inputHandler.SeekForward += (s, e) => { mediaController.SeekForward(5000); ShowControlBar(); };
        inputHandler.SeekBackward += (s, e) => { mediaController.SeekBackward(5000); ShowControlBar(); };
        inputHandler.ToggleFullscreen += (s, e) => fullscreenManager.ToggleFullscreen(videoContainer!, ShowControlBar);
        inputHandler.ExitFullscreen += (s, e) => fullscreenManager.ExitFullscreen();
        inputHandler.Back += (s, e) => BackRequested?.Invoke(this, EventArgs.Empty);
        inputHandler.NextEpisode += (s, e) => { playlistPanel?.PlayNext(); ShowControlBar(); };
        inputHandler.PreviousEpisode += (s, e) => { playlistPanel?.PlayPrevious(); ShowControlBar(); };
    }

    private void SetupTimers()
    {
        hideControlBarTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        hideControlBarTimer.Tick += HideControlBarTimer_Tick;
    }

    private void SetupMouseDetection()
    {
        if (videoContainer != null)
        {
            videoContainer.MouseClick += (s, e) => ShowControlBar();
            videoContainer.MouseDoubleClick += (s, e) => mediaController.TogglePlayPause();
            videoContainer.MouseMove += (s, e) => ShowControlBar();
        }

        this.MouseMove += PlayerPage_MouseMove;

        Console.WriteLine("[鼠标检测] 事件绑定完成");
    }

    private void PlayerPage_MouseMove(object? sender, MouseEventArgs e)
    {
        if (controlBar != null && e.Y > this.ClientSize.Height - 100)
        {
            ShowControlBar();
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

        playlistPanel?.LoadFolder(folderPath);

        var firstPath = playlistPanel?.FirstEpisodePath;
        if (firstPath != null)
        {
            mediaController.Play(firstPath);
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        Console.WriteLine("[PlayerPage] 正在销毁资源...");

        hideControlBarTimer?.Stop();
        hideControlBarTimer?.Dispose();

        fullscreenManager.Dispose();
        mediaController.Dispose();

        base.OnHandleDestroyed(e);
    }
}
