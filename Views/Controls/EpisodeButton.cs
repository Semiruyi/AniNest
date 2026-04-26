using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
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

        animTimer = new System.Windows.Forms.Timer { Interval = 16 };
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
            pressTarget = 0.88f;
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

        // 悬浮放大动画（平滑插值）
        if (Math.Abs(hoverTarget - hoverScale) > 0.001f)
        {
            hoverScale += (hoverTarget - hoverScale) * 0.18f;
            animating = true;
        }
        else
        {
            hoverScale = hoverTarget;
        }

        // 按下Q弹动画（弹簧物理）
        if (Math.Abs(pressTarget - pressScale) > 0.001f || Math.Abs(pressVelocity) > 0.001f)
        {
            const float tension = 0.3f;
            const float damping = 0.72f;
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

        // 悬浮时变蓝色高亮（即使播放过或未播放都统一变亮蓝）
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
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        using (var path = GetRoundedRectangle(rect, radius))
        using (var brush = new SolidBrush(baseColor))
        {
            e.Graphics.FillPath(brush, path);
        }

        // 悬浮时绘制边框高亮
        if (isHovered)
        {
            using (var path = GetRoundedRectangle(rect, radius))
            using (var pen = new Pen(Color.FromArgb(180, 210, 255), 2f))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }

        // 绘制数字
        string text = EpisodeIndex.ToString();
        TextRenderer.DrawText(e.Graphics, text, Font, rect, Color.White,
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
        }
        base.Dispose(disposing);
    }
}
