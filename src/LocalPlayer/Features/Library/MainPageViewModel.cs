using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalPlayer.Features.Library.Models;
using LocalPlayer.Infrastructure.Localization;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Thumbnails;

namespace LocalPlayer.Features.Library;

public partial class MainPageViewModel : ObservableObject
{
    private readonly ILibraryPageCoordinator _coordinator;
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _loc;
    private readonly EventHandler<ThumbnailProgressEventArgs> _thumbnailProgressChangedHandler;
    private bool _dataLoaded;
    private bool _isCleanedUp;

    public event EventHandler? LoadDataCompleted;
    public event Action<string, string>? FolderSelected;

    public ObservableCollection<FolderListItem> FolderItems { get; } = new();

    [ObservableProperty]
    private string _folderCountText = "0 个文件夹";

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private string _thumbnailProgressText = "";

    [ObservableProperty]
    private bool _isThumbnailProgressVisible;

    public MainPageViewModel(
        ILibraryPageCoordinator coordinator,
        ISettingsService settings,
        ILocalizationService loc,
        IThumbnailGenerator thumbnailGenerator)
    {
        _coordinator = coordinator;
        _settings = settings;
        _loc = loc;
        _thumbnailProgressChangedHandler = OnThumbnailProgressChanged;

        FolderItems.CollectionChanged += (_, _) => UpdateToolbarState();

        thumbnailGenerator.ProgressChanged += _thumbnailProgressChangedHandler;
    }

    public void AddFolderItem(string name, string path, int videoCount, string? coverPath)
    {
        FolderItems.Add(_coordinator.CreateFolderItem(name, path, videoCount, coverPath));
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (_dataLoaded)
        {
            LoadDataCompleted?.Invoke(this, EventArgs.Empty);
            return;
        }

        var loadedItems = await Task.Run(_coordinator.LoadFoldersData);

        FolderItems.Clear();
        foreach (var item in loadedItems)
            FolderItems.Add(item.Item);

        _coordinator.EnqueueAllFolders(loadedItems);
        _dataLoaded = true;
        LoadDataCompleted?.Invoke(this, EventArgs.Empty);
    }

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

        _coordinator.DeleteFolder(item);
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
                MessageBox.Show(_loc["Settings.ThumbnailSetNever"], _loc["Dialog.Info"]);
            else
                MessageBox.Show(string.Format(_loc["Settings.ThumbnailSetDays"], days), _loc["Dialog.Info"]);
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

        name = System.IO.Path.GetFileName(path);
        FolderSelected?.Invoke(path, name);
        return true;
    }

    public void Cleanup()
    {
        if (_isCleanedUp)
            return;

        _isCleanedUp = true;
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
        Application.Current.Dispatcher.BeginInvoke(() => UpdateThumbnailProgress(args.Ready, args.Total));
    }
}
