using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.ComponentModel;
using System.IO;
using Timer = System.Windows.Forms.Timer;

namespace LocalPlayer.Views.Controls;

// PotPlayer 风格的控制栏
public class PotPlayerControlBar : UserControl
{
    // 图标资源
    private Image? playIcon;
    private Image? pauseIcon;
    private Image? stopIcon;
    private Image? previousIcon;
    private Image? nextIcon;
    private Image? settingsIcon;
    private Image? playlistIcon;
    private Image? fullscreenIcon;
    
    // 播放控制按钮
    private Button? playPauseButton;
    private Button? stopButton;
    private Button? previousButton;
    private Button? nextButton;

    // 进度条区域
    private PotPlayerProgressBar? progressBar;
    private Label? currentTimeLabel;
    private Label? totalTimeLabel;
    private Label? separatorLabel;

    // 右侧控制
    private Button? settingsButton;
    private Button? playlistButton;
    private Button? fullscreenButton;

    // 状态
    private bool isPlaying = false;
    private long currentTime = 0;
    private long totalTime = 0;

    // 颜色
    private readonly Color backgroundColor = Color.FromArgb(32, 32, 32);
    private readonly Color buttonHoverColor = Color.FromArgb(64, 64, 64);
    private readonly Color buttonPressedColor = Color.FromArgb(48, 48, 48);
    private readonly Color textColor = Color.FromArgb(200, 200, 200);

    // 事件
    public event EventHandler? PlayPauseClicked;
    public event EventHandler? StopClicked;
    public event EventHandler? PreviousClicked;
    public event EventHandler? NextClicked;
    public event EventHandler? FullscreenClicked;
    public event EventHandler? SettingsClicked;
    public event EventHandler? PlaylistClicked;
    public event EventHandler<ProgressChangedEventArgs>? ProgressChanged;

    public PotPlayerControlBar()
    {
        this.Height = 80;
        this.BackColor = backgroundColor;
        this.DoubleBuffered = true;

        LoadIcons();
        InitializeComponents();

        this.MouseMove += (s, e) => { };
    }

    private void LoadIcons()
    {
        try
        {
            string iconsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Icons");
            
            // 如果目录不存在，尝试其他可能的路径
            if (!Directory.Exists(iconsPath))
            {
                iconsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Resources", "Icons");
            }
            
            Console.WriteLine($"[控制栏] 加载图标路径: {iconsPath}");
            
            if (Directory.Exists(iconsPath))
            {
                playIcon = LoadIcon(Path.Combine(iconsPath, "play.png"));
                pauseIcon = LoadIcon(Path.Combine(iconsPath, "pause.png"));
                stopIcon = LoadIcon(Path.Combine(iconsPath, "stop.png"));
                previousIcon = LoadIcon(Path.Combine(iconsPath, "previous.png"));
                nextIcon = LoadIcon(Path.Combine(iconsPath, "next.png"));
                settingsIcon = LoadIcon(Path.Combine(iconsPath, "settings.png"));
                playlistIcon = LoadIcon(Path.Combine(iconsPath, "playlist.png"));
                fullscreenIcon = LoadIcon(Path.Combine(iconsPath, "fullscreen.png"));
                
                Console.WriteLine("[控制栏] 图标加载完成");
            }
            else
            {
                Console.WriteLine($"[控制栏] 图标文件夹不存在: {iconsPath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[控制栏] 加载图标失败: {ex.Message}");
        }
    }

    private Image? LoadIcon(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return Image.FromFile(path);
            }
            else
            {
                Console.WriteLine($"[控制栏] 图标文件不存在: {path}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[控制栏] 加载图标失败 {path}: {ex.Message}");
        }
        return null;
    }

    private Button CreateImageButton(Image? icon, string fallbackText, int x, int y, int size)
    {
        var button = new Button
        {
            Location = new Point(x, y),
            Size = new Size(size, size),
            FlatStyle = FlatStyle.Flat,
            BackColor = backgroundColor,
            ForeColor = textColor,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
            UseVisualStyleBackColor = false,
            Padding = new Padding(5),
            Margin = new Padding(0)
        };
        
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = buttonHoverColor;
        button.FlatAppearance.MouseDownBackColor = buttonPressedColor;
        
        if (icon != null)
        {
            int iconSize = size - 10;
            button.Image = new Bitmap(icon, new Size(iconSize, iconSize));
            button.ImageAlign = ContentAlignment.MiddleCenter;
        }
        else
        {
            button.Text = fallbackText;
            button.Font = new Font("Segoe UI", size == 40 ? 16 : (size == 35 ? 14 : 11));
        }
        
        return button;
    }

