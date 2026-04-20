using LocalPlayer.Controls;

namespace LocalPlayer.Views;

public partial class MainPage : UserControl
{
    private FlowLayoutPanel? cardPanel;
    private Button? addButton;
    private Panel? bottomPanel;

    public MainPage()
    {
        SetupUI();
    }

    private void SetupUI()
    {
        this.BackColor = Color.FromArgb(20, 20, 20);
        this.Dock = DockStyle.Fill;

        cardPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(20, 20, 20, 20),  // 统一边距
            BackColor = Color.FromArgb(20, 20, 20)
        };

        bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 80,
            BackColor = Color.FromArgb(30, 30, 30)
        };

        addButton = new Button
        {
            Text = "➕ 添加文件夹",
            Font = new Font("微软雅黑", 12, FontStyle.Regular),
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Width = 160,
            Height = 40,
            Cursor = Cursors.Hand
        };
        addButton.FlatAppearance.BorderSize = 0;
        addButton.Click += AddButton_Click;

        addButton.Left = (bottomPanel.ClientSize.Width - addButton.Width) / 2;
        addButton.Top = (bottomPanel.ClientSize.Height - addButton.Height) / 2;

        bottomPanel.Controls.Add(addButton);
        bottomPanel.Resize += BottomPanel_Resize;

        this.Controls.Add(cardPanel);
        this.Controls.Add(bottomPanel);
    }

    private void BottomPanel_Resize(object? sender, EventArgs e)
    {
        if (addButton != null && bottomPanel != null)
        {
            addButton.Left = (bottomPanel.ClientSize.Width - addButton.Width) / 2;
            addButton.Top = (bottomPanel.ClientSize.Height - addButton.Height) / 2;
        }
    }

    private void AddButton_Click(object? sender, EventArgs e)
    {
        using (FolderBrowserDialog dialog = new FolderBrowserDialog())
        {
            dialog.Description = "选择包含视频的文件夹";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string folderPath = dialog.SelectedPath;
                string folderName = Path.GetFileName(folderPath);
                
                // 创建卡片
                var card = new FolderCard
                {
                    FolderName = folderName,
                    FolderPath = folderPath,
                    VideoCount = 0  // 暂时写 0，后续扫描真实数量
                };
                
                card.Click += (s, ev) =>
                {
                    MessageBox.Show($"点击了: {folderName}", "提示");
                };
                
                cardPanel?.Controls.Add(card);
            }
        }
    }
}