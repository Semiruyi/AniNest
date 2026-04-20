using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LocalPlayer.Controls;

public class FolderCard : UserControl
{
    // ========== 尺寸配置 ==========
    private const float ScaleFactor = 1.5f;           // 缩放倍数，改这里全局生效
    private const int BaseCoverWidth = 150;
    private const int BaseCoverHeight = 225;          // 2:3 比例
    private const int BaseCardWidth = 180;
    private const int BaseCardHeight = 315;
    private const int BaseMargin = 12;
    private const int BaseRadius = 12;
    
    // 实际使用的尺寸
    private static readonly int CoverWidth = (int)(BaseCoverWidth * ScaleFactor);
    private static readonly int CoverHeight = (int)(BaseCoverHeight * ScaleFactor);
    private static readonly int CardWidth = (int)(BaseCardWidth * ScaleFactor);
    private static readonly int CardHeight = (int)(BaseCardHeight * ScaleFactor);
    private static readonly int CardMargin = (int)(BaseMargin * ScaleFactor);
    private static readonly int CardRadius = (int)(BaseRadius * ScaleFactor);
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
            {
                int contentWidth = CardWidth - CardMargin * 2;
                progressBar.Width = (int)(contentWidth * value / 100);
            }
        }
    }

    // 点击事件
    public event EventHandler? CardClick;

    public FolderCard()
    {
        this.Size = new Size(CardWidth, CardHeight);
        this.BackColor = Color.FromArgb(35, 35, 35);
        this.Cursor = Cursors.Hand;
        this.Margin = new Padding(CardMargin);
        
        SetupUI();
        AttachHoverEffect();
        AttachClickEvent();
    }

    private void SetupUI()
    {
        int contentWidth = CardWidth - CardMargin * 2;
        
        // 封面图片
        coverBox = new PictureBox
        {
            Size = new Size(CoverWidth, CoverHeight),
            Location = new Point((CardWidth - CoverWidth) / 2, CardMargin),
            BackColor = Color.FromArgb(45, 45, 45),
            SizeMode = PictureBoxSizeMode.Zoom
        };
        // 给封面加个圆角效果（通过绘制）
        coverBox.Paint += CoverBox_Paint;
        
        int nameFontSize = (int)(9 * ScaleFactor);
        int infoFontSize = (int)(7 * ScaleFactor);
        
        int yOffset = CoverHeight + CardMargin + 8;
        
        // 文件夹名称（支持换行，最多两行）
        nameLabel = new Label
        {
            Font = new Font("微软雅黑", nameFontSize, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Location = new Point(CardMargin, yOffset),
            Size = new Size(contentWidth, (int)(36 * ScaleFactor)),
            TextAlign = ContentAlignment.TopLeft,
            AutoSize = false,
            Text = "文件夹名称"
        };
        
        yOffset += (int)(38 * ScaleFactor);
        
        // 视频数量
        infoLabel = new Label
        {
            Font = new Font("微软雅黑", infoFontSize),
            ForeColor = Color.FromArgb(160, 160, 160),
            BackColor = Color.Transparent,
            Location = new Point(CardMargin, yOffset),
            Size = new Size(contentWidth, (int)(16 * ScaleFactor)),
            Text = "0 个视频"
        };
        
        yOffset += (int)(20 * ScaleFactor);
        
        // 进度条背景
        Panel progressBg = new Panel
        {
            Size = new Size(contentWidth, (int)(3 * ScaleFactor)),
            Location = new Point(CardMargin, yOffset),
            BackColor = Color.FromArgb(60, 60, 60)
        };
        
        // 进度条
        progressBar = new Panel
        {
            Size = new Size(0, (int)(3 * ScaleFactor)),
            Location = new Point(0, 0),
            BackColor = Color.FromArgb(0, 122, 204)
        };
        progressBg.Controls.Add(progressBar);
        
        this.Controls.Add(coverBox);
        this.Controls.Add(nameLabel);
        this.Controls.Add(infoLabel);
        this.Controls.Add(progressBg);
    }

    private void CoverBox_Paint(object? sender, PaintEventArgs e)
    {
        if (coverBox?.Image == null) return;
        
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int radius = (int)(8 * ScaleFactor);
        
        using (var path = GetRoundedRectangle(coverBox.ClientRectangle, radius))
        {
            coverBox.Region = new Region(path);
        }
    }

    private void AttachHoverEffect()
    {
        Color originalColor = this.BackColor;
        Color hoverColor = Color.FromArgb(50, 50, 50);
        
        this.MouseEnter += (s, e) =>
        {
            this.BackColor = hoverColor;
        };
        
        this.MouseLeave += (s, e) =>
        {
            this.BackColor = originalColor;
        };
    }

    private void AttachClickEvent()
    {
        this.Click += (s, e) => CardClick?.Invoke(this, e);
        
        // 让子控件点击也触发卡片点击
        foreach (Control ctrl in this.Controls)
        {
            ctrl.Click += (s, e) => CardClick?.Invoke(this, e);
        }
    }

    private void AdjustNameFontSize()
    {
        if (nameLabel == null || string.IsNullOrEmpty(nameLabel.Text)) return;
        
        int maxFontSize = (int)(9 * ScaleFactor);
        int minFontSize = (int)(7 * ScaleFactor);
        
        // 先设置最大字体
        nameLabel.Font = new Font("微软雅黑", maxFontSize, FontStyle.Bold);
        
        // 测量文本是否超出
        using (Graphics g = nameLabel.CreateGraphics())
        {
            SizeF textSize = g.MeasureString(nameLabel.Text, nameLabel.Font, nameLabel.Width);
            
            if (textSize.Height <= nameLabel.Height && textSize.Width <= nameLabel.Width * 2)
                return;  // 当前字体合适
            
            // 缩小字体
            for (int size = maxFontSize - 1; size >= minFontSize; size--)
            {
                using (Font font = new Font("微软雅黑", size, FontStyle.Bold))
                {
                    textSize = g.MeasureString(nameLabel.Text, font, nameLabel.Width);
                    if (textSize.Height <= nameLabel.Height)
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
        {
            coverBox.Image = image;
        }
    }

    public void SetProgress(double percent)
    {
        ProgressPercent = percent;
    }

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
}