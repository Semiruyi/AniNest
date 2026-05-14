using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AniNest.Features.Library.Models;
using AniNest.Features.Library.Services;
using AniNest.Features.Metadata;
using AniNest.Infrastructure.Diagnostics;
using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Presentation;
using AniNest.Infrastructure.Thumbnails;
using AniNest.Presentation.Primitives;

namespace AniNest.Features.Library;

public partial class MainPageViewModel : ObservableObject, ITransitioningContentLifecycle
{
    private static readonly Infrastructure.Logging.Logger Log = Infrastructure.Logging.AppLog.For<MainPageViewModel>();
    private readonly ILibraryAppService _libraryService;
    private readonly IMetadataQueryService _metadataQueryService;
    private readonly ILocalizationService _loc;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly List<FolderListItem> _allFolderItems = new();
    private readonly EventHandler<ThumbnailProgressEventArgs> _thumbnailProgressChangedHandler;
    private readonly EventHandler<FolderMetadataRefreshedEventArgs> _folderMetadataRefreshedHandler;
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
    public ObservableCollection<LibraryFilterOption> FilterOptions { get; } = new();
    public ObservableCollection<FolderListItem> FolderItems { get; } = new();
    public int SelectedFilterIndex => FilterOptions
        .Select((option, index) => new { option.Filter, index })
        .FirstOrDefault(item => item.Filter == SelectedFilter)?.index ?? -1;

    [ObservableProperty]
    private string _folderCountText = "0 个文件夹";

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private string _thumbnailProgressText = "";

