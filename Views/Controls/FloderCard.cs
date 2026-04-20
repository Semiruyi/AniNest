using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LocalPlayer.Controls;

public class FolderCard : UserControl
{
    // ========== 尺寸配置 ==========
private const float ScaleFactor = 0.7f;
private const int BaseCoverWidth = 600;
private const int BaseCoverHeight = 750;

// 各区域高度（相对于封面高度）
private const float TitleHeightPercent = 0.15f;      // 标题区高度 = 封面高 15%
private const float InfoHeightPercent = 0.10f;       // 信息区高度 = 封面高 10%
private const float ProgressHeightPercent = 0.04f;

// 内容边距（相对于封面宽度）
private const float ContentMarginPercent = 0.05f;    // 左右边距 5%

private const int BaseRadius = 20;

// 实际尺寸
private int CoverWidth => (int)(BaseCoverWidth * ScaleFactor);
private int CoverHeight => (int)(BaseCoverHeight * ScaleFactor);
private int CardWidth => CoverWidth;
private int ContentMargin => (int)(CoverWidth * ContentMarginPercent);
private int ContentWidth => CoverWidth - ContentMargin * 2;

private int TitleAreaHeight => (int)(CoverHeight * TitleHeightPercent);
private int InfoAreaHeight => (int)(CoverHeight * InfoHeightPercent);
private int ProgressAreaHeight => (int)(CoverHeight * ProgressHeightPercent);

private int CardHeight => CoverHeight + TitleAreaHeight + InfoAreaHeight + ProgressAreaHeight;
private int CardRadius => (int)(BaseRadius * ScaleFactor);

// 各区域 Y 坐标
private int TitleY => CoverHeight;
private int ProgressY => CoverHeight + TitleAreaHeight;
private int InfoY => CoverHeight + TitleAreaHeight + ProgressAreaHeight;
// ===============================
    
    private Label? nameLabel;
    private Label? infoLabel;
    private PictureBox? coverBox;
    private Panel? progressBar;
    private string folderPath = "";
    private int videoCount = 0;
    private double progressPercent = 0;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string FolderName 
    { 
        get => nameLabel?.Text ?? ""; 
        set
        {
            if (nameLabel != null)
            {
                nameLabel.Text = value;
                AdjustNameFontSize();
            }
        }
    }
    
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string FolderPath 
    { 
        get => folderPath; 
        set => folderPath = value;
    }
    
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int VideoCount 
    { 
        get => videoCount;
        set
        {
            videoCount = value;
            if (infoLabel != null)
                infoLabel.Text = value == 0 ? "暂无视频" : $"{value} 个视频";
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double ProgressPercent
    {
        get => progressPercent;
        set
        {
            progressPercent = value;
            if (progressBar != null)
                progressBar.Width = (int)(ContentWidth * value / 100);
        }
    }

    public event EventHandler? CardClick;

    public FolderCard()
    {
        this.Size = new Size(CardWidth, CardHeight);
        this.BackColor = Color.FromArgb(35, 35, 35);
        this.Cursor = Cursors.Hand;
        this.Margin = new Padding(15);
        
        SetupUI();
        AttachHoverEffect();
        AttachClickEvent();
    }

    private void SetupUI()
    {
        // 封面图片（无边距）
        coverBox = new PictureBox
        {
            Size = new Size(CoverWidth, CoverHeight),
            Location = new Point(0, 0),
            BackColor = Color.FromArgb(45, 45, 45),
            SizeMode = PictureBoxSizeMode.StretchImage  // 填满，不留黑边
        };
        coverBox.Paint += CoverBox_Paint;
        
        // 标题区域（紧贴封面下方，高度 = 封面高 20%）
        nameLabel = new Label
        {
            Font = new Font("微软雅黑", 27, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Location = new Point(ContentMargin, TitleY),
            Size = new Size(ContentWidth, TitleAreaHeight),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Text = "文件夹名称"
        };
        
        // 进度条区域
        Panel progressBg = new Panel
        {
            Size = new Size(ContentWidth, ProgressAreaHeight),
            Location = new Point(ContentMargin, ProgressY),
            BackColor = Color.Transparent
        };

        // 进度条背景轨道（顶部居中）
        int barHeight = 8;
        int barWidth = ContentWidth;  // 或者可以设置更窄，比如 (int)(ContentWidth * 0.8f)

        Panel progressTrack = new Panel
        {
            Size = new Size(barWidth, barHeight),
            Location = new Point((ContentWidth - barWidth) / 2, 0),  // 水平居中，顶部对齐
            BackColor = Color.FromArgb(80, 80, 80)
        };
        progressTrack.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = GetRoundedRectangle(progressTrack.ClientRectangle, barHeight / 2))
            using (var brush = new SolidBrush(progressTrack.BackColor))
            {
                e.Graphics.FillPath(brush, path);
            }
        };

        // 进度条（圆角，淡蓝色）
        progressBar = new Panel
        {
            Size = new Size(0, barHeight),
            Location = new Point(0, 0),
            BackColor = Color.FromArgb(100, 180, 255)  // 淡蓝色
        };
        progressBar.Paint += (s, e) =>
        {
            if (progressBar.Width == 0) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = GetRoundedRectangle(progressBar.ClientRectangle, barHeight / 2))
            using (var brush = new SolidBrush(progressBar.BackColor))
            {
                e.Graphics.FillPath(brush, path);
            }
        };

        progressTrack.Controls.Add(progressBar);
        progressBg.Controls.Add(progressTrack);

        // 视频数量区域（高度 = 封面高 10%）
        infoLabel = new Label
        {
            Font = new Font("微软雅黑", 12),
            ForeColor = Color.FromArgb(160, 160, 160),
            BackColor = Color.Transparent,
            Location = new Point(ContentMargin, InfoY),
            Size = new Size(ContentWidth, InfoAreaHeight),
            TextAlign = ContentAlignment.TopCenter,
            Text = "0 个视频"
        };
        
        this.Controls.Add(coverBox);
        this.Controls.Add(nameLabel);
        this.Controls.Add(infoLabel);
        this.Controls.Add(progressBg);
    }
    private void CoverBox_Paint(object? sender, PaintEventArgs e)
    {
        if (coverBox?.Image == null) return;
        
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int radius = CardRadius;
        
        using (var path = GetRoundedRectangle(new Rectangle(0, 0, CoverWidth, CoverHeight), radius))
        {
            coverBox.Region = new Region(path);
        }
    }

