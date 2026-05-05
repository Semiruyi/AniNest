using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LocalPlayer.Localization;
using LocalPlayer.Messages;
using LocalPlayer.Model;
using LocalPlayer.View.Diagnostics;

namespace LocalPlayer.ViewModel.Player;

public partial class PlayerViewModel : ObservableObject
{
    private static readonly Logger Log = AppLog.For<PlayerViewModel>();
    private readonly ISettingsService _settings;
    private readonly IMediaPlayerController _media;
    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly PlayerInputHandler _inputHandler;
    private readonly Action<string> _videoReadyHandler;
    private readonly Action<string, int> _videoProgressHandler;

    private PlaylistManager _playlistManager = null!;
    private DispatcherTimer _saveTimer = null!;
    private bool _initialized;
    private bool _isCleanedUp;
    private long _loadGeneration;
    private float _savedRate = 1.0f;
    private bool _savedPlaylistVisible;
    private PerfSpan? _loadFolderSpan;
    private PerfSpan? _cleanupSpan;

    public ControlBarViewModel ControlBar { get; }
    public PlaylistViewModel Playlist { get; }

    public ImageSource? VideoSource => _media.VideoBitmap;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private string? _currentVideoPath;

    [ObservableProperty]
    private bool _isFullscreen;

    [ObservableProperty]
    private int _currentIndex = -1;

    public PlaylistItem? CurrentItem => _playlistManager.CurrentItem;

    public System.Collections.ObjectModel.ObservableCollection<PlaylistItem> PlaylistItems => _playlistManager.Items;

    public static string FormatTime(long ms) => MediaPlayerController.FormatTime(ms);

    public Dictionary<string, Key> GetCurrentBindings() => _inputHandler.GetCurrentBindings();
    public void ReloadBindings() => _inputHandler.ReloadBindings();
    public bool HandleKeyDown(KeyEventArgs e) => _inputHandler.HandleKeyDown(e);

    public PlayerViewModel(
        ISettingsService settings,
        IThumbnailGenerator thumbnailGenerator,
        IMediaPlayerController media,
        ILocalizationService loc)
    {
        _settings = settings;
        _media = media;
        _thumbnailGenerator = thumbnailGenerator;
        _initialized = true;
        _videoReadyHandler = OnVideoReady;
        _videoProgressHandler = OnVideoProgress;

        _inputHandler = new PlayerInputHandler(_settings);
        _playlistManager = new PlaylistManager(_settings, _media, path => thumbnailGenerator.GetState(path));

        ControlBar = new ControlBarViewModel(_media, _inputHandler, thumbnailGenerator, loc);
        Playlist = new PlaylistViewModel(loc);
        Playlist.SetPlaylistManager(_playlistManager);

        ControlBar.NextRequested += PlayNext;
        ControlBar.PreviousRequested += PlayPrevious;
        ControlBar.GoBackRequested += GoBackInternal;
        ControlBar.TogglePlaylistRequested += () => Playlist.IsVisible = !Playlist.IsVisible;

        WeakReferenceMessenger.Default.Register<ToggleFullscreenMessage>(this, (_, _) =>
        {
            IsFullscreen = !IsFullscreen;
            ControlBar.IsFullscreen = IsFullscreen;
            if (IsFullscreen)
            {
                _savedPlaylistVisible = Playlist.IsVisible;
                Playlist.IsVisible = false;
            }
            else
            {
                Playlist.IsVisible = _savedPlaylistVisible;
            }
        });

        _playlistManager.VideoPlayed += filePath =>
        {
            CurrentVideoPath = filePath;
            ControlBar.CurrentVideoPath = filePath;
            ControlBar.ThumbnailPreview.OnCurrentVideoPathChanged();
        };

        _thumbnailGenerator.VideoReady += _videoReadyHandler;
        _thumbnailGenerator.VideoProgress += _videoProgressHandler;

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _saveTimer.Tick += (_, _) => SaveProgress();

        WireMediaEvents();
        WireInputEvents();
        _inputHandler.ReloadBindings();

        WeakReferenceMessenger.Default.Register<LoadPlayerFolderSkeletonMessage>(this, (_, m) =>
            LoadFolderSkeleton(m.Path, m.Name));

        WeakReferenceMessenger.Default.Register<LoadPlayerFolderDataMessage>(this, async (_, _) =>
            await LoadFolderDataAsync());

        _media.Initialize();
    }

