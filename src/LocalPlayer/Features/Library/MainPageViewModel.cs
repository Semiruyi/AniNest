using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalPlayer.Features.Library.Models;
using LocalPlayer.Features.Library.Services;
using LocalPlayer.Infrastructure.Diagnostics;
using LocalPlayer.Infrastructure.Localization;
using LocalPlayer.Infrastructure.Thumbnails;

namespace LocalPlayer.Features.Library;

public partial class MainPageViewModel : ObservableObject
{
    private static readonly Infrastructure.Logging.Logger Log = Infrastructure.Logging.AppLog.For<MainPageViewModel>();
    private readonly ILibraryAppService _libraryService;
    private readonly ILocalizationService _loc;
    private readonly EventHandler<ThumbnailProgressEventArgs> _thumbnailProgressChangedHandler;
    private CancellationTokenSource? _loadDataCts;
    private CancellationTokenSource? _selectFolderCts;
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
        ILibraryAppService libraryService,
        ILocalizationService loc)
    {
        _libraryService = libraryService;
        _loc = loc;
        _thumbnailProgressChangedHandler = OnThumbnailProgressChanged;

        FolderItems.CollectionChanged += (_, _) => UpdateToolbarState();

        _libraryService.ThumbnailProgressChanged += _thumbnailProgressChangedHandler;
    }

    public void AddFolderItem(string name, string path, int videoCount, string? coverPath)
    {
        FolderItems.Add(CreateFolderItem(new LibraryFolderDto(name, path, videoCount, coverPath)));
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (_dataLoaded)
        {
            LoadDataCompleted?.Invoke(this, EventArgs.Empty);
            return;
        }

        CancelAndDispose(ref _loadDataCts);
        _loadDataCts = new CancellationTokenSource();

        try
        {
            var loadedItems = await _libraryService.LoadLibraryAsync(_loadDataCts.Token);

            FolderItems.Clear();
            foreach (var item in loadedItems)
                FolderItems.Add(CreateFolderItem(item));

            Log.Info(MemorySnapshot.Capture("MainPageViewModel.LoadDataAsync.loaded",
                ("items", FolderItems.Count),
                ("withCover", CountItemsWithCover())));
            _dataLoaded = true;
            LoadDataCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
        }
    }

    [RelayCommand]
    private async Task SelectFolder(FolderListItem? item)
    {
        if (item != null)
            await TrySelectFolderAsync(item.Path);
    }

    [RelayCommand]
    private async Task DeleteFolder(FolderListItem? item)
    {
        if (item == null)
            return;

        await _libraryService.DeleteFolderAsync(item.Path);
        FolderItems.Remove(item);
    }

    [RelayCommand]
    private void Settings()
    {
        int currentDays = _libraryService.GetThumbnailExpiryDays();
        string input = Microsoft.VisualBasic.Interaction.InputBox(
            _loc["Settings.ThumbnailExpiryPrompt"],
            _loc["Settings.ThumbnailSettings"],
            currentDays.ToString());

        var result = _libraryService.SaveThumbnailExpiryDays(input);
        if (!result.Success)
            return;

        if (result.Outcome == ThumbnailExpirySaveOutcome.SavedNever)
        {
            MessageBox.Show(_loc["Settings.ThumbnailSetNever"], _loc["Dialog.Info"]);
        }
        else if (result.Days.HasValue)
        {
            MessageBox.Show(string.Format(_loc["Settings.ThumbnailSetDays"], result.Days.Value), _loc["Dialog.Info"]);
        }
    }

    public async Task<bool> TrySelectFolderAsync(string path)
    {
        CancelAndDispose(ref _selectFolderCts);
        _selectFolderCts = new CancellationTokenSource();

        try
        {
            var result = await _libraryService.OpenFolderAsync(path, _selectFolderCts.Token);
            if (!result.Success)
            {
                MessageBox.Show(_loc["Dialog.NoVideosInFolder"], _loc["Dialog.Info"]);
                return false;
            }

            FolderSelected?.Invoke(path, result.FolderName);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public void Cleanup()
    {
        if (_isCleanedUp)
            return;

        _isCleanedUp = true;
        CancelAndDispose(ref _loadDataCts);
        CancelAndDispose(ref _selectFolderCts);
        _libraryService.ThumbnailProgressChanged -= _thumbnailProgressChangedHandler;
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

    private FolderListItem CreateFolderItem(LibraryFolderDto item)
        => new(item.Name, item.Path, item.VideoCount, item.CoverPath)
        {
            VideoCountText = string.Format(_loc["Library.VideoCount"], item.VideoCount)
        };

    private static void CancelAndDispose(ref CancellationTokenSource? cancellationTokenSource)
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;
    }

    private int CountItemsWithCover()
    {
        int count = 0;
        foreach (var item in FolderItems)
        {
            if (!string.IsNullOrWhiteSpace(item.CoverPath))
                count++;
        }

        return count;
    }
}
