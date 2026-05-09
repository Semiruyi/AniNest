using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AniNest.Features.Library.Models;
using AniNest.Features.Library.Services;
using AniNest.Infrastructure.Diagnostics;
using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Thumbnails;
using AniNest.Presentation.Primitives;

namespace AniNest.Features.Library;

public partial class MainPageViewModel : ObservableObject, ITransitioningContentLifecycle
{
    private static readonly Infrastructure.Logging.Logger Log = Infrastructure.Logging.AppLog.For<MainPageViewModel>();
    private readonly ILibraryAppService _libraryService;
    private readonly ILocalizationService _loc;
    private readonly EventHandler<ThumbnailProgressEventArgs> _thumbnailProgressChangedHandler;
    private readonly PropertyChangedEventHandler _localizationChangedHandler;
    private CancellationTokenSource? _loadDataCts;
    private CancellationTokenSource? _selectFolderCts;
    private FolderListItem? _activePopupItem;
    private bool _dataLoaded;
    private bool _isCleanedUp;
    private string? _lastFocusedFolderPath;

    public event EventHandler? LoadDataCompleted;
    public event Action<string, string>? FolderSelected;

    public ILocalizationService Localization => _loc;
    public string PlayedSummaryPrefix => _loc["Library.PlayedSummary.Prefix"];
    public string PlayedSummaryTotal => _loc["Library.PlayedSummary.Total"];
    public string PlayedSummarySuffix => _loc["Library.PlayedSummary.Suffix"];
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
        _localizationChangedHandler = OnLocalizationChanged;

        FolderItems.CollectionChanged += OnFolderItemsCollectionChanged;