    private void WireMediaEvents()
    {
        _media.Playing += (_, _) =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsPlaying = true;
                _saveTimer.Start();
            });

        _media.Paused += (_, _) =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsPlaying = false;
                _saveTimer.Stop();
                SaveProgress();
            });

        _media.Stopped += (_, _) =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsPlaying = false;
                _saveTimer.Stop();
            });
    }

    private void WireInputEvents()
    {
        _inputHandler.TogglePlayPause += (_, _) => _media.TogglePlayPause();
        _inputHandler.SeekForward += (_, _) => _media.SeekForward(5000);
        _inputHandler.SeekBackward += (_, _) => _media.SeekBackward(5000);
        _inputHandler.NextEpisode += (_, _) => PlayNext();
        _inputHandler.PreviousEpisode += (_, _) => PlayPrevious();
        _inputHandler.Back += (_, _) =>
        {
            if (IsFullscreen)
            {
                WeakReferenceMessenger.Default.Send(new ToggleFullscreenMessage());
            }
            else
            {
                GoBackInternal();
            }
        };
    }

    public void LoadFolderSkeleton(string folderPath, string folderName)
    {
        if (_isCleanedUp)
            return;

        var generation = ++_loadGeneration;
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

        _loadFolderSpan?.Dispose();
        _loadFolderSpan = null;
    }

    public async Task LoadFolderDataAsync()
    {
        if (_isCleanedUp)
            return;

        var generation = _loadGeneration;

        using var dataSpan = PerfSpan.Begin("Player.LoadFolderData");
        await Playlist.LoadFolderDataAsync();

        using var currentIndexSpan = PerfSpan.Begin("Player.CurrentIndexSync");
        CurrentIndex = Playlist.CurrentIndex;

        if (_isCleanedUp || generation != _loadGeneration)
            return;

        Playlist.ActivateCurrentVideo();
        Playlist.RefreshCurrentIndex();
    }

    private void PlayNext()
    {
        if (Playlist.PlayNext())
            CurrentIndex = Playlist.CurrentIndex;
    }

    private void PlayPrevious()
    {
        if (Playlist.PlayPrevious())
            CurrentIndex = Playlist.CurrentIndex;
    }

    public void SaveProgress()
    {
        Playlist.SaveProgress();
    }

    [RelayCommand]
    private void PlayPause() => _media.TogglePlayPause();

    [RelayCommand]
    private void GoBack()
    {
        if (IsFullscreen)
        {
            WeakReferenceMessenger.Default.Send(new ToggleFullscreenMessage());
        }
        else
        {
            GoBackInternal();
        }
    }

    [RelayCommand]
    private void EnterRightHold()
    {
        _savedRate = ControlBar.Rate;
        ControlBar.Rate = 3.0f;
        _media.Rate = 3.0f;
    }

    [RelayCommand]
    private void ExitRightHold()
    {
        ControlBar.Rate = _savedRate;
        _media.Rate = _savedRate;
    }

    private void GoBackInternal()
    {
        SaveProgress();
        WeakReferenceMessenger.Default.Send(new BackRequestedMessage());
    }

    [RelayCommand]
    private void KeyDown(KeyEventArgs e) => HandleKeyDown(e);

    [RelayCommand]
    private void Initialize() => _media.Initialize();

    [RelayCommand]
    private void Cleanup()
    {
        if (_isCleanedUp)
            return;

        _isCleanedUp = true;
        _cleanupSpan?.Dispose();
        _cleanupSpan = PerfSpan.Begin("Player.Cleanup");

        WeakReferenceMessenger.Default.UnregisterAll(this);
        _thumbnailGenerator.VideoReady -= _videoReadyHandler;
        _thumbnailGenerator.VideoProgress -= _videoProgressHandler;

        _initialized = false;
        _saveTimer.Stop();
        SaveProgress();
        _media.Dispose();

        _cleanupSpan?.Dispose();
        _cleanupSpan = null;
    }

    public void SetRate(float rate)
    {
        _media.Rate = rate;
        ControlBar.Rate = rate;
    }

    public long MediaLength => _media.Length;

    private void OnVideoReady(string path)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_initialized)
            {
                Log.Debug($"VideoReady -> UpdateThumbnailReady: {Path.GetFileName(path)}");
                _playlistManager.UpdateThumbnailReady(path);
            }
            else
            {
                Log.Debug($"VideoReady skipped (_initialized=false): {Path.GetFileName(path)}");
            }
        });
    }

    private void OnVideoProgress(string path, int percent)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_initialized)
            {
                Log.Debug($"VideoProgress -> UpdateThumbnailProgress: {Path.GetFileName(path)}={percent}%");
                _playlistManager.UpdateThumbnailProgress(path, percent);
            }
            else
            {
                Log.Debug($"VideoProgress skipped (_initialized=false): {Path.GetFileName(path)}={percent}%");
            }
        });
    }
}
