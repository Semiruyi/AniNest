using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using LocalPlayer.Models;
using LocalPlayer.Services;
using LocalPlayer.Views.Controls;

namespace LocalPlayer.Views;

public class PlayerPage : UserControl
{
    private LibVLC? libVLC;
    private MediaPlayer? mediaPlayer;
    private VideoView? videoView;
    private Panel? rightPanel;
    private ListBox? episodeList;
    private Button? backButton;

    // 控制栏
    private PotPlayerControlBar? controlBar;
    private System.Windows.Forms.Timer? hideControlBarTimer;
    private System.Windows.Forms.Timer? updateTimer;
    private bool isControlBarVisible = true;

    private string[] videoFiles = Array.Empty<string>();
    private string currentFolderPath = "";

    // 双击检测相关
    private DateTime lastClickTime = DateTime.MinValue;
    private readonly TimeSpan doubleClickInterval = TimeSpan.FromMilliseconds(500);
    private bool wasMouseDown = false;
    private System.Windows.Forms.Timer? mouseCheckTimer;

    // 全屏相关
    private bool isFullscreen = false;
    private Form? fullscreenForm = null;
    private Form? mainForm = null;
    private Control? originalParent = null;
    private int originalIndex = -1;
    private DockStyle originalDock;
    private Size originalSize;
    private Point originalLocation;

    private int lastSelectedIndex = -1;

    private Panel? videoContainer;

    // 弹幕
    private DanmakuOverlay? danmakuOverlay;
    private DanmakuInputBar? danmakuInputBar;
    private DanmakuService danmakuService = new();
    private string currentVideoPath = "";

    public event EventHandler? BackRequested;
    public event KeyEventHandler? KeyDownHandler;

    public PlayerPage()
    {
        Console.WriteLine("[PlayerPage] 构造函数开始");

        this.BackColor = Color.FromArgb(20, 20, 20);
        this.Dock = DockStyle.Fill;

        SetupControlBar();  // 先创建控制栏
        SetupUI();          // 再创建 UI（会使用 controlBar）
        SetupVLC();
        SetupMouseDetection();
        SetupTimers();

        Console.WriteLine("[PlayerPage] 初始化完成");
    }
    private void SetupUI()
    {
        // 左侧视频区域
        videoView = new VideoView
        {
            Dock = DockStyle.None,
            BackColor = Color.Black
        };

        // 创建视频容器（用于容纳 VideoView 和控制栏）
        videoContainer = new Panel
        {
            Dock = DockStyle.None,
            BackColor = Color.Black
        };

        // 将 VideoView 添加到容器
        videoContainer.Controls.Add(videoView);
        videoView.Dock = DockStyle.Fill;

        // 弹幕覆盖层（独立透明窗口，不作为子控件）
        danmakuOverlay = new DanmakuOverlay();
        danmakuOverlay.AttachTo(videoContainer);
        danmakuOverlay.Start();

        // 右侧选集面板
        rightPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 300,
            BackColor = Color.FromArgb(30, 30, 30)
        };

        // 返回按钮
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

        // 选集列表
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

        // 将视频容器添加到主控件
        this.Controls.Add(videoContainer);
        this.Controls.Add(rightPanel);

        // 将控制栏添加到视频容器（在添加到主控件之后）
        if (controlBar != null)
        {
            videoContainer.Controls.Add(controlBar);
            controlBar.Dock = DockStyle.Bottom;
            controlBar.Height = 70;
            controlBar.BringToFront();
            controlBar.Visible = true;
        }

