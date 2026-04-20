using System;
using System.Drawing;
using System.Windows.Forms;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;

namespace LocalPlayer.Views;

public class PlayerPage : UserControl
{
    private LibVLC? libVLC;
    private MediaPlayer? mediaPlayer;
    private VideoView? videoView;
    private Panel? rightPanel;
    private ListBox? episodeList;
    private Button? backButton;
    
    private string[] videoFiles = Array.Empty<string>();
    private string currentFolderPath = "";

    public event EventHandler? BackRequested;

    public PlayerPage()
    {
        this.BackColor = Color.FromArgb(20, 20, 20);
        this.Dock = DockStyle.Fill;
        
        SetupUI();
        SetupVLC();
    }

    private void SetupUI()
    {
        // 左侧视频区域 (70%)
        videoView = new VideoView
        {
            Dock = DockStyle.None,
            BackColor = Color.Black
        };
        
        // 右侧选集面板 (30%)
        rightPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = (int)(this.Parent?.Width * 0.3 ?? 300),
            BackColor = Color.FromArgb(30, 30, 30)
        };
        
        // 返回按钮
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
        backButton.Click += (s, e) => BackRequested?.Invoke(this, EventArgs.Empty);
        
        // 选集标题
        Label episodeTitle = new Label
        {
            Text = "选集",
            Font = new Font("微软雅黑", 14, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(10, 55),
            Size = new Size(200, 30)
        };
        
        // 选集列表
        episodeList = new ListBox
        {
            Location = new Point(10, 90),
            Size = new Size(rightPanel.Width - 20, 400),
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White,
            Font = new Font("微软雅黑", 11),
            BorderStyle = BorderStyle.None,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        episodeList.SelectedIndexChanged += EpisodeList_SelectedIndexChanged;
        
        rightPanel.Controls.Add(backButton);
        rightPanel.Controls.Add(episodeTitle);
        rightPanel.Controls.Add(episodeList);
        
        this.Controls.Add(videoView);
        this.Controls.Add(rightPanel);
        
        this.Resize += PlayerPage_Resize;
    }

    private void SetupVLC()
    {
        libVLC = new LibVLC();
        mediaPlayer = new MediaPlayer(libVLC);
        
        if (videoView != null)
        {
            videoView.MediaPlayer = mediaPlayer;
        }
    }

    private void PlayerPage_Resize(object? sender, EventArgs e)
    {
        if (videoView != null && rightPanel != null)
        {
            videoView.Size = new Size(this.ClientSize.Width - rightPanel.Width, this.ClientSize.Height);
            videoView.Location = new Point(0, 0);
            
            rightPanel.Height = this.ClientSize.Height;
            episodeList!.Height = this.ClientSize.Height - 120;
        }
    }

    public void LoadFolder(string folderPath, string folderName)
    {
        currentFolderPath = folderPath;
        
        // 扫描视频文件
        videoFiles = Services.VideoScanner.GetVideoFiles(folderPath);
        
        // 填充选集列表
        episodeList!.Items.Clear();
        for (int i = 0; i < videoFiles.Length; i++)
        {
            string fileName = Path.GetFileNameWithoutExtension(videoFiles[i]);
            episodeList.Items.Add($"{i + 1:00}. {fileName}");
        }
        
        // 自动播放第一集
        if (videoFiles.Length > 0)
        {
            PlayVideo(videoFiles[0]);
            episodeList.SelectedIndex = 0;
        }
    }

    private void PlayVideo(string filePath)
    {
        if (mediaPlayer == null || libVLC == null) return;
        
        var media = new Media(libVLC, filePath);
        mediaPlayer.Play(media);
    }

    private void EpisodeList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (episodeList!.SelectedIndex >= 0 && episodeList.SelectedIndex < videoFiles.Length)
        {
            PlayVideo(videoFiles[episodeList.SelectedIndex]);
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        mediaPlayer?.Stop();
        mediaPlayer?.Dispose();
        libVLC?.Dispose();
        base.OnHandleDestroyed(e);
    }
}