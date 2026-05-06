using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using LocalPlayer.Features.Player.Models;
using LocalPlayer.Infrastructure.Diagnostics;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Thumbnails;
using LocalPlayer.Infrastructure.Interop;
using LocalPlayer.Infrastructure.Localization;
using System.Windows;

namespace LocalPlayer.Features.Player;

public partial class PlayerSessionController : ObservableObject
{
    private static readonly Logger Log = AppLog.For<PlayerSessionController>();
    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly Action<string> _videoReadyHandler;
    private readonly Action<string, int> _videoProgressHandler;
    private readonly PlaylistManager _playlistManager;
    private bool _isCleanedUp;
    private long _loadGeneration;
    private long _loadedGeneration;
    private PerfSpan? _loadFolderSpan;

    public PlaylistViewModel Playlist { get; }

    [ObservableProperty]
    private int _currentIndex = -1;

    public PlaylistItem? CurrentItem => _playlistManager.CurrentItem;

    [ObservableProperty]
    private string? _currentVideoPath;

    public System.Collections.ObjectModel.ObservableCollection<PlaylistItem> PlaylistItems => _playlistManager.Items;

    public event Action<int>? CurrentIndexChanged;
    public event Action<string>? CurrentVideoPathChanged;

    public PlayerSessionController(
        ISettingsService settings,
        IThumbnailGenerator thumbnailGenerator,
        IMediaPlayerController media,
        ILocalizationService loc)
    {
        _thumbnailGenerator = thumbnailGenerator;
        _videoReadyHandler = OnVideoReady;
        _videoProgressHandler = OnVideoProgress;

        _playlistManager = new PlaylistManager(settings, media, path => thumbnailGenerator.GetState(path));
        Playlist = new PlaylistViewModel(loc);
        Playlist.SetPlaylistManager(_playlistManager);
        Playlist.CurrentIndexChanged += OnPlaylistCurrentIndexChanged;
        Playlist.VideoPlayed += OnPlaylistVideoPlayed;

        _thumbnailGenerator.VideoReady += _videoReadyHandler;
        _thumbnailGenerator.VideoProgress += _videoProgressHandler;
    }

    public void LoadFolderSkeleton(string folderPath, string folderName)
    {
        if (_isCleanedUp)
            return;

        Log.Info($"LoadFolderSkeleton start: {folderName} | {folderPath}");
        _loadGeneration++;
        _loadFolderSpan?.Dispose();
        _loadFolderSpan = PerfSpan.Begin("Player.LoadFolderSkeleton", new Dictionary<string, string>
        {
            ["folder"] = folderName
        });

        using var playlistSpan = PerfSpan.Begin("Player.Playlist.LoadFolderSkeleton", new Dictionary<string, string>
        {
            ["folder"] = folderName
        });
        Playlist.LoadFolderSkeleton(folderPath, folderName);
        Log.Info($"LoadFolderSkeleton done: generation={_loadGeneration}, items={PlaylistItems.Count}");

        _loadFolderSpan?.Dispose();
        _loadFolderSpan = null;
    }

    public async Task LoadFolderDataAsync()
    {
        if (_isCleanedUp)
            return;

        var generation = _loadGeneration;
        if (generation == _loadedGeneration)
        {
            Log.Debug($"LoadFolderDataAsync skipped: generation already loaded ({generation})");
            return;
        }

        Log.Info($"LoadFolderDataAsync start: generation={generation}, loaded={_loadedGeneration}, items={PlaylistItems.Count}");
        using var dataSpan = PerfSpan.Begin("Player.LoadFolderData");
        await Playlist.LoadFolderDataAsync();

        using var currentIndexSpan = PerfSpan.Begin("Player.CurrentIndexSync");
        SyncCurrentIndex();
        Log.Info($"After LoadFolderDataAsync sync: CurrentIndex={CurrentIndex}, CurrentVideoPath={CurrentVideoPath ?? "null"}");

        if (_isCleanedUp || generation != _loadGeneration)
            return;

        Playlist.ActivateCurrentVideo();
        SyncCurrentIndex();
        _loadedGeneration = generation;
        Log.Info($"LoadFolderDataAsync complete: CurrentIndex={CurrentIndex}, CurrentVideoPath={CurrentVideoPath ?? "null"}");
    }

    public bool PlayNext()
        => Playlist.PlayNext();

    public bool PlayPrevious()
        => Playlist.PlayPrevious();

    public void SaveProgress()
        => Playlist.SaveProgress();

    public void Cleanup()
    {
        if (_isCleanedUp)
            return;

        _isCleanedUp = true;
        _loadFolderSpan?.Dispose();
        _loadFolderSpan = null;

        Playlist.CurrentIndexChanged -= OnPlaylistCurrentIndexChanged;
        Playlist.VideoPlayed -= OnPlaylistVideoPlayed;
        Playlist.Cleanup();
        _thumbnailGenerator.VideoReady -= _videoReadyHandler;
        _thumbnailGenerator.VideoProgress -= _videoProgressHandler;
    }

    private void SyncCurrentIndex()
        => CurrentIndex = Playlist.CurrentIndex;

    partial void OnCurrentIndexChanged(int value)
        => OnPropertyChanged(nameof(CurrentItem));

    private void OnCurrentIndexChangedValue(int value)
        => CurrentIndexChanged?.Invoke(value);

    private void OnPlaylistCurrentIndexChanged(int value)
    {
        Log.Debug($"Playlist CurrentIndexChanged -> {value}");
        SyncCurrentIndex();
        OnCurrentIndexChangedValue(value);
    }

    private void OnPlaylistVideoPlayed(string filePath)
    {
        Log.Info($"Playlist VideoPlayed -> {Path.GetFileName(filePath)}");
        CurrentVideoPath = filePath;
        CurrentVideoPathChanged?.Invoke(filePath);
    }

    private void OnVideoReady(string path)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_isCleanedUp)
            {
                Log.Debug($"VideoReady skipped (_isCleanedUp=true): {Path.GetFileName(path)}");
                return;
            }

            Log.Debug($"VideoReady -> UpdateThumbnailReady: {Path.GetFileName(path)}");
            _playlistManager.UpdateThumbnailReady(path);
        });
    }

    private void OnVideoProgress(string path, int percent)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_isCleanedUp)
            {
                Log.Debug($"VideoProgress skipped (_isCleanedUp=true): {Path.GetFileName(path)}={percent}%");
                return;
            }

            Log.Debug($"VideoProgress -> UpdateThumbnailProgress: {Path.GetFileName(path)}={percent}%");
            _playlistManager.UpdateThumbnailProgress(path, percent);
        });
    }
}
