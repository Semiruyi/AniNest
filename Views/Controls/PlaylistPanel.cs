using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using LocalPlayer.Services;

namespace LocalPlayer.Views.Controls;

public class PlaylistPanel : UserControl
{
    private Panel? panel;
    private Panel? buttonsPanel;
    private Button? backButton;
    private readonly List<EpisodeButton> episodeButtons = new();

    private string[] videoFiles = Array.Empty<string>();
    private int lastSelectedIndex = -1;
    private bool suspendEpisodeEvent = false;
    private readonly SettingsService settingsService = new();

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

        buttonsPanel = new Panel
        {
            Location = new Point(0, 55),
            Size = new Size(this.Width, this.Height - 55),
            BackColor = Color.FromArgb(30, 30, 30),
            AutoScroll = true
        };

        panel.Controls.Add(backButton);
        panel.Controls.Add(buttonsPanel);
        this.Controls.Add(panel);

        this.Resize += PlaylistPanel_Resize;
    }

    private void PlaylistPanel_Resize(object? sender, EventArgs e)
    {
        if (buttonsPanel != null)
        {
            buttonsPanel.Size = new Size(this.Width, this.Height - 55);
        }
    }

    public void LoadFolder(string folderPath)
    {
        Console.WriteLine($"[PlaylistPanel] 加载文件夹: {folderPath}");

        videoFiles = VideoScanner.GetVideoFiles(folderPath);
        Console.WriteLine($"[PlaylistPanel] 找到 {videoFiles.Length} 个视频文件");

        // 清除旧按钮
        foreach (var btn in episodeButtons)
        {
            btn.Dispose();
        }
        episodeButtons.Clear();

        const int buttonSize = 60;
        const int gap = 8;
        const int padding = 10;
        const int buttonsPerRow = 4;

        for (int i = 0; i < videoFiles.Length; i++)
        {
            int row = i / buttonsPerRow;
            int col = i % buttonsPerRow;
            int x = padding + col * (buttonSize + gap);
            int y = padding + row * (buttonSize + gap);

            var btn = new EpisodeButton
            {
                EpisodeIndex = i + 1,
                FilePath = videoFiles[i],
                PlayState = settingsService.IsVideoPlayed(videoFiles[i])
                    ? EpisodePlayState.Played
                    : EpisodePlayState.Unplayed
            };
            btn.SetNormalBounds(new Rectangle(x, y, buttonSize, buttonSize));
            btn.MouseClick += EpisodeButton_Click;

            buttonsPanel?.Controls.Add(btn);
            episodeButtons.Add(btn);
        }

        lastSelectedIndex = -1;
    }

    private void EpisodeButton_Click(object? sender, MouseEventArgs e)
    {
        if (sender is not EpisodeButton btn || suspendEpisodeEvent) return;

        int index = episodeButtons.IndexOf(btn);
        if (index < 0) return;

        if (index == lastSelectedIndex)
        {
            Console.WriteLine($"[PlaylistPanel] 索引未变化，仍为第 {index + 1} 集");
            return;
        }

        // 重新加载设置，确保获取其他组件（如 PlayerPage）更新后的播放状态
        settingsService.Reload();

        // 把之前正在播放的按钮恢复为对应状态
        if (lastSelectedIndex >= 0 && lastSelectedIndex < episodeButtons.Count)
        {
            var prevBtn = episodeButtons[lastSelectedIndex];
            bool wasPlayed = settingsService.IsVideoPlayed(prevBtn.FilePath);
            prevBtn.PlayState = wasPlayed ? EpisodePlayState.Played : EpisodePlayState.Unplayed;
        }

        // 新按钮设为播放中
        btn.PlayState = EpisodePlayState.Playing;

        Console.WriteLine($"[PlaylistPanel] 集数变化: 第 {lastSelectedIndex + 1} 集 -> 第 {index + 1} 集");
        Console.WriteLine($"[PlaylistPanel] 切换到第 {index + 1} 集: {Path.GetFileName(videoFiles[index])}");

        // 必须先更新 lastSelectedIndex，再触发事件！
        // 因为外部 handler 可能调用 RefreshPlayStatus，它会依赖 lastSelectedIndex
        lastSelectedIndex = index;
        EpisodeChanged?.Invoke(this, videoFiles[index]);
    }

    public void SelectVideo(string? videoPath)
    {
        suspendEpisodeEvent = true;

        // 重置所有按钮状态
        foreach (var btn in episodeButtons)
        {
            bool isPlayed = settingsService.IsVideoPlayed(btn.FilePath);
            btn.PlayState = isPlayed ? EpisodePlayState.Played : EpisodePlayState.Unplayed;
        }

        if (!string.IsNullOrEmpty(videoPath))
        {
            int index = Array.IndexOf(videoFiles, videoPath);
            if (index >= 0 && index < episodeButtons.Count)
            {
                episodeButtons[index].PlayState = EpisodePlayState.Playing;
                lastSelectedIndex = index;
            }
            else if (episodeButtons.Count > 0)
            {
                episodeButtons[0].PlayState = EpisodePlayState.Playing;
                lastSelectedIndex = 0;
            }
        }
        else if (episodeButtons.Count > 0)
        {
            episodeButtons[0].PlayState = EpisodePlayState.Playing;
            lastSelectedIndex = 0;
        }

        suspendEpisodeEvent = false;
    }

    public string? FirstEpisodePath => videoFiles.Length > 0 ? videoFiles[0] : null;

    public string? CurrentEpisodePath
    {
        get
        {
            if (lastSelectedIndex < 0 || lastSelectedIndex >= videoFiles.Length) return null;
            return videoFiles[lastSelectedIndex];
        }
    }

    public void PlayNext()
    {
        if (episodeButtons.Count == 0) return;

        int nextIndex = lastSelectedIndex + 1;
        if (nextIndex < episodeButtons.Count)
        {
            EpisodeButton_Click(episodeButtons[nextIndex], new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
        }
    }

    public void PlayPrevious()
    {
        if (episodeButtons.Count == 0) return;

        int prevIndex = lastSelectedIndex - 1;
        if (prevIndex >= 0)
        {
            EpisodeButton_Click(episodeButtons[prevIndex], new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
        }
    }

    public void ToggleVisibility()
    {
        this.Visible = !this.Visible;
    }

    public void RefreshPlayStatus()
    {
        // 重新加载设置，确保获取 PlayerPage 更新后的播放状态
        settingsService.Reload();

        for (int i = 0; i < episodeButtons.Count; i++)
        {
            var btn = episodeButtons[i];
            if (i == lastSelectedIndex)
            {
                btn.PlayState = EpisodePlayState.Playing;
            }
            else
            {
                bool isPlayed = settingsService.IsVideoPlayed(btn.FilePath);
                btn.PlayState = isPlayed ? EpisodePlayState.Played : EpisodePlayState.Unplayed;
            }
        }
    }
}