    private void AttachHoverEffect()
    {
        Color originalColor = this.BackColor;
        Color hoverColor = Color.FromArgb(50, 50, 50);
        
        this.MouseEnter += (s, e) => this.BackColor = hoverColor;
        this.MouseLeave += (s, e) =>
        {
            // 检查鼠标是否真的离开了整个卡片区域
            if (!this.ClientRectangle.Contains(this.PointToClient(Cursor.Position)))
            {
                this.BackColor = originalColor;
            }
        };
        
        AttachHoverToChildren(this, hoverColor, originalColor);
    }

    private void AttachHoverToChildren(Control parent, Color hoverColor, Color originalColor)
    {
        foreach (Control child in parent.Controls)
        {
            child.MouseEnter += (s, e) => this.BackColor = hoverColor;
            child.MouseLeave += (s, e) =>
            {
                // 关键修复：延迟检查，等鼠标位置更新
                this.BeginInvoke(new Action(() =>
                {
                    if (!this.ClientRectangle.Contains(this.PointToClient(Cursor.Position)))
                    {
                        this.BackColor = originalColor;
                    }
                }));
            };
            
            if (child.HasChildren)
                AttachHoverToChildren(child, hoverColor, originalColor);
        }
    }

    private void AttachClickEvent()
    {
        this.Click += (s, e) => CardClick?.Invoke(this, e);
        foreach (Control ctrl in this.Controls)
            ctrl.Click += (s, e) => CardClick?.Invoke(this, e);
    }

    private void AdjustNameFontSize()
    {
        if (nameLabel == null || string.IsNullOrEmpty(nameLabel.Text)) return;
        
        int maxSize = (int)(25 * ScaleFactor);
        int minSize = (int)(12 * ScaleFactor);
        
        nameLabel.Font = new Font("微软雅黑", maxSize, FontStyle.Bold);
        
        using (Graphics g = nameLabel.CreateGraphics())
        {
            SizeF textSize = g.MeasureString(nameLabel.Text, nameLabel.Font, nameLabel.Width);
            if (textSize.Width <= nameLabel.Width * 1.2f) return;
            
            for (int size = maxSize - 1; size >= minSize; size--)
            {
                using (Font font = new Font("微软雅黑", size, FontStyle.Bold))
                {
                    textSize = g.MeasureString(nameLabel.Text, font, nameLabel.Width);
                    if (textSize.Width <= nameLabel.Width)
                    {
                        nameLabel.Font = new Font("微软雅黑", size, FontStyle.Bold);
                        break;
                    }
                }
            }
        }
    }

    public void SetCoverImage(Image? image)
    {
        if (image != null && coverBox != null)
            coverBox.Image = image;
    }

    public void SetProgress(double percent) => ProgressPercent = percent;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        
        using (var path = GetRoundedRectangle(this.ClientRectangle, CardRadius))
        using (var brush = new SolidBrush(this.BackColor))
        {
            e.Graphics.FillPath(brush, path);
        }
    }

    private GraphicsPath GetRoundedRectangle(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int r = Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2);
        
        if (r <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }
        
        path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
        path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
        path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        return path;
    }


    public void CheckAndResetHover()
    {
        Point mousePos = this.PointToClient(Cursor.Position);
        if (!this.ClientRectangle.Contains(mousePos))
        {
            this.BackColor = Color.FromArgb(35, 35, 35);
        }
    }
}