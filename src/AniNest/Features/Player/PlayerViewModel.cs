using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AniNest.Features.Player.Models;
using AniNest.Features.Player.Input;
using AniNest.Features.Player.Services;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Presentation;
using AniNest.Presentation.Primitives;

namespace AniNest.Features.Player;

public partial class PlayerViewModel : ObservableObject, ITransitioningContentLifecycle, IPlayerInputHost
{
    private static readonly Logger Log = AppLog.For<PlayerViewModel>();
    private readonly PlayerSessionController _session;
    private readonly PlayerPlaybackStateController _playback;
    private readonly IPlayerPlaybackFacade _playbackFacade;
    private readonly ILocalizationService _loc;
    private readonly IDialogService _dialogs;
    private readonly System.ComponentModel.PropertyChangedEventHandler _playbackPropertyChangedHandler;
    private bool _isMediaInitialized;
    private Task? _initializeTask;

    private float _savedRate = 1.0f;

    public ControlBarViewModel ControlBar { get; }
    public PlaylistViewModel Playlist => _session.Playlist;

    public event Action? ToggleFullscreenRequested;
    public event Action? GoBackRequested;
    public IPlayerInputService InputService { get; }

    public bool IsPlaying => _playback.IsPlaying;
    public ImageSource? VideoSource => _playback.VideoSource;
    public PlaylistItem? CurrentItem => _session.CurrentItem;
    public System.Collections.ObjectModel.ObservableCollection<PlaylistItem> PlaylistItems => _session.PlaylistItems;
    public string? CurrentVideoPath => _playback.CurrentVideoPath;
    public string CurrentVideoFileName => string.IsNullOrWhiteSpace(CurrentVideoPath)
        ? string.Empty
        : Path.GetFileName(CurrentVideoPath);

    [ObservableProperty]
    private bool _isFullscreen;

    [ObservableProperty]
    private int _currentIndex = -1;

    private bool _savedPlaylistVisible;

    public string FormatTime(long ms) => _playbackFacade.FormatTime(ms);

    public PlayerViewModel(
        PlayerSessionController session,
        PlayerPlaybackStateController playback,
        IPlayerPlaybackFacade playbackFacade,
        ILocalizationService loc,
        ISettingsService settings,
        IDialogService dialogs,
        IPlayerInputService inputService)
    {
        _session = session;
        _playback = playback;
        _playbackFacade = playbackFacade;
        _loc = loc;
        _dialogs = dialogs;
        InputService = inputService;
        _playbackPropertyChangedHandler = (_, args) =>
        {
            if (args.PropertyName is null)
                return;

            if (args.PropertyName == nameof(PlayerPlaybackStateController.IsPlaying))
                OnPropertyChanged(nameof(IsPlaying));

            if (args.PropertyName == nameof(PlayerPlaybackStateController.CurrentVideoPath))
            {
                OnPropertyChanged(nameof(CurrentVideoPath));
                OnPropertyChanged(nameof(CurrentVideoFileName));
            }
        };

        ControlBar = new ControlBarViewModel(playbackFacade, loc, settings, playback);

        ControlBar.NextRequested += () => _ = _session.PlayNext();
        ControlBar.PreviousRequested += () => _ = _session.PlayPrevious();
        ControlBar.GoBackRequested += GoBackInternal;
        ControlBar.TogglePlaylistRequested += () => Playlist.IsVisible = !Playlist.IsVisible;
        ControlBar.ToggleFullscreenRequested += () => ToggleFullscreenRequested?.Invoke();
        Playlist.PlaybackFailed += OnPlaybackFailed;

        _session.CurrentIndexChanged += value => CurrentIndex = value;
        _playback.PropertyChanged += _playbackPropertyChangedHandler;
    }

    partial void OnCurrentIndexChanged(int value)
        => OnPropertyChanged(nameof(CurrentItem));

    public void SetRate(float rate)
    {
        _playbackFacade.Rate = rate;
        ControlBar.SetRate(rate);
    }