    private void InitializeComponents()
    {
        int buttonSize = 40;
        int centerY = (this.Height - buttonSize) / 2;
        
        // 播放/暂停按钮
        playPauseButton = CreateImageButton(playIcon, "▶", 10, centerY, buttonSize);
        playPauseButton.Click += (s, e) => PlayPauseClicked?.Invoke(this, EventArgs.Empty);

        // 停止按钮
        stopButton = CreateImageButton(stopIcon, "■", 55, centerY, buttonSize);
        stopButton.Click += (s, e) => StopClicked?.Invoke(this, EventArgs.Empty);

        // 上一集
        previousButton = CreateImageButton(previousIcon, "⏮", 100, centerY, buttonSize);
        previousButton.Click += (s, e) => PreviousClicked?.Invoke(this, EventArgs.Empty);

        // 下一集
        nextButton = CreateImageButton(nextIcon, "⏭", 145, centerY, buttonSize);
        nextButton.Click += (s, e) => NextClicked?.Invoke(this, EventArgs.Empty);

        // 标签的居中位置
        int labelCenterY = (this.Height - 20) / 2;
        
        // 当前时间标签
        currentTimeLabel = new Label
        {
            Location = new Point(195, labelCenterY),
            AutoSize = true,
            ForeColor = textColor,
            Font = new Font("Segoe UI", 10),
            Text = "00:00",
            TextAlign = ContentAlignment.MiddleRight
        };

        // 分隔符
        separatorLabel = new Label
        {
            Location = new Point(252, labelCenterY),
            AutoSize = true,
            ForeColor = textColor,
            Font = new Font("Segoe UI", 10),
            Text = "/",
            TextAlign = ContentAlignment.MiddleCenter
        };

        // 总时长标签
        totalTimeLabel = new Label
        {
            Location = new Point(264, labelCenterY),
            AutoSize = true,
            ForeColor = Color.FromArgb(150, 150, 150),
            Font = new Font("Segoe UI", 10),
            Text = "00:00",
            TextAlign = ContentAlignment.MiddleLeft
        };

        // 进度条居中
        int progressBarCenterY = (this.Height - 26) / 2;
        
        // 进度条
        progressBar = new PotPlayerProgressBar
        {
            Location = new Point(330, progressBarCenterY),
            Size = new Size(300, 26)
        };
        progressBar.ProgressChanged += (s, e) => ProgressChanged?.Invoke(this, e);

        // 右侧按钮
        int rightButtonSize = 35;
        int rightButtonCenterY = (this.Height - rightButtonSize) / 2;
        
        // 设置按钮
        settingsButton = CreateImageButton(settingsIcon, "⚙", 0, rightButtonCenterY, rightButtonSize);
        settingsButton.Click += (s, e) => SettingsClicked?.Invoke(this, EventArgs.Empty);

        // 播放列表按钮
        playlistButton = CreateImageButton(playlistIcon, "☰", 0, rightButtonCenterY, rightButtonSize);
        playlistButton.Click += (s, e) => PlaylistClicked?.Invoke(this, EventArgs.Empty);

        // 全屏按钮
        fullscreenButton = CreateImageButton(fullscreenIcon, "⛶", 0, rightButtonCenterY, rightButtonSize);
        fullscreenButton.Click += (s, e) => FullscreenClicked?.Invoke(this, EventArgs.Empty);

        // 添加到控件集合
        this.Controls.AddRange(new Control[] {
            playPauseButton, stopButton, previousButton, nextButton,
            currentTimeLabel, separatorLabel, totalTimeLabel,
            progressBar, settingsButton, playlistButton, fullscreenButton
        });

    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (progressBar != null)
        {
            int buttonSize = 40;
            int rightButtonSize = 35;
            int centerY = (this.Height - buttonSize) / 2;
            int progressBarCenterY = (this.Height - 26) / 2;
            int rightButtonCenterY = (this.Height - rightButtonSize) / 2;
            
            if (playPauseButton != null) playPauseButton.Location = new Point(10, centerY);
            if (stopButton != null) stopButton.Location = new Point(55, centerY);
            if (previousButton != null) previousButton.Location = new Point(100, centerY);
            if (nextButton != null) nextButton.Location = new Point(145, centerY);
            
            // 时间标签垂直居中
            if (currentTimeLabel != null) currentTimeLabel.Top = (this.Height - currentTimeLabel.Height) / 2;
            if (separatorLabel != null) separatorLabel.Top = (this.Height - separatorLabel.Height) / 2;
            if (totalTimeLabel != null) totalTimeLabel.Top = (this.Height - totalTimeLabel.Height) / 2;

            // 时间标签水平位置：紧跟 nextButton 右侧
            int labelSpacing = 6;
            int nextRight = nextButton?.Right ?? 185;

            if (currentTimeLabel != null) currentTimeLabel.Left = nextRight + labelSpacing;
            if (separatorLabel != null) separatorLabel.Left = (currentTimeLabel?.Right ?? 0) + labelSpacing;
            if (totalTimeLabel != null) totalTimeLabel.Left = (separatorLabel?.Right ?? 0) + labelSpacing;
            
            // 右侧按钮
            int rightMargin = 10;
            int buttonSpacing = 5;
            int rightButtonTotalWidth = 3 * 35 + 2 * buttonSpacing;
            int rightStart = this.Width - rightButtonTotalWidth - rightMargin;

            // 进度条位置紧跟时间标签区域
            int progressBarSpacing = 10;
            int progressBarX = (totalTimeLabel?.Right ?? 0) + progressBarSpacing;
            progressBar.Width = Math.Max(200, rightStart - progressBarX - progressBarSpacing);
            progressBar.Location = new Point(progressBarX, progressBarCenterY);
            
            if (settingsButton != null) settingsButton.Location = new Point(rightStart, rightButtonCenterY);
            if (playlistButton != null) playlistButton.Location = new Point(rightStart + 35 + buttonSpacing, rightButtonCenterY);
            if (fullscreenButton != null) fullscreenButton.Location = new Point(rightStart + 2 * (35 + buttonSpacing), rightButtonCenterY);
        }
    }

