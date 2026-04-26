using System;
using System.Drawing;
using System.Windows.Forms;
using LocalPlayer.Services;

namespace LocalPlayer.Views.Controls;

public class PlaylistPanel : UserControl
{
    private Panel? panel;
    private ListBox? episodeList;
    private Button? backButton;

    private string[] videoFiles = Array.Empty<string>();
    private int lastSelectedIndex = -1;

    public event EventHandler? BackClicked;
    public event EventHandler<string>? EpisodeChanged;

    public PlaylistPanel()
    {
        this.Dock = DockStyle.Right;
        this.Width = 300;
        this.BackColor = Color.FromArgb(30, 30, 30);

        InitializeComponents();
    }

    private void InitializeComponents()
    {
        panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 30, 30)
        };

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
        backButton.Click += (s, e) => BackClicked?.Invoke(this, EventArgs.Empty);

        episodeList = new ListBox
        {
            Location = new Point(10, 60),
            Size = new Size(this.Width - 20, 400),
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White,
            Font = new Font("微软雅黑", 11),
            BorderStyle = BorderStyle.None,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };
        episodeList.SelectedIndexChanged += EpisodeList_SelectedIndexChanged;

        panel.Controls.Add(backButton);
        panel.Controls.Add(episodeList);
        this.Controls.Add(panel);

        this.Resize += PlaylistPanel_Resize;
    }

    private void PlaylistPanel_Resize(object? sender, EventArgs e)
    {
        if (episodeList != null)
        {
            episodeList.Height = this.Height - 120;
        }
    }

    public void LoadFolder(string folderPath)
    {
        Console.WriteLine($"[PlaylistPanel] 加载文件夹: {folderPath}");

        videoFiles = VideoScanner.GetVideoFiles(folderPath);
        Console.WriteLine($"[PlaylistPanel] 找到 {videoFiles.Length} 个视频文件");

        episodeList!.Items.Clear();
        for (int i = 0; i < videoFiles.Length; i++)
        {
            string fileName = Path.GetFileNameWithoutExtension(videoFiles[i]);
            episodeList.Items.Add($"{i + 1:00}. {fileName}");
        }

        lastSelectedIndex = -1;

        if (videoFiles.Length > 0)
        {
            episodeList.SelectedIndex = 0;
        }
    }

    public string? FirstEpisodePath => videoFiles.Length > 0 ? videoFiles[0] : null;

    public void PlayNext()
    {
        if (episodeList == null || episodeList.Items.Count == 0) return;

        int nextIndex = episodeList.SelectedIndex + 1;
        if (nextIndex < episodeList.Items.Count)
        {
            episodeList.SelectedIndex = nextIndex;
        }
    }

    public void PlayPrevious()
    {
        if (episodeList == null || episodeList.Items.Count == 0) return;

        int prevIndex = episodeList.SelectedIndex - 1;
        if (prevIndex >= 0)
        {
            episodeList.SelectedIndex = prevIndex;
        }
    }

    public void ToggleVisibility()
    {
        this.Visible = !this.Visible;
    }

    private void EpisodeList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (episodeList == null) return;

        int currentIndex = episodeList.SelectedIndex;

        if (currentIndex == lastSelectedIndex)
        {
            Console.WriteLine($"[PlaylistPanel] 索引未变化，仍为第 {currentIndex + 1} 集");
            return;
        }

        Console.WriteLine($"[PlaylistPanel] 集数变化: 第 {lastSelectedIndex + 1} 集 -> 第 {currentIndex + 1} 集");

        if (currentIndex >= 0 && currentIndex < videoFiles.Length)
        {
            string fileName = Path.GetFileName(videoFiles[currentIndex]);
            Console.WriteLine($"[PlaylistPanel] 切换到第 {currentIndex + 1} 集: {fileName}");
            EpisodeChanged?.Invoke(this, videoFiles[currentIndex]);
            lastSelectedIndex = currentIndex;
        }
        else
        {
            Console.WriteLine($"[PlaylistPanel] 无效的索引: {currentIndex}");
        }
    }
}