        // 弹幕输入栏（在控制栏上方）
        danmakuInputBar = new DanmakuInputBar
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            Visible = true
        };
        danmakuInputBar.DanmakuSent += DanmakuInputBar_DanmakuSent;
        danmakuInputBar.ToggleRequested += DanmakuInputBar_ToggleRequested;
        danmakuInputBar.TestDanmakuRequested += DanmakuInputBar_TestDanmakuRequested;
        videoContainer.Controls.Add(danmakuInputBar);

        this.Resize += PlayerPage_Resize;

        // 初始化布局
        PlayerPage_Resize(this, EventArgs.Empty);
    }


    private void DanmakuInputBar_DanmakuSent(object? sender, string text)
    {
        if (mediaPlayer == null || string.IsNullOrEmpty(currentVideoPath)) return;

        double timeSeconds = mediaPlayer.Time / 1000.0;
        var color = danmakuInputBar?.GetSelectedColor() ?? Color.White;

        var item = new DanmakuItem
        {
            Text = text,
            TimeSeconds = timeSeconds,
            ColorHex = ColorTranslator.ToHtml(color),
            FontSize = 22,
            Speed = 120f
        };

        danmakuService.Add(currentVideoPath, item);
        danmakuOverlay?.UpdateVideoTime(timeSeconds);
        Console.WriteLine($"[弹幕] 发送: \"{text}\" @ {timeSeconds:F1}s");
    }

    private void DanmakuInputBar_ToggleRequested(object? sender, EventArgs e)
    {
        if (danmakuOverlay != null && danmakuInputBar != null)
        {
            danmakuOverlay.DanmakuEnabled = danmakuInputBar.DanmakuEnabled;
            Console.WriteLine($"[弹幕] {(danmakuInputBar.DanmakuEnabled ? "开启" : "关闭")}");
        }
    }

    private void DanmakuInputBar_TestDanmakuRequested(object? sender, EventArgs e)
    {
        if (mediaPlayer == null || mediaPlayer.Length <= 0)
        {
            Console.WriteLine("[弹幕测试] 请先播放视频");
            return;
        }

        double totalSeconds = mediaPlayer.Length / 1000.0;
        var testDanmaku = GenerateTestDanmaku(totalSeconds);

        danmakuOverlay?.Clear();
        danmakuOverlay?.LoadDanmaku(testDanmaku);

        // 同时保存为该视频的弹幕
        if (!string.IsNullOrEmpty(currentVideoPath))
            danmakuService.Save(currentVideoPath, testDanmaku);

        Console.WriteLine($"[弹幕测试] 已生成 {testDanmaku.Count} 条均匀分布弹幕 (时长 {totalSeconds:F0}s)");
    }

    private static readonly string[] SampleTexts = {
        "前方高能！", "哈哈哈笑死", "太强了", "666666", "awsl",
        "来了来了", "名场面", "泪目", "好家伙", "妙啊",
        "弹幕护体", "下次一定", "爷青回", "破防了", "好活",
        "上手了", "细节", "这也太美了", "催更催更", "冲冲冲",
        "太甜了吧", "笑不活了", "合理怀疑", "有内味了", "开局就王炸",
        "绝绝子", "不是吧不是吧", "直接起飞", "格局打开了", "芜湖起飞"
    };

    private static readonly string[] SampleColors = {
        "#FFFFFF", "#FFFFFF", "#FFFFFF", "#FFFFFF",
        "#FF6666", "#FF6666",
        "#FFAA00", "#FFD700",
        "#66FF66", "#66FF66",
        "#66CCFF", "#66CCFF",
        "#FF66CC", "#FF66CC",
        "#AAAAFF", "#CCCCCC"
    };

    private static List<DanmakuItem> GenerateTestDanmaku(double totalSeconds)
    {
        var rng = new Random();
        int count = Math.Max(30, (int)(totalSeconds / 2));
        count = Math.Min(count, 500);

        var danmaku = new List<DanmakuItem>();
        double interval = totalSeconds / count;

        for (int i = 0; i < count; i++)
        {
            double time = interval * i + rng.NextDouble() * interval * 0.8;
            time = Math.Min(time, totalSeconds - 0.5);

            danmaku.Add(new DanmakuItem
            {
                Text = SampleTexts[rng.Next(SampleTexts.Length)],
                TimeSeconds = Math.Max(0, time),
                ColorHex = SampleColors[rng.Next(SampleColors.Length)],
                FontSize = rng.Next(18, 28),
                Speed = 100f + rng.Next(0, 50)
            });
        }

        danmaku.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));
        return danmaku;
    }

    private void SetupControlBar()
    {
        controlBar = new PotPlayerControlBar
        {
            Height = 70,
            Visible = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        // 绑定控制栏事件
        controlBar.PlayPauseClicked += ControlBar_PlayPauseClicked;
        controlBar.StopClicked += ControlBar_StopClicked;
        controlBar.PreviousClicked += ControlBar_PreviousClicked;
        controlBar.NextClicked += ControlBar_NextClicked;
        controlBar.FullscreenClicked += ControlBar_FullscreenClicked;
        controlBar.SettingsClicked += ControlBar_SettingsClicked;
        controlBar.PlaylistClicked += ControlBar_PlaylistClicked;
        controlBar.VolumeChanged += ControlBar_VolumeChanged;
        controlBar.MuteChanged += ControlBar_MuteChanged;
        controlBar.ProgressChanged += ControlBar_ProgressChanged;

        // 控制栏作为视频区域的子控件（叠加层）
        // 注意：不要在这里添加到 Controls，稍后在 SetupUI 中添加
    }

    private void SetupTimers()
    {
        // 自动隐藏控制栏的定时器
        hideControlBarTimer = new System.Windows.Forms.Timer
        {
            Interval = 3000
        };
        hideControlBarTimer.Tick += HideControlBarTimer_Tick;

        // 更新进度的定时器
        updateTimer = new System.Windows.Forms.Timer
        {
            Interval = 200
        };
        updateTimer.Tick += UpdateTimer_Tick;
        updateTimer.Start();
    }

    private void SetupVLC()
    {
        libVLC = new LibVLC();
        mediaPlayer = new MediaPlayer(libVLC);

        if (videoView != null)
        {
            videoView.MediaPlayer = mediaPlayer;
        }

        // 监听播放状态变化
        mediaPlayer.Playing += (s, e) =>
        {
            if (this.IsHandleCreated)
                this.BeginInvoke(new Action(() =>
                {
                    controlBar?.UpdatePlayPauseButton(true);
                }));
        };

        mediaPlayer.Paused += (s, e) =>
        {
            if (this.IsHandleCreated)
                this.BeginInvoke(new Action(() =>
                {
                    controlBar?.UpdatePlayPauseButton(false);
                }));
        };

        mediaPlayer.Stopped += (s, e) =>
        {
            if (this.IsHandleCreated)
                this.BeginInvoke(new Action(() =>
                {
                    controlBar?.UpdatePlayPauseButton(false);
                }));
        };

        Console.WriteLine("[VLC] 初始化完成");
    }

    private void SetupMouseDetection()
    {
        mouseCheckTimer = new System.Windows.Forms.Timer();
        mouseCheckTimer.Interval = 50;
        mouseCheckTimer.Tick += MouseCheckTimer_Tick;
        mouseCheckTimer.Start();

        // 在视频区域和控制栏上处理鼠标移动
        if (videoView != null)
        {
            videoView.MouseMove += (s, e) => ShowControlBar();
        }

        this.MouseMove += PlayerPage_MouseMove;

        Console.WriteLine("[鼠标检测] 定时器已启动");
    }

    private void PlayerPage_MouseMove(object? sender, MouseEventArgs e)
    {
        // 检查鼠标是否在底部区域（控制栏区域）
        if (controlBar != null && e.Y > this.ClientSize.Height - 100)
        {
            ShowControlBar();
        }
    }

    // 控制栏事件处理
    private void ControlBar_PlayPauseClicked(object? sender, EventArgs e)
    {
        TogglePlayPause();
        ShowControlBar();
    }

    private void ControlBar_StopClicked(object? sender, EventArgs e)
    {
        Stop();
        ShowControlBar();
    }

    private void ControlBar_PreviousClicked(object? sender, EventArgs e)
    {
        PlayPreviousEpisode();
        ShowControlBar();
    }

    private void ControlBar_NextClicked(object? sender, EventArgs e)
    {
        PlayNextEpisode();
        ShowControlBar();
    }

    private void ControlBar_FullscreenClicked(object? sender, EventArgs e)
    {
        ToggleFullScreen();
    }

    private void ControlBar_SettingsClicked(object? sender, EventArgs e)
    {
        Console.WriteLine("[控制栏] 设置按钮点击");
        // TODO: 显示设置菜单
    }

    private void ControlBar_PlaylistClicked(object? sender, EventArgs e)
    {
        Console.WriteLine("[控制栏] 播放列表按钮点击");
        // 切换右侧面板显示
        if (rightPanel != null)
        {
            rightPanel.Visible = !rightPanel.Visible;
            PlayerPage_Resize(this, EventArgs.Empty);
        }
    }

    private void ControlBar_VolumeChanged(object? sender, int volume)
    {
        if (mediaPlayer != null)
        {
            mediaPlayer.Volume = volume;
            Console.WriteLine($"[音量] 设置为 {volume}%");
        }
    }

    private void ControlBar_MuteChanged(object? sender, bool muted)
    {
        if (mediaPlayer != null)
        {
            mediaPlayer.Mute = muted;
            Console.WriteLine($"[静音] {(muted ? "开启" : "关闭")}");
        }
    }

    private void ControlBar_ProgressChanged(object? sender, ProgressChangedEventArgs e)
    {
        if (mediaPlayer != null && mediaPlayer.Length > 0)
        {
            mediaPlayer.Time = e.NewTime;
            Console.WriteLine($"[进度] 跳转到 {FormatTime(e.NewTime)}");
        }
    }

    // 定时器事件
    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (mediaPlayer != null && controlBar != null && mediaPlayer.Length > 0)
        {
            // 只在非拖动状态下更新进度条
            if (!controlBar.IsProgressDragging)
            {
                long currentTime = mediaPlayer.Time;
                long totalTime = mediaPlayer.Length;

                // Buffering 是事件，不能直接读取值
                // 我们暂时不使用缓冲百分比，或者可以自己跟踪
                controlBar.UpdateProgress(currentTime, totalTime, 0);
            }

            // 同步弹幕时间
            if (mediaPlayer.IsPlaying)
            {
                danmakuOverlay?.UpdateVideoTime(mediaPlayer.Time / 1000.0);
            }
        }
    }

    private void HideControlBarTimer_Tick(object? sender, EventArgs e)
    {
        // 检查鼠标是否在控制栏上
        if (controlBar != null && controlBar.Visible)
        {
            Point mousePos = controlBar.PointToClient(Cursor.Position);
            bool isMouseOverControlBar = controlBar.ClientRectangle.Contains(mousePos);

            // 检查鼠标是否在视频容器底部区域
            bool isMouseNearBottom = false;
            if (videoContainer != null)
            {
                Point containerMousePos = videoContainer.PointToClient(Cursor.Position);
                isMouseNearBottom = containerMousePos.Y > videoContainer.Height - 100;
            }

            if (!isMouseOverControlBar && !isMouseNearBottom && !controlBar.IsProgressDragging)
            {
                controlBar.Visible = false;
                isControlBarVisible = false;

                // 全屏时也隐藏鼠标
                if (isFullscreen)
                {
                    Cursor.Hide();
                }
            }
        }
        hideControlBarTimer?.Stop();
    }

    // 控制栏显示/隐藏
    private void ShowControlBar()
    {
        if (controlBar != null)
        {
            if (!controlBar.Visible)
            {
                controlBar.Visible = true;
                Cursor.Show();
            }
        }

        StartHideTimer();
    }

    private void StartHideTimer()
    {
        hideControlBarTimer?.Stop();
        hideControlBarTimer?.Start();
    }

    // 停止播放
    private void Stop()
    {
        if (mediaPlayer != null)
        {
            mediaPlayer.Stop();
            controlBar?.UpdatePlayPauseButton(false);
            controlBar?.UpdateProgress(0, 0);
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

    // 修改 HandleVideoClick 方法
    private void HandleVideoClick()
    {
        DateTime now = DateTime.Now;
        TimeSpan timeSinceLastClick = now - lastClickTime;

        if (timeSinceLastClick < doubleClickInterval && timeSinceLastClick.TotalMilliseconds > 0)
        {
            Console.WriteLine($"[视频区域] 检测到双击 (间隔 {timeSinceLastClick.TotalMilliseconds:F0}ms)");

            TogglePlayPause();

            lastClickTime = DateTime.MinValue;
        }
        else
        {
            // 单击：只显示控制栏，不隐藏
            Console.WriteLine("[视频区域] 单击");
            ShowControlBar();

            lastClickTime = now;
        }
    }
    private void PlayerPage_Resize(object? sender, EventArgs e)
    {
        if (!isFullscreen)
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
                TogglePlayPause();
                ShowControlBar();
                break;

            case Keys.Left:
                SeekBackward(5000);
                ShowControlBar();
                break;

            case Keys.Right:
                SeekForward(5000);
                ShowControlBar();
                break;

            case Keys.Up:
                IncreaseVolume(10);
                ShowControlBar();
                break;

            case Keys.Down:
                DecreaseVolume(10);
                ShowControlBar();
                break;

            case Keys.F:
                ToggleFullScreen();
                break;

            case Keys.Escape:
                if (isFullscreen)
                {
                    ToggleFullScreen();
                }
                else
                {
                    BackRequested?.Invoke(this, EventArgs.Empty);
                }
                break;

            case Keys.M:
                ToggleMute();
                ShowControlBar();
                break;

            case Keys.J:
                SeekBackward(10000);
                ShowControlBar();
                break;

            case Keys.L:
                SeekForward(10000);
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

    public void TogglePlayPause()
    {
        if (mediaPlayer == null) return;

        if (mediaPlayer.IsPlaying)
        {
            mediaPlayer.Pause();
        }
        else
        {
            mediaPlayer.Play();
        }
    }

    private void SeekForward(long milliseconds)
    {
        if (mediaPlayer == null || mediaPlayer.Length <= 0) return;

        long currentTime = mediaPlayer.Time;
        long totalLength = mediaPlayer.Length;
        long newTime = Math.Min(totalLength, currentTime + milliseconds);
        mediaPlayer.Time = newTime;
    }

    private void SeekBackward(long milliseconds)
    {
        if (mediaPlayer == null || mediaPlayer.Length <= 0) return;

        long currentTime = mediaPlayer.Time;
        long newTime = Math.Max(0, currentTime - milliseconds);
        mediaPlayer.Time = newTime;
    }

    private void IncreaseVolume(int amount)
    {
        if (mediaPlayer == null) return;

        int newVolume = Math.Min(100, mediaPlayer.Volume + amount);
        mediaPlayer.Volume = newVolume;
        controlBar?.SetVolume(newVolume);
    }

    private void DecreaseVolume(int amount)
    {
        if (mediaPlayer == null) return;

        int newVolume = Math.Max(0, mediaPlayer.Volume - amount);
        mediaPlayer.Volume = newVolume;
        controlBar?.SetVolume(newVolume);
    }

    private void ToggleMute()
    {
        if (mediaPlayer == null) return;

        mediaPlayer.Mute = !mediaPlayer.Mute;
        controlBar?.SetMuted(mediaPlayer.Mute);
    }

    private void ToggleFullScreen()
    {
        if (mediaPlayer == null || videoView == null) return;

        if (!isFullscreen)
        {
            EnterFullScreen();
        }
        else
        {
            ExitFullScreen();
        }
    }

    private void EnterFullScreen()
    {
        Console.WriteLine("[全屏] 进入全屏模式");

        mainForm = this.FindForm();
        if (mainForm == null) return;

        // 找到视频容器
        Panel? videoContainer = videoView?.Parent as Panel;
        if (videoContainer == null) return;

        // 保存视频容器的原始状态
        originalParent = videoContainer.Parent;
        originalIndex = originalParent!.Controls.GetChildIndex(videoContainer);
        originalDock = videoContainer.Dock;
        originalSize = videoContainer.Size;
        originalLocation = videoContainer.Location;

        // 创建全屏窗体
        fullscreenForm = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            WindowState = FormWindowState.Maximized,
            TopMost = true,
            BackColor = Color.Black,
            KeyPreview = true
        };

        fullscreenForm.KeyDown += FullscreenForm_KeyDown;
        fullscreenForm.MouseMove += (s, e) =>
        {
            // 鼠标在底部区域时显示控制栏
            if (e.Y > fullscreenForm.ClientSize.Height - 100)
            {
                ShowControlBar();
            }
        };

        // 将整个视频容器移到全屏窗体
        originalParent.Controls.Remove(videoContainer);
        fullscreenForm.Controls.Add(videoContainer);
        videoContainer.Dock = DockStyle.Fill;

        mainForm.Hide();
        fullscreenForm.Show();

        videoContainer.Invalidate();
        videoContainer.Update();

        // 弹幕窗口跟随全屏
        danmakuOverlay?.AttachTo(videoContainer);

        isFullscreen = true;
        StartHideTimer();

        Console.WriteLine("[全屏] ✓ 已进入全屏");
    }


    private void ExitFullScreen()
    {
        Console.WriteLine("[全屏] 退出全屏模式");

        if (fullscreenForm == null || originalParent == null || mainForm == null)
            return;

        // 找到视频容器
        Panel? videoContainer = videoView?.Parent as Panel;
        if (videoContainer == null) return;

        // 从全屏窗体中移除视频容器
        fullscreenForm.Controls.Remove(videoContainer);

        // 恢复视频容器到原始容器
        originalParent.Controls.Add(videoContainer);
        originalParent.Controls.SetChildIndex(videoContainer, originalIndex);

        // 恢复视频容器的原始属性
        videoContainer.Dock = originalDock;
        videoContainer.Size = originalSize;
        videoContainer.Location = originalLocation;

        // 关闭全屏窗体
        fullscreenForm.Close();
        fullscreenForm.Dispose();
        fullscreenForm = null;

        // 显示主窗体
        mainForm.Show();
        mainForm.Focus();

        videoContainer.Invalidate();
        videoContainer.Update();

        // 弹幕窗口跟随恢复
        danmakuOverlay?.AttachTo(videoContainer);

        isFullscreen = false;
        isControlBarVisible = true;
        controlBar!.Visible = true;
        Cursor.Show();

        Console.WriteLine("[全屏] ✓ 已退出全屏");
    }
    private void FullscreenForm_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Escape:
            case Keys.F:
                ToggleFullScreen();
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;

            case Keys.Space:
                TogglePlayPause();
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;

            case Keys.Left:
                SeekBackward(5000);
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;

            case Keys.Right:
                SeekForward(5000);
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;

            case Keys.Up:
                IncreaseVolume(10);
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;

            case Keys.Down:
                DecreaseVolume(10);
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;
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

    private string FormatTime(long milliseconds)
    {
        TimeSpan time = TimeSpan.FromMilliseconds(milliseconds);
        if (time.TotalHours >= 1)
            return time.ToString(@"hh\:mm\:ss");
        else
            return time.ToString(@"mm\:ss");
    }

    public void LoadFolder(string folderPath, string folderName)
    {
        currentFolderPath = folderPath;
        Console.WriteLine($"[PlayerPage] 加载文件夹: {folderPath}");

        videoFiles = Services.VideoScanner.GetVideoFiles(folderPath);
        Console.WriteLine($"[PlayerPage] 找到 {videoFiles.Length} 个视频文件");

        episodeList!.Items.Clear();
        for (int i = 0; i < videoFiles.Length; i++)
        {
            string fileName = Path.GetFileNameWithoutExtension(videoFiles[i]);
            episodeList.Items.Add($"{i + 1:00}. {fileName}");
        }

        if (videoFiles.Length > 0)
        {
            PlayVideo(videoFiles[0]);
            episodeList.SelectedIndex = 0;
        }

        // 显示弹幕覆盖层
        danmakuOverlay?.Show();
    }

    private void PlayVideo(string filePath)
    {
        if (mediaPlayer == null || libVLC == null) return;

        Console.WriteLine($"[VLC] 开始播放: {Path.GetFileName(filePath)}");
        var media = new Media(libVLC, filePath);
        mediaPlayer.Play(media);

        // 加载弹幕
        currentVideoPath = filePath;
        var danmakuList = danmakuService.Load(filePath);
        danmakuOverlay?.Clear();
        danmakuOverlay?.LoadDanmaku(danmakuList);
        Console.WriteLine($"[弹幕] 已加载 {danmakuList.Count} 条弹幕");
    }

    private void EpisodeList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (episodeList == null) return;

        int currentIndex = episodeList.SelectedIndex;

        // 检查是否真的有变化
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

            PlayVideo(videoFiles[currentIndex]);

            // 更新上次选中的索引
            lastSelectedIndex = currentIndex;
        }
        else
        {
            Console.WriteLine($"[选集] 无效的索引: {currentIndex}");
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        Console.WriteLine("[PlayerPage] 正在销毁资源...");

        mouseCheckTimer?.Stop();
        mouseCheckTimer?.Dispose();
        hideControlBarTimer?.Stop();
        hideControlBarTimer?.Dispose();
        updateTimer?.Stop();
        updateTimer?.Dispose();

        danmakuOverlay?.Stop();
        danmakuOverlay?.Hide();
        danmakuOverlay?.Close();
        danmakuOverlay?.Dispose();

        if (isFullscreen)
        {
            ExitFullScreen();
        }

        mediaPlayer?.Stop();
        mediaPlayer?.Dispose();
        libVLC?.Dispose();
        base.OnHandleDestroyed(e);
    }
}