    public void UpdatePlayPauseButton(bool playing)
    {
        isPlaying = playing;
        if (playPauseButton != null)
        {
            if (playing && pauseIcon != null)
            {
                playPauseButton.Image = new Bitmap(pauseIcon, new Size(30, 30));
                playPauseButton.Text = "";
            }
            else if (!playing && playIcon != null)
            {
                playPauseButton.Image = new Bitmap(playIcon, new Size(30, 30));
                playPauseButton.Text = "";
            }
            else
            {
                playPauseButton.Image = null;
                playPauseButton.Text = playing ? "⏸" : "▶";
            }
        }
    }

    public void UpdateTime(long current, long total)
    {
        currentTime = current;
        totalTime = total;

        if (currentTimeLabel != null)
            currentTimeLabel.Text = FormatTime(current);
        if (totalTimeLabel != null)
            totalTimeLabel.Text = FormatTime(total);
    }



    public void UpdateProgress(long current, long total, int bufferPercent = 0)
    {
        if (progressBar != null)
        {
            progressBar.CurrentTime = current;
            progressBar.TotalTime = total;
            progressBar.BufferPercent = bufferPercent;
        }

        UpdateTime(current, total);
    }

    public bool IsProgressDragging => progressBar?.IsDragging ?? false;

    private string FormatTime(long milliseconds)
    {
        if (milliseconds < 0) milliseconds = 0;

        TimeSpan time = TimeSpan.FromMilliseconds(milliseconds);
        if (time.TotalHours >= 1)
            return time.ToString(@"hh\:mm\:ss");
        else
            return time.ToString(@"mm\:ss");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            playIcon?.Dispose();
            pauseIcon?.Dispose();
            stopIcon?.Dispose();
            previousIcon?.Dispose();
            nextIcon?.Dispose();
            settingsIcon?.Dispose();
            playlistIcon?.Dispose();
            fullscreenIcon?.Dispose();
        }
        base.Dispose(disposing);
    }
}


// PotPlayer 风格的进度条
public class PotPlayerProgressBar : Control
{
    private long currentTime = 0;
    private long totalTime = 0;
    private int bufferPercent = 0;
    private bool isDragging = false;
    private bool isHovering = false;
    private float hoverPosition = -1;

