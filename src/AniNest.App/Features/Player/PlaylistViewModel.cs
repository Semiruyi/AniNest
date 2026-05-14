using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AniNest.Features.Player.Models;
using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Player;

public partial class PlaylistViewModel : ObservableObject
{
    private PlaylistManager _playlistManager = null!;
    private readonly ILocalizationService _loc;
    private Action<string>? _videoPlayedHandler;
    private Action<PlaybackFailureInfo>? _playbackFailedHandler;

    public PlaylistViewModel(ILocalizationService loc)
    {
        _loc = loc;
    }

    public event Action<int>? CurrentIndexChanged;
    public event Action<string>? VideoPlayed;
    public event Action<PlaybackFailureInfo>? PlaybackFailed;

    [ObservableProperty]
    private string _episodeCountText = "";

    [ObservableProperty]
    private string _currentFolderName = "";

    [ObservableProperty]
    private bool _isVisible = true;

    private int _currentIndex = -1;
    public int CurrentIndex
    {
        get => _currentIndex;
        set => SetCurrentIndex(value);
    }

    public PlaylistItem? CurrentItem => _playlistManager?.CurrentItem;

    public ObservableCollection<PlaylistItem> Items => _playlistManager?.Items
        ?? new ObservableCollection<PlaylistItem>();

    public void SetPlaylistManager(PlaylistManager playlistManager)
    {
        _playlistManager = playlistManager;
        _videoPlayedHandler = filePath => VideoPlayed?.Invoke(filePath);
        _playbackFailedHandler = failure => PlaybackFailed?.Invoke(failure);
        _playlistManager.VideoPlayed += _videoPlayedHandler;
        _playlistManager.PlaybackFailed += _playbackFailedHandler;
    }

    public async System.Threading.Tasks.Task LoadFolderAsync(string folderPath, string folderName, CancellationToken cancellationToken = default)
    {
        await _playlistManager.LoadFolderAsync(folderPath, folderName, cancellationToken);
        CurrentFolderName = folderName;

        var items = _playlistManager.Items;
        EpisodeCountText = items.Count > 0 ? string.Format(_loc["Player.EpisodeCount"], items.Count) : "";
        SetCurrentIndex(_playlistManager.CurrentIndex, force: true);
    }

    public async System.Threading.Tasks.Task LoadFolderSkeletonAsync(string folderPath, string folderName, CancellationToken cancellationToken = default)
    {
        await _playlistManager.LoadFolderSkeletonAsync(folderPath, folderName, cancellationToken);
        CurrentFolderName = folderName;

        var items = _playlistManager.Items;
        EpisodeCountText = items.Count > 0 ? string.Format(_loc["Player.EpisodeCount"], items.Count) : "";
        SetCurrentIndex(-1, force: true);
    }

    public async System.Threading.Tasks.Task LoadFolderDataAsync()
    {
        await _playlistManager.LoadFolderDataAsync();
        SetCurrentIndex(_playlistManager.CurrentIndex, force: true);
    }

    public async System.Threading.Tasks.Task LoadFolderDataAsync(CancellationToken cancellationToken)
    {
        await _playlistManager.LoadFolderDataAsync(cancellationToken);
        SetCurrentIndex(_playlistManager.CurrentIndex, force: true);
    }

    public void ActivateCurrentVideo()
    {
        _playlistManager.PlayCurrentVideo();
    }

    public void SaveProgress()
    {
        _playlistManager.SaveProgress();
    }

    public bool PlayNext()
    {
        if (_playlistManager.PlayNext())
        {
            SetCurrentIndex(_playlistManager.CurrentIndex, force: true);
            return true;
        }
        return false;
    }

    public bool PlayPrevious()
    {
        if (_playlistManager.PlayPrevious())
        {
            SetCurrentIndex(_playlistManager.CurrentIndex, force: true);
            return true;
        }
        return false;
    }

    [RelayCommand]
    private void PlayEpisode(PlaylistItem item)
    {
        int index = item.Number - 1;
        if (index < 0 || index >= _playlistManager.Items.Count) return;
        if (index == CurrentIndex) return;

        if (CurrentIndex >= 0 && CurrentIndex < _playlistManager.Items.Count)
            _playlistManager.Items[CurrentIndex].IsPlayed = true;

        _playlistManager.PlayEpisode(index);
        SetCurrentIndex(_playlistManager.CurrentIndex, force: true);
    }

    public void SyncThumbnailVisualStates(
        IReadOnlyDictionary<string, ThumbnailActiveTaskSnapshot> activeTasksByPath,
        Func<string, ThumbnailState> getThumbnailState)
        => _playlistManager.SyncThumbnailVisualStates(activeTasksByPath, getThumbnailState);

    public void RefreshCurrentIndex()
    {
        SetCurrentIndex(_playlistManager.CurrentIndex, force: true);
    }

    public void ResetSession()
    {
        _playlistManager.ResetSession();
        CurrentFolderName = string.Empty;
        EpisodeCountText = string.Empty;
        IsVisible = true;
        SetCurrentIndex(-1, force: true);
    }

    public void Cleanup()
    {
        if (_videoPlayedHandler != null)
        {
            _playlistManager.VideoPlayed -= _videoPlayedHandler;
            _videoPlayedHandler = null;
        }

        if (_playbackFailedHandler != null)
        {
            _playlistManager.PlaybackFailed -= _playbackFailedHandler;
            _playbackFailedHandler = null;
        }
    }

    private void SetCurrentIndex(int value, bool force = false)
    {
        if (!force && _currentIndex == value)
            return;

        _currentIndex = value;
        OnPropertyChanged(nameof(CurrentIndex));
        OnPropertyChanged(nameof(CurrentItem));
        CurrentIndexChanged?.Invoke(value);
    }
}
