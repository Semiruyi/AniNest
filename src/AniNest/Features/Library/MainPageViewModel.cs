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

namespace AniNest.Features.Library;

public partial class MainPageViewModel : ObservableObject
{
    private static readonly Infrastructure.Logging.Logger Log = Infrastructure.Logging.AppLog.For<MainPageViewModel>();
    private readonly ILibraryAppService _libraryService;
    private readonly ILocalizationService _loc;
    private readonly EventHandler<ThumbnailProgressEventArgs> _thumbnailProgressChangedHandler;
    private CancellationTokenSource? _loadDataCts;
    private CancellationTokenSource? _selectFolderCts;
    private FolderListItem? _activePopupItem;
    private bool _dataLoaded;
    private bool _isCleanedUp;

    public event EventHandler? LoadDataCompleted;
    public event Action<string, string>? FolderSelected;

    public ILocalizationService Localization => _loc;
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

        FolderItems.CollectionChanged += OnFolderItemsCollectionChanged;

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
            VideoCountText = string.Format(_loc["Library.VideoCount"], item.VideoCount)
        };
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
