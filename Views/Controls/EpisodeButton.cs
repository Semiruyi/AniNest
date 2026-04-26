using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LocalPlayer.Views.Controls;

public enum EpisodePlayState
{
    Unplayed,
    Playing,
    Played
}

public class EpisodeButton : Control
{
    private EpisodePlayState playState = EpisodePlayState.Unplayed;
    private bool isHovered = false;
    private bool isPressed = false;

    private readonly System.Windows.Forms.Timer animTimer;
    private float hoverScale = 1.0f;
    private float pressScale = 1.0f;
    private float pressVelocity = 0f;
    private float hoverTarget = 1.0f;
    private float pressTarget = 1.0f;

    private Size normalSize;
    private Point normalLocation;

    // 颜色定义
    private static readonly Color UnplayedColor = Color.FromArgb(80, 80, 80);
    private static readonly Color PlayingColor = Color.FromArgb(0, 122, 204);
    private static readonly Color PlayedColor = Color.FromArgb(92, 159, 214);
    private static readonly Color HoverColor = Color.FromArgb(0, 150, 240);

    [DllImport("winmm.dll")]
    private static extern int timeBeginPeriod(int uPeriod);

    [DllImport("winmm.dll")]
    private static extern int timeEndPeriod(int uPeriod);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int EpisodeIndex { get; set; }
    
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string FilePath { get; set; } = "";

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EpisodePlayState PlayState
    {
        get => playState;
        set
        {
            playState = value;
            Invalidate();
        }
    }

    public EpisodeButton()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw, true);

        Cursor = Cursors.Hand;
        Font = new Font("微软雅黑", 11, FontStyle.Bold);

        // 提高系统计时器精度到 1ms，这样 WinForms Timer 才能突破 64Hz 限制
        timeBeginPeriod(1);

        // 6ms ≈ 166fps，在 160Hz 显示器上接近满帧
        animTimer = new System.Windows.Forms.Timer { Interval = 6 };
        animTimer.Tick += AnimTimer_Tick;
    }

    public void SetNormalBounds(Rectangle bounds)
    {
        normalSize = bounds.Size;
        normalLocation = bounds.Location;
        this.Bounds = bounds;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        isHovered = true;
        hoverTarget = 1.15f;
        BringToFront();
        EnsureTimerRunning();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        isHovered = false;
        hoverTarget = 1.0f;
        EnsureTimerRunning();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            isPressed = true;
            pressTarget = 0.96f;  // iOS 风格：按下只轻微缩小到 96%
            pressVelocity = 0;
            EnsureTimerRunning();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            isPressed = false;
            pressTarget = 1.0f;
            EnsureTimerRunning();
        }
    }

    private void EnsureTimerRunning()
    {
        if (!animTimer.Enabled)
            animTimer.Start();
    }

    private void AnimTimer_Tick(object? sender, EventArgs e)
    {
        bool animating = false;

        // 悬浮放大动画（原来的线性逼近）
        if (Math.Abs(hoverTarget - hoverScale) > 0.001f)
        {
            hoverScale += (hoverTarget - hoverScale) * 0.18f;
            animating = true;
        }
        else
        {
            hoverScale = hoverTarget;
        }

        // 按下/松开弹簧动画（iOS 风格：阻尼更小，回弹时有过冲）
        if (Math.Abs(pressTarget - pressScale) > 0.001f || Math.Abs(pressVelocity) > 0.001f)
        {
            const float tension = 0.35f;
            const float damping = 0.55f;  // 阻尼更小 → 回弹时带过冲（overshoot）
            pressVelocity += (pressTarget - pressScale) * tension;
            pressVelocity *= damping;
            pressScale += pressVelocity;
            animating = true;
        }
        else
        {
            pressScale = pressTarget;
            pressVelocity = 0;
        }

        if (animating)
        {
            UpdateButtonBounds();
            Invalidate();
        }
        else
        {
            animTimer.Stop();
        }
    }

    private void UpdateButtonBounds()
    {
        float totalScale = hoverScale * pressScale;
        int newW = (int)(normalSize.Width * totalScale);
        int newH = (int)(normalSize.Height * totalScale);
        int newX = normalLocation.X + (normalSize.Width - newW) / 2;
        int newY = normalLocation.Y + (normalSize.Height - newH) / 2;
        this.Bounds = new Rectangle(newX, newY, newW, newH);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        Color baseColor = playState switch
        {
            EpisodePlayState.Playing => PlayingColor,
            EpisodePlayState.Played => PlayedColor,
            _ => UnplayedColor
        };

        // 悬浮时变蓝色高亮
        if (isHovered)
        {
            baseColor = HoverColor;
        }

        // 按下时颜色稍暗
        if (isPressed)
        {
            baseColor = ControlPaint.Dark(baseColor, 0.08f);
        }

        int radius = 10;
        var bgRect = new Rectangle(0, 0, Width - 1, Height - 1);

        using (var path = GetRoundedRectangle(bgRect, radius))
        using (var brush = new SolidBrush(baseColor))
        {
            e.Graphics.FillPath(brush, path);
        }

        // 绘制数字——使用固定的 normalSize 区域，这样按钮缩放时文字位置不动
        string text = EpisodeIndex.ToString();
        int textOffsetX = (Width - normalSize.Width) / 2;
        int textOffsetY = (Height - normalSize.Height) / 2;
        var textRect = new Rectangle(textOffsetX, textOffsetY, normalSize.Width, normalSize.Height);
        TextRenderer.DrawText(e.Graphics, text, Font, textRect, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private static GraphicsPath GetRoundedRectangle(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int r = Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2);
        path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
        path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
        path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            animTimer?.Stop();
            animTimer?.Dispose();
            timeEndPeriod(1);
        }
        base.Dispose(disposing);
    }
}
