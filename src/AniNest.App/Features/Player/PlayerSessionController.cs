using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using AniNest.Features.Player.Models;
using AniNest.Features.Player.Services;
using AniNest.Infrastructure.Diagnostics;
using AniNest.Infrastructure.Logging;

namespace AniNest.Features.Player;

public partial class PlayerSessionController : ObservableObject
{
    private static readonly Logger Log = AppLog.For<PlayerSessionController>();
    private readonly IPlayerThumbnailSyncService _thumbnailSyncService;
    private readonly IPlayerPlaylistService _playlistService;
    private bool _isCleanedUp;

    public bool IsCleanedUp => _isCleanedUp;
    public PlaylistViewModel Playlist => _playlistService.Playlist;

    [ObservableProperty]
    private int _currentIndex = -1;

    public PlaylistItem? CurrentItem => _playlistService.CurrentItem;

    [ObservableProperty]
    private string? _currentVideoPath;

    public System.Collections.ObjectModel.ObservableCollection<PlaylistItem> PlaylistItems => _playlistService.Items;

    public event Action<int>? CurrentIndexChanged;
    public event Action<string>? CurrentVideoPathChanged;

    public PlayerSessionController(
        IPlayerThumbnailSyncService thumbnailSyncService,
        IPlayerPlaylistService playlistService)
    {
        _thumbnailSyncService = thumbnailSyncService;
        _playlistService = playlistService;
        Playlist.CurrentIndexChanged += OnPlaylistCurrentIndexChanged;
        Playlist.VideoPlayed += OnPlaylistVideoPlayed;
        _thumbnailSyncService.Attach(Playlist);
    }

    public Task LoadFolderSkeletonAsync(string folderPath, string folderName, CancellationToken cancellationToken)
        => _playlistService.LoadFolderSkeletonAsync(folderPath, folderName, cancellationToken);

    public async Task LoadFolderDataAsync(CancellationToken cancellationToken)
    {
        await _playlistService.LoadFolderDataAsync(cancellationToken);
        SyncCurrentIndex();
    }

    public void ActivateCurrentVideo()
    {
        _playlistService.ActivateCurrentVideo();
        SyncCurrentIndex();
    }

    public bool PlayNext()
        => _playlistService.PlayNext();

    public bool PlayPrevious()
        => _playlistService.PlayPrevious();

    public void SaveProgress()
        => _playlistService.SaveProgress();

    public void ResetSession()
    {
        Log.Info(MemorySnapshot.Capture("PlayerSessionController.ResetSession.begin",
            ("items", PlaylistItems.Count),
            ("currentIndex", CurrentIndex),
            ("hasCurrentVideoPath", !string.IsNullOrWhiteSpace(CurrentVideoPath))));
        _playlistService.ResetSession();
        CurrentVideoPath = null;
        SyncCurrentIndex();
        Log.Info(MemorySnapshot.Capture("PlayerSessionController.ResetSession.end",
            ("items", PlaylistItems.Count),
            ("currentIndex", CurrentIndex),
            ("hasCurrentVideoPath", !string.IsNullOrWhiteSpace(CurrentVideoPath))));
    }

    public void Cleanup()
    {
        if (_isCleanedUp)
            return;

        _isCleanedUp = true;

        Playlist.CurrentIndexChanged -= OnPlaylistCurrentIndexChanged;
        Playlist.VideoPlayed -= OnPlaylistVideoPlayed;
        _thumbnailSyncService.Detach(Playlist);
        _playlistService.Cleanup();
    }

    private void SyncCurrentIndex()
        => CurrentIndex = Playlist.CurrentIndex;

    partial void OnCurrentIndexChanged(int value)
        => OnPropertyChanged(nameof(CurrentItem));

    private void OnCurrentIndexChangedValue(int value)
        => CurrentIndexChanged?.Invoke(value);

    private void OnPlaylistCurrentIndexChanged(int value)
    {
        SyncCurrentIndex();
        var currentPath = CurrentItem?.FilePath;
        if (!string.Equals(CurrentVideoPath, currentPath, StringComparison.OrdinalIgnoreCase))
        {
            CurrentVideoPath = currentPath;
            Log.Info(MemorySnapshot.Capture("PlayerSessionController.CurrentVideoPathSynced",
                ("index", value),
                ("currentVideoPath", CurrentVideoPath ?? "null"),
                ("items", PlaylistItems.Count)));
        }
        Log.Info(MemorySnapshot.Capture("PlayerSessionController.CurrentIndexChanged",
            ("index", value),
            ("sessionIndex", CurrentIndex),
            ("currentVideoPath", CurrentVideoPath ?? "null"),
            ("items", PlaylistItems.Count)));
        OnCurrentIndexChangedValue(value);
    }

    private void OnPlaylistVideoPlayed(string filePath)
    {
        CurrentVideoPath = filePath;
        Log.Info(MemorySnapshot.Capture("PlayerSessionController.VideoPlayed",
            ("filePath", filePath),
            ("currentIndex", CurrentIndex),
            ("items", PlaylistItems.Count)));
        CurrentVideoPathChanged?.Invoke(filePath);
    }
}
