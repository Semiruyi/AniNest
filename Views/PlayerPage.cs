using System;
using System.Drawing;
using System.Windows.Forms;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;

namespace LocalPlayer.Views;

public class PlayerPage : UserControl
{
    private LibVLC? libVLC;
    private MediaPlayer? mediaPlayer;
    private VideoView? videoView;
    private Panel? rightPanel;
    private ListBox? episodeList;
    private Button? backButton;
    
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

    public event EventHandler? BackRequested;
    public event KeyEventHandler? KeyDownHandler;

    public PlayerPage()
    {
        Console.WriteLine("[PlayerPage] 构造函数开始");
        
        this.BackColor = Color.FromArgb(20, 20, 20);
        this.Dock = DockStyle.Fill;
        
        SetupUI();
        SetupVLC();
        SetupMouseDetection();
        
        Console.WriteLine("[PlayerPage] 初始化完成");
    }

    private void SetupUI()
    {
        // 左侧视频区域 (70%)
        videoView = new VideoView
        {
            Dock = DockStyle.None,
            BackColor = Color.Black
        };
        
        // 右侧选集面板 (30%)
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
            Location = new Point(10, 50),
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
        
        this.Controls.Add(videoView);
        this.Controls.Add(rightPanel);
        
        this.Resize += PlayerPage_Resize;
    }

    private void SetupVLC()
    {
        libVLC = new LibVLC();
        mediaPlayer = new MediaPlayer(libVLC);
        
        if (videoView != null)
        {
            videoView.MediaPlayer = mediaPlayer;
        }
        
        Console.WriteLine("[VLC] 初始化完成");
    }

    private void SetupMouseDetection()
    {
        // 创建定时器检测鼠标点击
        mouseCheckTimer = new System.Windows.Forms.Timer();
        mouseCheckTimer.Interval = 50; // 50ms 检测一次
        mouseCheckTimer.Tick += MouseCheckTimer_Tick;
        mouseCheckTimer.Start();
        
        Console.WriteLine("[鼠标检测] 定时器已启动");
    }

    private void MouseCheckTimer_Tick(object? sender, EventArgs e)
    {
        // 检查鼠标左键状态
        bool isMouseDown = (Control.MouseButtons & MouseButtons.Left) != 0;
        
        // 检测鼠标按下事件（从释放到按下）
        if (isMouseDown && !wasMouseDown)
        {
            // 鼠标刚按下
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
            // 获取鼠标在屏幕上的位置
            Point screenMousePos = Control.MousePosition;
            
            // 转换为 VideoView 的客户区坐标
            Point clientMousePos = videoView.PointToClient(screenMousePos);
            
            // 检查是否在 VideoView 的客户区内
            bool isOver = videoView.ClientRectangle.Contains(clientMousePos);
            
            return isOver;
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
            // 短时间内第二次点击，视为双击
            Console.WriteLine($"[视频区域] 检测到双击 (间隔 {timeSinceLastClick.TotalMilliseconds:F0}ms)");
            
            if (isFullscreen)
            {
                Console.WriteLine("[全屏] 双击退出全屏");
                ToggleFullScreen();
            }
            else
            {
                Console.WriteLine("[视频区域] 双击切换暂停/播放");
                TogglePlayPause();
            }
            
            // 重置时间
            lastClickTime = DateTime.MinValue;
        }
        else
        {
            // 第一次单击
            Console.WriteLine("[视频区域] 第一次单击，等待第二次点击...");
            lastClickTime = now;
        }
    }

    private void PlayerPage_Resize(object? sender, EventArgs e)
    {
        if (videoView != null && rightPanel != null && !isFullscreen)
        {
            videoView.Size = new Size(this.ClientSize.Width - rightPanel.Width, this.ClientSize.Height);
            videoView.Location = new Point(0, 0);
            
            rightPanel.Height = this.ClientSize.Height;
            episodeList!.Height = this.ClientSize.Height - 120;
        }
    }

