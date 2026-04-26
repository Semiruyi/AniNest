using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using LocalPlayer.Models;
using LocalPlayer.Services;

namespace LocalPlayer.Views;

public partial class MainPage : System.Windows.Controls.UserControl
{
    private readonly SettingsService settingsService = new();

    public event Action<object, string, string>? FolderSelected;

    public MainPage()
    {
        InitializeComponent();
        Loaded += MainPage_Loaded;
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadFolders();
    }

    private void LoadFolders()
    {
        var folders = settingsService.GetFolders();
        var items = new List<FolderListItem>();

        foreach (var folder in folders)
        {
            if (Directory.Exists(folder.Path))
            {
                int count = VideoScanner.CountVideosInFolder(folder.Path);
                items.Add(new FolderListItem(folder.Name, folder.Path, count));
            }
            else
            {
                settingsService.RemoveFolder(folder.Path);
            }
        }

        FolderList.ItemsSource = items;
    }

    private void FolderCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is string path)
        {
            string name = Path.GetFileName(path);
            var videos = VideoScanner.GetVideoFiles(path);
            if (videos.Length == 0)
            {
                System.Windows.MessageBox.Show("文件夹内没有视频文件", "提示");
                return;
            }
            FolderSelected?.Invoke(this, path, name);
        }
    }

    private void AddFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择包含视频的文件夹"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            string path = dialog.SelectedPath;
            string name = Path.GetFileName(path);

            foreach (FolderListItem item in FolderList.ItemsSource ?? new List<FolderListItem>())
            {
                if (item.Path == path)
                {
                    System.Windows.MessageBox.Show("该文件夹已添加", "提示");
                    return;
                }
            }

            int count = VideoScanner.CountVideosInFolder(path);
            if (count == 0)
            {
                System.Windows.MessageBox.Show("该文件夹内没有视频文件", "提示");
                return;
            }

            settingsService.AddFolder(path, name);
            LoadFolders();
        }
    }
}

public record FolderListItem(string Name, string Path, int VideoCount);
