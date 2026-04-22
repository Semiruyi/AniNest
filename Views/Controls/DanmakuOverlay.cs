using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using LocalPlayer.Models;

namespace LocalPlayer.Views.Controls;

public class DanmakuOverlay : Form
{
    private readonly List<LiveDanmaku> liveDanmaku = new();
    private readonly List<DanmakuItem> allDanmaku = new();
    private readonly System.Windows.Forms.Timer animTimer;
    private readonly Random random = new();
    private readonly System.Windows.Forms.Timer syncTimer;

    private double lastVideoTime = -1;
    private int lastFiredIndex = -1;
    private bool enabled = true;

    private const int TrackHeight = 32;
    private const int MarginTop = 10;
    private const int MarginBottom = 80;
    private const int MaxTracks = 15;

    private Control? targetControl;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool DanmakuEnabled
    {
        get => enabled;
        set { enabled = value; if (!value) { liveDanmaku.Clear(); Invalidate(); } }
    }

    public DanmakuOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Opacity = 1.0;
        AllowTransparency = true;
        BackColor = Color.FromArgb(1, 1, 1);
        TransparencyKey = BackColor;
        DoubleBuffered = true;

        animTimer = new System.Windows.Forms.Timer { Interval = 16 };
        animTimer.Tick += AnimTimer_Tick;

        syncTimer = new System.Windows.Forms.Timer { Interval = 100 };
        syncTimer.Tick += SyncTimer_Tick;
    }

    protected override bool ShowWithoutActivation => true;

    public void AttachTo(Control target)
    {
        targetControl = target;
        SyncPosition();
        syncTimer.Start();
    }

    public new void Show()
    {
        base.Show();
        if (targetControl != null)
            SyncPosition();
    }

    private void SyncTimer_Tick(object? sender, EventArgs e)
    {
        SyncPosition();
    }

    private void SyncPosition()
    {
        if (targetControl == null || !targetControl.IsHandleCreated) return;

        try
        {
            var screenRect = targetControl.RectangleToScreen(targetControl.ClientRectangle);
            if (Location != screenRect.Location || Size != screenRect.Size)
            {
                Bounds = screenRect;
            }
        }
        catch { }
    }

    public void Start()
    {
        animTimer.Start();
    }

    public void Stop()
    {
        animTimer.Stop();
        liveDanmaku.Clear();
        Invalidate();
    }

    public void LoadDanmaku(List<DanmakuItem> items)
    {
        allDanmaku.Clear();
        allDanmaku.AddRange(items);
        allDanmaku.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));
        lastFiredIndex = -1;
        lastVideoTime = -1;
    }

    public void Clear()
    {
        allDanmaku.Clear();
        liveDanmaku.Clear();
        lastFiredIndex = -1;
        lastVideoTime = -1;
        Invalidate();
    }

    public void UpdateVideoTime(double timeSeconds)
    {
        if (!enabled || allDanmaku.Count == 0) return;

        bool seeked = Math.Abs(timeSeconds - lastVideoTime) > 2.0;
        if (seeked)
            lastFiredIndex = FindIndexForTime(timeSeconds) - 1;

        lastVideoTime = timeSeconds;

        for (int i = lastFiredIndex + 1; i < allDanmaku.Count; i++)
        {
            var item = allDanmaku[i];
            if (item.TimeSeconds > timeSeconds) break;

            SpawnDanmaku(item);
            lastFiredIndex = i;
        }
    }

    private int FindIndexForTime(double timeSeconds)
    {
        int lo = 0, hi = allDanmaku.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (allDanmaku[mid].TimeSeconds < timeSeconds) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    private void SpawnDanmaku(DanmakuItem item)
    {
        var font = new Font("微软雅黑", item.FontSize, FontStyle.Bold);
        int textWidth = TextRenderer.MeasureText(item.Text, font).Width + 20;

        int trackCount = Math.Max(1, (Height - MarginTop - MarginBottom) / TrackHeight);
        trackCount = Math.Min(trackCount, MaxTracks);

        int bestTrack = -1;
        int minEndX = int.MaxValue;
        for (int t = 0; t < trackCount; t++)
        {
            int trackEndX = GetTrackRightEdge(t);
            if (trackEndX < minEndX)
            {
                minEndX = trackEndX;
                bestTrack = t;
            }
        }

        if (bestTrack < 0) bestTrack = random.Next(trackCount);
        if (minEndX > Width * 0.85)
        {
            int t = random.Next(trackCount);
            if (GetTrackRightEdge(t) <= Width * 0.85) bestTrack = t;
        }

        float speed = item.Speed + random.Next(-15, 15);
        speed = Math.Max(60, speed);

        liveDanmaku.Add(new LiveDanmaku
        {
            Text = item.Text,
            Color = item.GetColor(),
            Font = font,
            X = Width + random.Next(0, 50),
            Y = MarginTop + bestTrack * TrackHeight,
            Speed = speed,
            TextWidth = textWidth
        });
    }

    private int GetTrackRightEdge(int track)
    {
        int edge = 0;
        int y = MarginTop + track * TrackHeight;
        foreach (var d in liveDanmaku)
        {
            if (Math.Abs(d.Y - y) < TrackHeight / 2)
                edge = Math.Max(edge, (int)d.X + d.TextWidth);
        }
        return edge;
    }

    private void AnimTimer_Tick(object? sender, EventArgs e)
    {
        if (!enabled) return;

        float dt = animTimer.Interval / 1000f;
        for (int i = liveDanmaku.Count - 1; i >= 0; i--)
        {
            liveDanmaku[i].X -= liveDanmaku[i].Speed * dt;
            if (liveDanmaku[i].X + liveDanmaku[i].TextWidth < 0)
                liveDanmaku[i].Dead = true;
        }
        liveDanmaku.RemoveAll(d => d.Dead);
        Invalidate();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00080000; // WS_EX_LAYERED
            cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT (click-through)
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x0084;
        const int HTTRANSPARENT = -1;

        if (m.Msg == WM_NCHITTEST)
        {
            m.Result = (IntPtr)HTTRANSPARENT;
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!enabled) return;

        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        foreach (var d in liveDanmaku)
        {
            if (d.Dead) continue;

            using var outlineBrush = new SolidBrush(Color.FromArgb(180, Color.Black));
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    g.DrawString(d.Text, d.Font, outlineBrush, d.X + dx, d.Y + dy);
                }
            }

            using var textBrush = new SolidBrush(d.Color);
            g.DrawString(d.Text, d.Font, textBrush, d.X, d.Y);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            animTimer.Stop();
            animTimer.Dispose();
            syncTimer.Stop();
            syncTimer.Dispose();
            foreach (var d in liveDanmaku) d.Font.Dispose();
            liveDanmaku.Clear();
        }
        base.Dispose(disposing);
    }

    private class LiveDanmaku
    {
        public string Text = "";
        public Color Color;
        public Font Font = null!;
        public float X;
        public float Y;
        public float Speed;
        public int TextWidth;
        public bool Dead;
    }
}