    public void HandleKeyDown(KeyEventArgs e)
    {
        // 只记录功能键，避免刷屏
        if (IsFunctionKey(e.KeyCode))
        {
            Console.WriteLine($"[PlayerPage] 处理按键: {e.KeyCode}");
        }
        
        bool handled = true;
        
        switch (e.KeyCode)
        {
            case Keys.Space:
                Console.WriteLine("[空格键] ✓ 暂停/播放");
                TogglePlayPause();
                break;
                
            case Keys.Left:
                Console.WriteLine("[左键] ✓ 后退5秒");
                SeekBackward(5000);
                break;
                
            case Keys.Right:
                Console.WriteLine("[右键] ✓ 前进5秒");
                SeekForward(5000);
                break;
                
            case Keys.Up:
                Console.WriteLine("[上键] ✓ 增加音量");
                IncreaseVolume(10);
                break;
                
            case Keys.Down:
                Console.WriteLine("[下键] ✓ 减少音量");
                DecreaseVolume(10);
                break;
                
            case Keys.F:
                Console.WriteLine("[F键] ✓ 切换全屏");
                ToggleFullScreen();
                break;
                
            case Keys.Escape:
                Console.WriteLine("[ESC键] ✓ 退出全屏");
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
                Console.WriteLine("[M键] ✓ 静音切换");
                ToggleMute();
                break;
                
            case Keys.J:
                Console.WriteLine("[J键] ✓ 后退10秒");
                SeekBackward(10000);
                break;
                
            case Keys.L:
                Console.WriteLine("[L键] ✓ 前进10秒");
                SeekForward(10000);
                break;
                
            case Keys.N:
            case Keys.PageDown:
                Console.WriteLine("[N/PageDown键] ✓ 下一集");
                PlayNextEpisode();
                break;
                
            case Keys.P:
            case Keys.PageUp:
                Console.WriteLine("[P/PageUp键] ✓ 上一集");
                PlayPreviousEpisode();
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

    // 判断是否是功能键
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
    
    // 切换播放/暂停
    public void TogglePlayPause()
    {
        if (mediaPlayer == null)
        {
            Console.WriteLine("[VLC] ✗ mediaPlayer 为空，无法切换播放状态");
            return;
        }
        
        if (mediaPlayer.IsPlaying)
        {
            Console.WriteLine("[VLC] ⏸ 暂停播放");
            mediaPlayer.Pause();
        }
        else
        {
            Console.WriteLine("[VLC] ▶ 恢复播放");
            mediaPlayer.Play();
        }
    }
    
    // 前进/后退
    private void SeekForward(long milliseconds)
    {
        if (mediaPlayer == null || mediaPlayer.Length <= 0)
        {
            Console.WriteLine("[VLC] ✗ 无法跳转：播放器未就绪或视频未加载");
            return;
        }
        
        long currentTime = mediaPlayer.Time;
        long totalLength = mediaPlayer.Length;
        long newTime = Math.Min(totalLength, currentTime + milliseconds);
        
        Console.WriteLine($"[VLC] 前进: {FormatTime(currentTime)} -> {FormatTime(newTime)} (前进 {milliseconds/1000}秒)");
        mediaPlayer.Time = newTime;
    }
    
    private void SeekBackward(long milliseconds)
    {
        if (mediaPlayer == null || mediaPlayer.Length <= 0)
        {
            Console.WriteLine("[VLC] ✗ 无法跳转：播放器未就绪或视频未加载");
            return;
        }
        
        long currentTime = mediaPlayer.Time;
        long newTime = Math.Max(0, currentTime - milliseconds);
        
        Console.WriteLine($"[VLC] 后退: {FormatTime(currentTime)} -> {FormatTime(newTime)} (后退 {milliseconds/1000}秒)");
        mediaPlayer.Time = newTime;
    }
    
    // 音量控制
    private void IncreaseVolume(int amount)
    {
        if (mediaPlayer == null) return;
        
        int newVolume = Math.Min(100, mediaPlayer.Volume + amount);
        mediaPlayer.Volume = newVolume;
        Console.WriteLine($"[VLC] 音量增加: {newVolume}%");
    }
    
    private void DecreaseVolume(int amount)
    {
        if (mediaPlayer == null) return;
        
        int newVolume = Math.Max(0, mediaPlayer.Volume - amount);
        mediaPlayer.Volume = newVolume;
        Console.WriteLine($"[VLC] 音量减少: {newVolume}%");
    }
    
    private void ToggleMute()
    {
        if (mediaPlayer == null) return;
        
        mediaPlayer.Mute = !mediaPlayer.Mute;
        Console.WriteLine($"[VLC] 静音: {(mediaPlayer.Mute ? "开启" : "关闭")}");
    }
    
    // 全屏控制
    private void ToggleFullScreen()
    {
        if (mediaPlayer == null || videoView == null)
        {
            Console.WriteLine("[全屏] ✗ 无法切换：播放器未就绪");
            return;
        }
        
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
        
        // 保存主窗体引用
        mainForm = this.FindForm();
        if (mainForm == null)
        {
            Console.WriteLine("[全屏] ✗ 无法找到主窗体");
            return;
        }
        
        // 保存 VideoView 的原始状态
        originalParent = videoView!.Parent;
        originalIndex = originalParent!.Controls.GetChildIndex(videoView);
        originalDock = videoView.Dock;
        originalSize = videoView.Size;
        originalLocation = videoView.Location;
        
        // 创建全屏窗体
        fullscreenForm = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            WindowState = FormWindowState.Maximized,
            TopMost = true,
            BackColor = Color.Black,
            KeyPreview = true
        };
        
        // 添加键盘事件处理
        fullscreenForm.KeyDown += FullscreenForm_KeyDown;
        
        // 从原容器中移除 VideoView
        originalParent.Controls.Remove(videoView);
        
        // 将 VideoView 添加到全屏窗体
        fullscreenForm.Controls.Add(videoView);
        videoView.Dock = DockStyle.Fill;
        
        // 隐藏主窗体
        mainForm.Hide();
        
        // 显示全屏窗体
        fullscreenForm.Show();
        
        // 强制刷新 VideoView
        videoView.Invalidate();
        videoView.Update();
        
        isFullscreen = true;
        Console.WriteLine("[全屏] ✓ 已进入全屏（双击可退出）");
    }
    
    private void ExitFullScreen()
    {
        Console.WriteLine("[全屏] 退出全屏模式");
        
        if (fullscreenForm == null || videoView == null || originalParent == null || mainForm == null)
        {
            Console.WriteLine("[全屏] ✗ 无法退出全屏：状态异常");
            return;
        }
        
        // 从全屏窗体中移除 VideoView
        fullscreenForm.Controls.Remove(videoView);
        
        // 恢复 VideoView 到原始容器
        originalParent.Controls.Add(videoView);
        originalParent.Controls.SetChildIndex(videoView, originalIndex);
        
        // 恢复 VideoView 的原始属性
        videoView.Dock = originalDock;
        videoView.Size = originalSize;
        videoView.Location = originalLocation;
        
        // 关闭全屏窗体
        fullscreenForm.Close();
        fullscreenForm.Dispose();
        fullscreenForm = null;
        
        // 显示主窗体
        mainForm.Show();
        mainForm.Focus();
        
        // 强制刷新 VideoView
        videoView.Invalidate();
        videoView.Update();
        
        isFullscreen = false;
        Console.WriteLine("[全屏] ✓ 已退出全屏");
    }
    
    private void FullscreenForm_KeyDown(object? sender, KeyEventArgs e)
    {
        // 全屏模式下的键盘事件处理
        Console.WriteLine($"[全屏键盘] {e.KeyCode}");
        
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
    
    // 剧集切换
    private void PlayNextEpisode()
    {
        if (episodeList == null || episodeList.Items.Count == 0) return;
        
        int nextIndex = episodeList.SelectedIndex + 1;
        if (nextIndex < episodeList.Items.Count)
        {
            episodeList.SelectedIndex = nextIndex;
            Console.WriteLine($"[选集] 切换到下一集: 第 {nextIndex + 1} 集");
        }
        else
        {
            Console.WriteLine("[选集] 已经是最后一集");
        }
    }
    
    private void PlayPreviousEpisode()
    {
        if (episodeList == null || episodeList.Items.Count == 0) return;
        
        int prevIndex = episodeList.SelectedIndex - 1;
        if (prevIndex >= 0)
        {
            episodeList.SelectedIndex = prevIndex;
            Console.WriteLine($"[选集] 切换到上一集: 第 {prevIndex + 1} 集");
        }
        else
        {
            Console.WriteLine("[选集] 已经是第一集");
        }
    }
    
    // 辅助方法：格式化时间
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
        
        // 扫描视频文件
        videoFiles = Services.VideoScanner.GetVideoFiles(folderPath);
        Console.WriteLine($"[PlayerPage] 找到 {videoFiles.Length} 个视频文件");
        
        // 填充选集列表
        episodeList!.Items.Clear();
        for (int i = 0; i < videoFiles.Length; i++)
        {
            string fileName = Path.GetFileNameWithoutExtension(videoFiles[i]);
            episodeList.Items.Add($"{i + 1:00}. {fileName}");
        }
        
        // 自动播放第一集
        if (videoFiles.Length > 0)
        {
            PlayVideo(videoFiles[0]);
            episodeList.SelectedIndex = 0;
        }
    }

    private void PlayVideo(string filePath)
    {
        if (mediaPlayer == null || libVLC == null)
        {
            Console.WriteLine("[VLC] ✗ mediaPlayer 或 libVLC 为空，无法播放");
            return;
        }
        
        Console.WriteLine($"[VLC] 开始播放: {Path.GetFileName(filePath)}");
        var media = new Media(libVLC, filePath);
        mediaPlayer.Play(media);
    }

    private void EpisodeList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (episodeList!.SelectedIndex >= 0 && episodeList.SelectedIndex < videoFiles.Length)
        {
            Console.WriteLine($"[选集] 切换到第 {episodeList.SelectedIndex + 1} 集");
            PlayVideo(videoFiles[episodeList.SelectedIndex]);
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        Console.WriteLine("[PlayerPage] 正在销毁资源...");
        
        // 停止定时器
        mouseCheckTimer?.Stop();
        mouseCheckTimer?.Dispose();
        
        // 退出全屏
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