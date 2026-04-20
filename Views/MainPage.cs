using LocalPlayer.Controls;
using LocalPlayer.Services;

namespace LocalPlayer.Views;

public partial class MainPage : UserControl
{
    private FlowLayoutPanel? cardPanel;
    private Button? addButton;
    private Panel? bottomPanel;
    private SettingsService settingsService;

    public MainPage()
    {
        settingsService = new SettingsService();
        SetupUI();
        LoadSavedFolders();
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

    private void LoadSavedFolders()
    {
        var folders = settingsService.GetFolders();
        foreach (var folder in folders)
        {
            if (Directory.Exists(folder.Path))
            {
                AddFolderCard(folder.Name, folder.Path);
            }
        }
    }

    private void AddFolderCard(string folderName, string folderPath)
    {
        var card = new FolderCard
        {
            FolderName = folderName,
            FolderPath = folderPath,
            VideoCount = 0,  // TODO: 扫描真实数量
            ProgressPercent = 0
        };
        
        card.CardClick += (s, ev) =>
        {
            MessageBox.Show($"打开文件夹: {card.FolderName}\n路径: {card.FolderPath}");
            // TODO: 切换到播放页面
        };

        // 右键菜单：删除
        var contextMenu = new ContextMenuStrip();
        contextMenu.Renderer = new DarkMenuRenderer();  // 使用深色渲染器
        contextMenu.BackColor = Color.FromArgb(40, 40, 40);
        contextMenu.ForeColor = Color.White;

        var deleteItem = new ToolStripMenuItem("删除")
        {
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White
        };
        deleteItem.Click += (s, ev) =>
        {
            if (MessageBox.Show($"确定要移除 \"{folderName}\" 吗？", "确认", 
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                settingsService.RemoveFolder(folderPath);
                cardPanel?.Controls.Remove(card);
                card.Dispose();
            }
        };
        contextMenu.Items.Add(deleteItem);
        card.ContextMenuStrip = contextMenu;
        
        cardPanel?.Controls.Add(card);
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
                
                // 检查是否已存在
                foreach (FolderCard card in cardPanel!.Controls)
                {
                    if (card.FolderPath == folderPath)
                    {
                        MessageBox.Show("该文件夹已添加", "提示");
                        return;
                    }
                }
                
                // 保存到设置
                settingsService.AddFolder(folderPath, folderName);
                
                // 添加卡片
                AddFolderCard(folderName, folderPath);
            }
        }
    }
}