using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalPlayer.Model;

namespace LocalPlayer.ViewModel;

public partial class MainPageViewModel : ObservableObject
{
    public static void Log(string message) => AppLog.Info(nameof(MainPageViewModel), message);

    private readonly ISettingsService _settings;
    private readonly IThumbnailGenerator _thumbnailGenerator;

    public ObservableCollection<FolderListItem> FolderItems { get; } = new();

    [ObservableProperty]
    private string _folderCountText = "0 个文件夹";

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private string _thumbnailProgressText = "";

    [ObservableProperty]
    private bool _isThumbnailProgressVisible;

    public event Action<object, string, string>? FolderSelected;

    public MainPageViewModel(ISettingsService settings, IThumbnailGenerator thumbnailGenerator)
    {
        _settings = settings;
        _thumbnailGenerator = thumbnailGenerator;

        FolderItems.CollectionChanged += (_, _) => UpdateToolbarState();

        _thumbnailGenerator.ProgressChanged += (_, args) =>
            Application.Current.Dispatcher.BeginInvoke(
                () => UpdateThumbnailProgress(args.Ready, args.Total));
    }

    public List<FolderListItem> LoadFoldersData()
    {
        var items = new List<FolderListItem>();
        var folders = _settings.GetFolders();

        foreach (var folder in folders)
        {
            if (Directory.Exists(folder.Path))
            {
                var (count, coverPath) = VideoScanner.ScanFolder(folder.Path);
                items.Add(new FolderListItem(folder.Name, folder.Path, count, coverPath));
            }
            else
            {
                _settings.RemoveFolder(folder.Path);
                _thumbnailGenerator.DeleteForFolder(folder.Path);
            }
        }
        return items;
    }

    public void EnqueueAllFolders(List<FolderListItem> items)
    {
        foreach (var item in items)
            EnqueueFolderForThumbnails(item);
    }

    public void EnqueueFolderForThumbnails(FolderListItem item)
    {
        int cardOrder = 0;
        var folders = _settings.GetFolders();
        var folderInfo = folders.FirstOrDefault(f => f.Path == item.Path);
        if (folderInfo != null)
            cardOrder = folderInfo.OrderIndex;

        var folderProgress = _settings.GetFolderProgress(item.Path);
        string? lastPlayed = folderProgress?.LastVideoPath;

        var playedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var videoFiles = VideoScanner.GetVideoFiles(item.Path);
        foreach (var vf in videoFiles)
        {
            if (_settings.IsVideoPlayed(vf))
                playedPaths.Add(vf);
        }

        _thumbnailGenerator.EnqueueFolder(item.Path, cardOrder, lastPlayed, playedPaths);
    }

    public void ReorderFolders(List<string> orderedPaths)
        => _settings.ReorderFolders(orderedPaths);

    [RelayCommand]
    private void AddFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择包含视频的文件夹"
        };

        if (dialog.ShowDialog() != true) return;

        string path = dialog.FolderName;
        string name = Path.GetFileName(path);

        if (FolderItems.Any(i => i.Path == path))
        {
            MessageBox.Show("该文件夹已添加", "提示");
            return;
        }

        var (count, coverPath) = VideoScanner.ScanFolder(path);
        if (count == 0)
        {
            MessageBox.Show("该文件夹内没有视频文件", "提示");
            return;
        }

        _settings.AddFolder(path, name);
        var newItem = new FolderListItem(name, path, count, coverPath);
        FolderItems.Add(newItem);
    }

    public void DeleteFolder(FolderListItem item)
    {
        _settings.RemoveFolder(item.Path);
        _thumbnailGenerator.DeleteForFolder(item.Path);
        FolderItems.Remove(item);
    }

    [RelayCommand]
    private void Settings()
    {
        int currentDays = _settings.GetThumbnailExpiryDays();
        string input = Microsoft.VisualBasic.Interaction.InputBox(
            "缩略图过期天数（0=永不过期）：",
            "缩略图设置",
            currentDays.ToString());

        if (int.TryParse(input, out int days) && days >= 0 && days <= 365)
        {
            _settings.SetThumbnailExpiryDays(days);
            if (days == 0)
                MessageBox.Show("已设置：缩略图永不过期", "提示");
            else
                MessageBox.Show($"已设置：缩略图 {days} 天后过期", "提示");
        }
    }

    public bool TrySelectFolder(string path, out string name)
    {
        name = "";
        var videos = VideoScanner.GetVideoFiles(path);
        if (videos.Length == 0)
        {
            MessageBox.Show("文件夹内没有视频文件", "提示");
            return false;
        }
        name = Path.GetFileName(path);
        FolderSelected?.Invoke(this, path, name);
        return true;
    }

    private void UpdateToolbarState()
    {
        int count = FolderItems.Count;
        FolderCountText = $"{count} 个文件夹";
        IsEmpty = count == 0;
    }

    private void UpdateThumbnailProgress(int ready, int total)
    {
        if (total > 0 && ready < total)
        {
            ThumbnailProgressText = $"缩略图 {ready}/{total}";
            IsThumbnailProgressVisible = true;
        }
        else
        {
            IsThumbnailProgressVisible = false;
        }
    }
}