    private readonly Color trackColor = Color.FromArgb(70, 70, 70);
    private readonly Color bufferColor = Color.FromArgb(100, 100, 100);
    private readonly Color progressColor = Color.FromArgb(64, 128, 255);
    private readonly Color knobColor = Color.FromArgb(200, 200, 200);

    public event EventHandler<ProgressChangedEventArgs>? ProgressChanged;
    public event EventHandler? DragStarted;
    public event EventHandler? DragEnded;

    public PotPlayerProgressBar()
    {
        this.DoubleBuffered = true;
        this.Cursor = Cursors.Hand;
        this.MinimumSize = new Size(100, 20);

        this.MouseDown += ProgressBar_MouseDown;
        this.MouseMove += ProgressBar_MouseMove;
        this.MouseUp += ProgressBar_MouseUp;
        this.MouseEnter += (s, e) => { isHovering = true; Invalidate(); };
        this.MouseLeave += (s, e) => { isHovering = false; hoverPosition = -1; Invalidate(); };
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long CurrentTime
    {
        get => currentTime;
        set { currentTime = value; Invalidate(); }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long TotalTime
    {
        get => totalTime;
        set { totalTime = value; Invalidate(); }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int BufferPercent
    {
        get => bufferPercent;
        set { bufferPercent = Math.Max(0, Math.Min(100, value)); Invalidate(); }
    }

    public bool IsDragging => isDragging;

    private void ProgressBar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && totalTime > 0)
        {
            isDragging = true;
            DragStarted?.Invoke(this, EventArgs.Empty);
            UpdateProgressFromMouse(e.X);
        }
    }

    private void ProgressBar_MouseMove(object? sender, MouseEventArgs e)
    {
        if (totalTime > 0)
        {
            hoverPosition = e.X;
            Invalidate();

            if (isDragging)
            {
                UpdateProgressFromMouse(e.X);
            }
        }
    }

    private void ProgressBar_MouseUp(object? sender, MouseEventArgs e)
    {
        if (isDragging)
        {
            isDragging = false;
            UpdateProgressFromMouse(e.X);
            DragEnded?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateProgressFromMouse(int mouseX)
    {
        float percent = (float)mouseX / Width;
        percent = Math.Max(0, Math.Min(1, percent));

        long newTime = (long)(totalTime * percent);
        ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(newTime, percent));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int trackHeight = isHovering || isDragging ? 5 : 3;
        int trackY = (Height - trackHeight) / 2;

        using (SolidBrush trackBrush = new SolidBrush(trackColor))
        {
            g.FillRectangle(trackBrush, 0, trackY, Width, trackHeight);
        }

        if (bufferPercent > 0 && totalTime > 0)
        {
            int bufferWidth = (int)(Width * bufferPercent / 100f);
            using (SolidBrush bufferBrush = new SolidBrush(bufferColor))
            {
                g.FillRectangle(bufferBrush, 0, trackY, bufferWidth, trackHeight);
            }
        }

        if (totalTime > 0)
        {
            float progressPercent = (float)currentTime / totalTime;
            int progressWidth = (int)(Width * progressPercent);

            using (SolidBrush progressBrush = new SolidBrush(progressColor))
            {
                g.FillRectangle(progressBrush, 0, trackY, progressWidth, trackHeight);
            }

            if ((isHovering || isDragging) && progressWidth > 0)
            {
                int knobSize = isDragging ? 12 : 10;
                int knobX = progressWidth - knobSize / 2;
                int knobY = (Height - knobSize) / 2;

                using (SolidBrush knobBrush = new SolidBrush(knobColor))
                {
                    g.FillEllipse(knobBrush, knobX, knobY, knobSize, knobSize);
                }
            }
        }

        if (hoverPosition >= 0 && hoverPosition <= Width && totalTime > 0 && !isDragging)
        {
            using (SolidBrush hoverBrush = new SolidBrush(Color.FromArgb(50, 255, 255, 255)))
            {
                g.FillRectangle(hoverBrush, 0, trackY, (int)hoverPosition, trackHeight);
            }
        }
    }
}
// 进度变更事件参数
public class ProgressChangedEventArgs : EventArgs
{
    public long NewTime { get; }
    public float Percent { get; }

    public ProgressChangedEventArgs(long newTime, float percent)
    {
        NewTime = newTime;
        Percent = percent;
    }
}