    public void SetFullscreen(bool value)
    {
        IsFullscreen = value;
        ControlBar.IsFullscreen = value;

        if (value)
        {
            _savedPlaylistVisible = Playlist.IsVisible;
            Playlist.IsVisible = false;
        }
        else
        {
            Playlist.IsVisible = _savedPlaylistVisible;
        }
    }

    public void OnAppearing()
    {
        if (_isMediaInitialized)
            return;

        _initializeTask ??= InitializeMediaAsync();
    }

    public void OnDisappearing()
    {
        if (_playbackFacade.IsPlaying)
            _playbackFacade.TogglePlayPause();
    }

    public bool TryHandleInput(PlayerInputAction action)
    {
        switch (action)
        {
            case PlayerInputAction.PlayPause:
                _playbackFacade.TogglePlayPause();
                return true;
            case PlayerInputAction.Stop:
                _playbackFacade.Stop();
                return true;
            case PlayerInputAction.Next:
                _ = _session.PlayNext();
                return true;
            case PlayerInputAction.Previous:
                _ = _session.PlayPrevious();
                return true;
            case PlayerInputAction.ToggleFullscreen:
                ToggleFullscreenRequested?.Invoke();
                return true;
            case PlayerInputAction.ExitFullscreenOrBack:
                GoBack();
                return true;
            case PlayerInputAction.TogglePlaylist:
                Playlist.IsVisible = !Playlist.IsVisible;
                return true;
            case PlayerInputAction.SeekForwardSmall:
                _playbackFacade.SeekForward(5_000);
                return true;
            case PlayerInputAction.SeekBackwardSmall:
                _playbackFacade.SeekBackward(5_000);
                return true;
            case PlayerInputAction.SeekForwardLarge:
                _playbackFacade.SeekForward(15_000);
                return true;
            case PlayerInputAction.SeekBackwardLarge:
                _playbackFacade.SeekBackward(15_000);
                return true;
            case PlayerInputAction.BoostSpeedHold:
                EnterRightHold();
                return true;
            case PlayerInputAction.BoostSpeedRelease:
                ExitRightHold();
                return true;
            default:
                return false;
        }
    }

    [RelayCommand]
    private void PlayPause() => _playbackFacade.TogglePlayPause();

    [RelayCommand]
    private void GoBack()
    {
        if (IsFullscreen)
            ToggleFullscreenRequested?.Invoke();
        else
            GoBackInternal();
    }

    [RelayCommand]
    private void EnterRightHold()
    {
        _savedRate = ControlBar.Rate;
        ControlBar.SetRate(3.0f);
        _playbackFacade.Rate = 3.0f;
    }

    [RelayCommand]
    private void ExitRightHold()
    {
        ControlBar.SetRate(_savedRate);
        _playbackFacade.Rate = _savedRate;
    }

    [RelayCommand]
    private void Cleanup()
    {
        _playback.PropertyChanged -= _playbackPropertyChangedHandler;
        ControlBar.Cleanup();
        _playback.Cleanup();
        _session.Cleanup();
    }

    private void GoBackInternal()
    {
        _session.SaveProgress();
        GoBackRequested?.Invoke();
    }

    private void OnPlaybackFailed(PlaybackFailureInfo failure)
    {
        string fileName = Path.GetFileName(failure.FilePath);
        string message = string.Format(
            _loc["Player.PlaybackFailed.Message"],
            fileName,
            failure.ErrorMessage ?? _loc["Dialog.UnknownError"]);
        _dialogs.ShowError(message, _loc["Player.PlaybackFailed.Title"]);
    }

    private async Task InitializeMediaAsync()
    {
        try
        {
            await _playbackFacade.InitializeAsync();
            _isMediaInitialized = true;
            OnPropertyChanged(nameof(VideoSource));
        }
        catch (Exception ex)
        {
            Log.Error("Media initialization failed", ex);
        }
        finally
        {
            _initializeTask = null;
        }
    }
}