    [ObservableProperty]
    private bool _isThumbnailProgressVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedFilterIndex))]
    private LibraryFilter _selectedFilter = LibraryFilter.All;

    public MainPageViewModel(
        ILibraryAppService libraryService,
        IMetadataQueryService metadataQueryService,
        ILocalizationService loc,
        IDialogService dialogs,
        IUiDispatcher uiDispatcher)
    {
        _libraryService = libraryService;
        _metadataQueryService = metadataQueryService;
        _loc = loc;
        _dialogs = dialogs;
        _uiDispatcher = uiDispatcher;
        _thumbnailProgressChangedHandler = OnThumbnailProgressChanged;
        _folderMetadataRefreshedHandler = OnFolderMetadataRefreshed;
        _localizationChangedHandler = OnLocalizationChanged;

        FolderItems.CollectionChanged += OnFolderItemsCollectionChanged;

        _libraryService.ThumbnailProgressChanged += _thumbnailProgressChangedHandler;
        _metadataQueryService.FolderMetadataRefreshed += _folderMetadataRefreshedHandler;
        _loc.PropertyChanged += _localizationChangedHandler;
        InitializeFilterOptions();
        RefreshFilterOptionSelectionState();
    }

    public void AddFolderItem(string name, string path, int videoCount, string? coverPath)
    {
        _allFolderItems.Add(CreateFolderItem(new LibraryFolderDto(name, path, videoCount, coverPath)));
        ApplyCurrentFilter();
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

            _allFolderItems.Clear();
            foreach (var item in loadedItems)
                _allFolderItems.Add(CreateFolderItem(item));

            ApplyCurrentFilter();

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

        int currentIndex = _allFolderItems.FindIndex(folder => string.Equals(folder.Path, item.Path, StringComparison.OrdinalIgnoreCase));
        if (currentIndex <= 0)
        {
            Log.Debug($"MoveFolderToFront skipped: already first name={item.Name} path={item.Path}");
            CloseFolderPopup();
            return;
        }

        CloseFolderPopup();
        Log.Info($"MoveFolderToFront: name={item.Name} path={item.Path} fromIndex={currentIndex}");
        await _libraryService.MoveFolderToFrontAsync(item.Path);
        MoveFolderItemToFront(item.Path);
        ApplyCurrentFilter();
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
        _allFolderItems.RemoveAll(folder => string.Equals(folder.Path, item.Path, StringComparison.OrdinalIgnoreCase));
        ApplyCurrentFilter();
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
    private void SetSelectedFilter(LibraryFilter filter)
    {
        SelectedFilter = filter;
    }

    [RelayCommand]
    private Task SetFolderWatchStatus(FolderStatusChangeRequest? request)
        => request == null
            ? Task.CompletedTask
            : SetFolderWatchStatusAsync(request.Item, request.Status);

    internal async Task SetFolderWatchStatusAsync(FolderListItem? item, Infrastructure.Persistence.WatchStatus status)
    {
        if (item == null)
            return;

        CloseFolderPopup();
        if (item.Status == status)
        {
            Log.Debug($"SetFolderWatchStatus skipped: name={item.Name} path={item.Path} status={status}");
            return;
        }

        Log.Info($"SetFolderWatchStatus: name={item.Name} path={item.Path} status={status}");
        await _libraryService.SetFolderWatchStatusAsync(item.Path, status);
        item.Status = status;
        ApplyCurrentFilter();
    }

    [RelayCommand]
    private async Task ToggleFolderFavorite(FolderListItem? item)
    {
        if (item == null)
            return;

        CloseFolderPopup();
        bool nextFavorite = !item.IsFavorite;
        Log.Info(
            $"ToggleFolderFavorite start: name={item.Name} path={item.Path} " +
            $"selectedFilter={SelectedFilter} isFavoriteBefore={item.IsFavorite} isFavoriteAfter={nextFavorite} " +
            $"visibleCountBefore={FolderItems.Count} totalCount={_allFolderItems.Count}");
        await _libraryService.SetFolderFavoriteAsync(item.Path, nextFavorite);
        item.IsFavorite = nextFavorite;
        bool matchesAfterToggle = MatchesSelectedFilter(item);
        Log.Debug(
            $"ToggleFolderFavorite post-update: name={item.Name} path={item.Path} " +
            $"selectedFilter={SelectedFilter} matchesAfterToggle={matchesAfterToggle}");
        ApplyCurrentFilter();
        Log.Info(
            $"ToggleFolderFavorite complete: name={item.Name} path={item.Path} " +
            $"selectedFilter={SelectedFilter} isFavoriteNow={item.IsFavorite} " +
            $"visibleCountAfter={FolderItems.Count} stillVisible={FolderItems.Contains(item)} " +
            $"folderItemsOrder=[{string.Join(", ", FolderItems.Select(x => x.Name))}]");
    }

    [RelayCommand]
    private void Settings()
    {
        int currentDays = _libraryService.GetThumbnailExpiryDays();
        string input = _dialogs.ShowInput(
            _loc["Settings.ThumbnailExpiryPrompt"],
            _loc["Settings.ThumbnailSettings"],
            currentDays.ToString());

        var result = _libraryService.SaveThumbnailExpiryDays(input);
        if (!result.Success)
            return;

        if (result.Outcome == ThumbnailExpirySaveOutcome.SavedNever)
        {
            _dialogs.ShowInfo(_loc["Settings.ThumbnailSetNever"], _loc["Dialog.Info"]);
        }
        else if (result.Days.HasValue)
        {
            _dialogs.ShowInfo(string.Format(_loc["Settings.ThumbnailSetDays"], result.Days.Value), _loc["Dialog.Info"]);
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
                _dialogs.ShowInfo(_loc["Dialog.NoVideosInFolder"], _loc["Dialog.Info"]);
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
        _metadataQueryService.FolderMetadataRefreshed -= _folderMetadataRefreshedHandler;
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
        _uiDispatcher.BeginInvoke(() => UpdateThumbnailProgress(args.Ready, args.Total));
    }

    private void OnFolderMetadataRefreshed(object? sender, FolderMetadataRefreshedEventArgs args)
    {
        _uiDispatcher.BeginInvoke(() => ApplyMetadataRefresh(args.FolderPath, args.Metadata));
    }

    private FolderListItem CreateFolderItem(LibraryFolderDto item)
    {
        return new FolderListItem(item.Name, item.Path, item.VideoCount, item.CoverPath, item.Metadata)
        {
            PlayedPercent = item.VideoCount > 0 ? (double)item.PlayedCount / item.VideoCount * 100 : 0,
            PlayedCount = item.PlayedCount,
            Status = item.Status,
            IsFavorite = item.IsFavorite
        };
    }

    private void ApplyMetadataRefresh(string folderPath, FolderMetadata metadata)
    {
        var item = _allFolderItems.FirstOrDefault(folder =>
            string.Equals(folder.Path, folderPath, StringComparison.OrdinalIgnoreCase));
        if (item == null)
            return;

        item.Metadata = metadata;
    }

    partial void OnSelectedFilterChanged(LibraryFilter value)
    {
        RefreshFilterOptionSelectionState();
        ApplyCurrentFilter();
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(ILocalizationService.CurrentLanguage) and not "Item[]")
            return;

        OnPropertyChanged(nameof(Localization));
        OnPropertyChanged(nameof(PlayedSummaryPrefix));
        OnPropertyChanged(nameof(PlayedSummaryTotal));
        OnPropertyChanged(nameof(PlayedSummarySuffix));
        RefreshFilterOptionLabels();
    }

    private void OnFolderItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Log.Debug(
            $"FolderItems.CollectionChanged: action={e.Action} oldCount={e.OldItems?.Count ?? 0} newCount={e.NewItems?.Count ?? 0} " +
            $"visibleCountNow={FolderItems.Count}");

        if (e.OldItems != null)
        {
            foreach (FolderListItem item in e.OldItems)
                Log.Debug($"FolderItems.CollectionChanged oldItem: {item.Name} path={item.Path}");
        }

        if (e.NewItems != null)
        {
            foreach (FolderListItem item in e.NewItems)
                Log.Debug($"FolderItems.CollectionChanged newItem: {item.Name} path={item.Path}");
        }

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
        {
            var item = FolderItems[i];
            int allIndex = _allFolderItems.FindIndex(folder => string.Equals(folder.Path, item.Path, StringComparison.OrdinalIgnoreCase));
            item.CanMoveToFront = allIndex > 0;
        }
    }

    private void ApplyCurrentFilter()
    {
        var filteredItems = _allFolderItems.Where(MatchesSelectedFilter).ToList();

        Log.Debug(
            $"ApplyCurrentFilter: selectedFilter={SelectedFilter} totalCount={_allFolderItems.Count} " +
            $"filteredCount={filteredItems.Count} visibleBefore={FolderItems.Count}");

        for (int i = FolderItems.Count - 1; i >= 0; i--)
        {
            var existingItem = FolderItems[i];
            if (!filteredItems.Contains(existingItem))
            {
                Log.Debug(
                    $"ApplyCurrentFilter remove: index={i} name={existingItem.Name} path={existingItem.Path} " +
                    $"status={existingItem.Status} favorite={existingItem.IsFavorite}");
                FolderItems.RemoveAt(i);
            }
        }

        for (int targetIndex = 0; targetIndex < filteredItems.Count; targetIndex++)
        {
            var targetItem = filteredItems[targetIndex];

            if (targetIndex < FolderItems.Count && ReferenceEquals(FolderItems[targetIndex], targetItem))
                continue;

            int existingIndex = FolderItems.IndexOf(targetItem);
            if (existingIndex >= 0)
            {
                Log.Debug(
                    $"ApplyCurrentFilter move: from={existingIndex} to={targetIndex} " +
                    $"name={targetItem.Name} path={targetItem.Path}");
                FolderItems.Move(existingIndex, targetIndex);
                continue;
            }

            Log.Debug(
                $"ApplyCurrentFilter insert: index={targetIndex} name={targetItem.Name} path={targetItem.Path} " +
                $"status={targetItem.Status} favorite={targetItem.IsFavorite}");
            FolderItems.Insert(targetIndex, targetItem);
        }

        Log.Debug(
            $"ApplyCurrentFilter complete: selectedFilter={SelectedFilter} visibleAfter={FolderItems.Count} " +
            $"order=[{string.Join(", ", FolderItems.Select(item => item.Name))}]");
    }

    private bool MatchesSelectedFilter(FolderListItem item)
    {
        return SelectedFilter switch
        {
            LibraryFilter.All => true,
            LibraryFilter.Watching => item.Status == Infrastructure.Persistence.WatchStatus.Watching,
            LibraryFilter.Unsorted => item.Status == Infrastructure.Persistence.WatchStatus.Unsorted,
            LibraryFilter.Completed => item.Status == Infrastructure.Persistence.WatchStatus.Completed,
            LibraryFilter.Favorites => item.IsFavorite,
            LibraryFilter.Dropped => item.Status == Infrastructure.Persistence.WatchStatus.Dropped,
            _ => true
        };
    }

    private void MoveFolderItemToFront(string path)
    {
        var item = _allFolderItems.FirstOrDefault(folder => string.Equals(folder.Path, path, StringComparison.OrdinalIgnoreCase));
        if (item == null)
            return;

        _allFolderItems.Remove(item);
        _allFolderItems.Insert(0, item);
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
            if (!string.IsNullOrWhiteSpace(item.EffectiveCoverPath))
                count++;
        }

        return count;
    }

    private void InitializeFilterOptions()
    {
        FilterOptions.Clear();
        FilterOptions.Add(new LibraryFilterOption(LibraryFilter.All, "Library.All", _loc["Library.All"]));
        FilterOptions.Add(new LibraryFilterOption(LibraryFilter.Watching, "Library.Watching", _loc["Library.Watching"]));
        FilterOptions.Add(new LibraryFilterOption(LibraryFilter.Unsorted, "Library.Unsorted", _loc["Library.Unsorted"]));
        FilterOptions.Add(new LibraryFilterOption(LibraryFilter.Completed, "Library.Completed", _loc["Library.Completed"]));
        FilterOptions.Add(new LibraryFilterOption(LibraryFilter.Favorites, "Library.Favorites", _loc["Library.Favorites"]));
        FilterOptions.Add(new LibraryFilterOption(LibraryFilter.Dropped, "Library.Dropped", _loc["Library.Dropped"]));
    }

    private void RefreshFilterOptionLabels()
    {
        foreach (var option in FilterOptions)
            option.DisplayName = _loc[option.LocalizationKey];
    }

    private void RefreshFilterOptionSelectionState()
    {
        foreach (var option in FilterOptions)
            option.IsSelected = option.Filter == SelectedFilter;
    }
}