        _libraryService.ThumbnailProgressChanged += _thumbnailProgressChangedHandler;
        _loc.PropertyChanged += _localizationChangedHandler;
    }

    public void AddFolderItem(string name, string path, int videoCount, string? coverPath)
    {
        FolderItems.Add(CreateFolderItem(new LibraryFolderDto(name, path, videoCount, coverPath)));
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        await LoadDataCoreAsync(forceReload: false);
    }

    public Task RefreshLibraryAsync()
        => LoadDataCoreAsync(forceReload: true);

    public void OnAppearing()
    {
        if (!_dataLoaded)
            return;

        _ = RefreshLibraryOnAppearAsync();
    }

    public void OnDisappearing()
    {
    }

    private async Task LoadDataCoreAsync(bool forceReload)
    {
        if (_dataLoaded && !forceReload)
        {
            Log.Debug("LoadDataCore skipped: data already loaded");
            LoadDataCompleted?.Invoke(this, EventArgs.Empty);
            return;
        }

        CancelAndDispose(ref _loadDataCts);
        _loadDataCts = new CancellationTokenSource();
        Log.Info($"LoadDataCore start: forceReload={forceReload}");

        try
        {
            var loadedItems = await _libraryService.LoadLibraryAsync(_loadDataCts.Token);

            FolderItems.Clear();
            foreach (var item in loadedItems)
                FolderItems.Add(CreateFolderItem(item));

            _dataLoaded = true;
            FocusFirstFolderIfNeeded(loadedItems);
            Log.Info($"LoadDataCore complete: items={loadedItems.Count}");
            LoadDataCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            Log.Info($"LoadDataCore canceled: forceReload={forceReload}");
        }
        catch (Exception ex)
        {
            Log.Error($"LoadDataCore failed: forceReload={forceReload}", ex);
        }
    }

    [RelayCommand]
    private async Task SelectFolder(FolderListItem? item)
    {
        CloseFolderPopup();

        if (item != null)
        {
            Log.Debug($"SelectFolder: name={item.Name} path={item.Path}");
            await TrySelectFolderAsync(item.Path);
        }
    }

    [RelayCommand]
    private void OpenFolderPopup(FolderListItem? item)
    {
        if (item == null)
            return;

        if (ReferenceEquals(_activePopupItem, item))
        {
            bool nextState = !item.IsPopupOpen;
            Log.Debug($"OpenFolderPopup(toggle): name={item.Name} path={item.Path} nextState={nextState}");
            item.IsPopupOpen = nextState;
            if (!nextState)
                _activePopupItem = null;

            return;
        }

        Log.Debug($"OpenFolderPopup(open): name={item.Name} path={item.Path}");
        CloseFolderPopup();
        _activePopupItem = item;
        item.IsPopupOpen = true;
    }

    [RelayCommand(CanExecute = nameof(CanMoveFolderToFront))]
    private async Task MoveFolderToFront(FolderListItem? item)
    {
        if (item == null)
            return;

        int currentIndex = FolderItems.IndexOf(item);
        if (currentIndex <= 0)
        {
            Log.Debug($"MoveFolderToFront skipped: already first name={item.Name} path={item.Path}");
            CloseFolderPopup();
            return;
        }

        CloseFolderPopup();
        Log.Info($"MoveFolderToFront: name={item.Name} path={item.Path} fromIndex={currentIndex}");
        await _libraryService.MoveFolderToFrontAsync(item.Path);
        FolderItems.Move(currentIndex, 0);
        UpdateMoveToFrontState();
        MoveFolderToFrontCommand.NotifyCanExecuteChanged();
    }

    private bool CanMoveFolderToFront(FolderListItem? item)
        => item is { CanMoveToFront: true };

    [RelayCommand]
    private async Task DeleteFolder(FolderListItem? item)
    {
        if (item == null)
            return;

        CloseFolderPopup();
        Log.Info($"DeleteFolder: name={item.Name} path={item.Path}");
        await _libraryService.DeleteFolderAsync(item.Path);
        FolderItems.Remove(item);
    }

    [RelayCommand]
    private async Task PrioritizeFolderThumbnails(FolderListItem? item)
    {
        if (item == null)
            return;

        CloseFolderPopup();
        Log.Info($"PrioritizeFolderThumbnails: name={item.Name} path={item.Path}");
        await _libraryService.PrioritizeFolderThumbnailsAsync(item.Path);
    }

    [RelayCommand]
    private async Task RegenerateFolderThumbnails(FolderListItem? item)
    {
        if (item == null)
            return;

        CloseFolderPopup();
        Log.Info($"RegenerateFolderThumbnails: name={item.Name} path={item.Path}");
        await _libraryService.RegenerateFolderThumbnailsAsync(item.Path);
    }

    [RelayCommand]
    private async Task ClearFolderThumbnailCache(FolderListItem? item)
    {
        if (item == null)
            return;

        CloseFolderPopup();
        Log.Info($"ClearFolderThumbnailCache: name={item.Name} path={item.Path}");
        await _libraryService.ClearFolderThumbnailCacheAsync(item.Path);
    }

    [RelayCommand]
    private async Task ClearFolderWatchHistory(FolderListItem? item)
    {
        if (item == null)
            return;

        CloseFolderPopup();
        Log.Info($"ClearFolderWatchHistory: name={item.Name} path={item.Path}");
        var updated = await _libraryService.ClearFolderWatchHistoryAsync(item.Path);
        if (updated == null)
            return;

        item.PlayedCount = updated.PlayedCount;
        item.PlayedPercent = updated.VideoCount > 0
            ? (double)updated.PlayedCount / updated.VideoCount * 100
            : 0;
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
        CloseFolderPopup();
        CancelAndDispose(ref _loadDataCts);
        CancelAndDispose(ref _selectFolderCts);
        _libraryService.ThumbnailProgressChanged -= _thumbnailProgressChangedHandler;
        _loc.PropertyChanged -= _localizationChangedHandler;
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
    {
        return new FolderListItem(item.Name, item.Path, item.VideoCount, item.CoverPath)
        {
            PlayedPercent = item.VideoCount > 0 ? (double)item.PlayedCount / item.VideoCount * 100 : 0,
            PlayedCount = item.PlayedCount
        };
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(ILocalizationService.CurrentLanguage) and not "Item[]")
            return;

        OnPropertyChanged(nameof(Localization));
        OnPropertyChanged(nameof(PlayedSummaryPrefix));
        OnPropertyChanged(nameof(PlayedSummaryTotal));
        OnPropertyChanged(nameof(PlayedSummarySuffix));
    }

    private void OnFolderItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (FolderListItem item in e.OldItems)
                item.PropertyChanged -= OnFolderItemPropertyChanged;
        }

        if (e.NewItems != null)
        {
            foreach (FolderListItem item in e.NewItems)
                item.PropertyChanged += OnFolderItemPropertyChanged;
        }

        UpdateToolbarState();
        UpdateMoveToFrontState();
        MoveFolderToFrontCommand.NotifyCanExecuteChanged();

        if (_activePopupItem != null && !FolderItems.Contains(_activePopupItem))
            _activePopupItem = null;
    }

    private void OnFolderItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not FolderListItem item || e.PropertyName != nameof(FolderListItem.IsPopupOpen))
            return;

        Log.Debug($"FolderPopupStateChanged: name={item.Name} path={item.Path} isOpen={item.IsPopupOpen} active={ReferenceEquals(_activePopupItem, item)}");

        if (item.IsPopupOpen)
        {
            if (!ReferenceEquals(_activePopupItem, item))
            {
                CloseFolderPopup(item);
                _activePopupItem = item;
            }

            return;
        }

        if (ReferenceEquals(_activePopupItem, item))
            _activePopupItem = null;
    }

    private void UpdateMoveToFrontState()
    {
        for (int i = 0; i < FolderItems.Count; i++)
            FolderItems[i].CanMoveToFront = i > 0;
    }

    private void CloseFolderPopup(FolderListItem? except = null)
    {
        foreach (var item in FolderItems)
        {
            if (ReferenceEquals(item, except))
                continue;

            if (item.IsPopupOpen)
            {
                Log.Debug($"CloseFolderPopup: name={item.Name} path={item.Path}");
                item.IsPopupOpen = false;
            }
        }

        if (_activePopupItem != null && !ReferenceEquals(_activePopupItem, except))
            _activePopupItem = null;
    }

    private static void CancelAndDispose(ref CancellationTokenSource? cancellationTokenSource)
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;
    }

    private async Task RefreshLibraryOnAppearAsync()
    {
        Log.Debug("RefreshLibraryOnAppear queued");

        try
        {
            await RefreshLibraryAsync();
            Log.Debug("RefreshLibraryOnAppear complete");
        }
        catch (Exception ex)
        {
            Log.Error("RefreshLibraryOnAppear failed", ex);
        }
    }

    private void FocusFirstFolderIfNeeded(IReadOnlyList<LibraryFolderDto> loadedItems)
    {
        if (loadedItems.Count == 0)
            return;

        var firstPath = loadedItems[0].Path;
        if (string.Equals(_lastFocusedFolderPath, firstPath, StringComparison.OrdinalIgnoreCase))
            return;

        _lastFocusedFolderPath = firstPath;
        Log.Info($"FocusFirstFolder: path={firstPath}");
        _ = _libraryService.FocusFolderThumbnailsAsync(firstPath);
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
