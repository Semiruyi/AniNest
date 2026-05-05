using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LocalPlayer.Core.Localization;
using LocalPlayer.Core.Messaging;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Paths;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Thumbnails;
using LocalPlayer.Infrastructure.Interop;
using LocalPlayer.Features.Library.Models;

namespace LocalPlayer.Features.Library;

public partial class MainPageViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly ILocalizationService _loc;
    private readonly EventHandler<ThumbnailProgressEventArgs> _thumbnailProgressChangedHandler;
    private bool _dataLoaded;
    private bool _isCleanedUp;

    public event EventHandler? LoadDataCompleted;

    public ObservableCollection<FolderListItem> FolderItems { get; } = new();

    [ObservableProperty]
    private string _folderCountText = "0 涓枃浠跺す";

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private string _thumbnailProgressText = "";

    [ObservableProperty]
    private bool _isThumbnailProgressVisible;

    public MainPageViewModel(
        ISettingsService settings,
        IThumbnailGenerator thumbnailGenerator,
        ILocalizationService loc)
    {
        _settings = settings;
        _thumbnailGenerator = thumbnailGenerator;
        _loc = loc;
        _thumbnailProgressChangedHandler = OnThumbnailProgressChanged;

        FolderItems.CollectionChanged += (_, _) => UpdateToolbarState();

        WeakReferenceMessenger.Default.Register<FolderAddedMessage>(this, (_, m) =>
        {
            var item = new FolderListItem(m.Name, m.Path, m.VideoCount, m.CoverPath)
            {
                VideoCountText = string.Format(_loc["Library.VideoCount"], m.VideoCount)
            };
            FolderItems.Add(item);
        });

        _thumbnailGenerator.ProgressChanged += _thumbnailProgressChangedHandler;
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (_dataLoaded)
        {
            LoadDataCompleted?.Invoke(this, EventArgs.Empty);
            return;
        }

        var loadedItems = await Task.Run(LoadFoldersData);

        FolderItems.Clear();
        foreach (var item in loadedItems)
            FolderItems.Add(item.Item);

        EnqueueAllFolders(loadedItems);
        _dataLoaded = true;
        LoadDataCompleted?.Invoke(this, EventArgs.Empty);
    }

    public List<(FolderListItem Item, string[] VideoFiles)> LoadFoldersData()
    {
        var items = new List<(FolderListItem Item, string[] VideoFiles)>();
        var folders = _settings.GetFolders();

        foreach (var folder in folders)
        {
            if (Directory.Exists(folder.Path))
            {
                var result = VideoScanner.ScanFolder(folder.Path);
                var item = new FolderListItem(folder.Name, folder.Path, result.VideoCount, result.CoverPath)
                {
                    VideoCountText = string.Format(_loc["Library.VideoCount"], result.VideoCount)
                };
                items.Add((item, result.VideoFiles));
            }
            else
            {
                _settings.RemoveFolder(folder.Path);
                _thumbnailGenerator.DeleteForFolder(folder.Path);
            }
        }

        return items;
    }

    public void EnqueueAllFolders(List<(FolderListItem Item, string[] VideoFiles)> items)
    {
        foreach (var (item, videoFiles) in items)
            EnqueueFolderForThumbnails(item, videoFiles);
    }

    public void EnqueueFolderForThumbnails(FolderListItem item, string[] videoFiles)
    {
        int cardOrder = 0;
        var folders = _settings.GetFolders();
        var folderInfo = folders.FirstOrDefault(f => f.Path == item.Path);
        if (folderInfo != null)
            cardOrder = folderInfo.OrderIndex;

        var folderProgress = _settings.GetFolderProgress(item.Path);
        string? lastPlayed = folderProgress?.LastVideoPath;

        var playedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
    private void SelectFolder(FolderListItem? item)
    {
        if (item != null)
            TrySelectFolder(item.Path, out _);
    }

    [RelayCommand]
    private void DeleteFolder(FolderListItem? item)
    {
        if (item == null)
            return;

        _settings.RemoveFolder(item.Path);
        _thumbnailGenerator.DeleteForFolder(item.Path);
        FolderItems.Remove(item);
    }

    [RelayCommand]
    private void Settings()
    {
        int currentDays = _settings.GetThumbnailExpiryDays();
        string input = Microsoft.VisualBasic.Interaction.InputBox(
            _loc["Settings.ThumbnailExpiryPrompt"],
            _loc["Settings.ThumbnailSettings"],
            currentDays.ToString());

        if (int.TryParse(input, out int days) && days >= 0 && days <= 365)
        {
            _settings.SetThumbnailExpiryDays(days);
            if (days == 0)
            {
                MessageBox.Show(_loc["Settings.ThumbnailSetNever"], _loc["Dialog.Info"]);
            }
            else
            {
                MessageBox.Show(string.Format(_loc["Settings.ThumbnailSetDays"], days), _loc["Dialog.Info"]);
            }
        }
    }

    public bool TrySelectFolder(string path, out string name)
    {
        name = "";
        var videos = VideoScanner.GetVideoFiles(path);
        if (videos.Length == 0)
        {
            MessageBox.Show(_loc["Dialog.NoVideosInFolder"], _loc["Dialog.Info"]);
            return false;
        }

        name = Path.GetFileName(path);
        WeakReferenceMessenger.Default.Send(new FolderSelectedMessage(path, name));
        return true;
    }

    public void Cleanup()
    {
        if (_isCleanedUp)
            return;

        _isCleanedUp = true;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _thumbnailGenerator.ProgressChanged -= _thumbnailProgressChangedHandler;
    }

    private void UpdateToolbarState()
    {
        int count = FolderItems.Count;
        FolderCountText = string.Format(_loc["Library.FolderCount"], count);
        IsEmpty = count == 0;
    }

    private void UpdateThumbnailProgress(int ready, int total)
    {
        if (total > 0 && ready < total)
        {
            ThumbnailProgressText = string.Format(_loc["Library.ThumbnailProgress"], ready, total);
            IsThumbnailProgressVisible = true;
        }
        else
        {
            IsThumbnailProgressVisible = false;
        }
    }

    private void OnThumbnailProgressChanged(object? sender, ThumbnailProgressEventArgs args)
    {
        Application.Current.Dispatcher.BeginInvoke(
            () => UpdateThumbnailProgress(args.Ready, args.Total));
    }
}




