using System;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AniNest.Features.Player.Models;
using AniNest.Features.Player.Input;
using AniNest.Features.Player.Services;
using AniNest.Infrastructure.Localization;
using AniNest.Presentation.Primitives;

namespace AniNest.Features.Player;

public partial class PlayerViewModel : ObservableObject, ITransitioningContentLifecycle, IPlayerInputHost
{
    private readonly PlayerSessionController _session;
    private readonly PlayerPlaybackStateController _playback;
    private readonly IPlayerPlaybackFacade _playbackFacade;
    private bool _isMediaInitialized;

    private float _savedRate = 1.0f;

    public ControlBarViewModel ControlBar { get; }
    public PlaylistViewModel Playlist => _session.Playlist;

    public event Action? ToggleFullscreenRequested;
    public event Action? GoBackRequested;
    public IPlayerInputService InputService { get; }

    public ImageSource? VideoSource => _playback.VideoSource;
    public PlaylistItem? CurrentItem => _session.CurrentItem;
    public System.Collections.ObjectModel.ObservableCollection<PlaylistItem> PlaylistItems => _session.PlaylistItems;

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
        IPlayerInputService inputService)
    {
        _session = session;
        _playback = playback;
        _playbackFacade = playbackFacade;
        InputService = inputService;

        ControlBar = new ControlBarViewModel(playbackFacade, loc, playback);

        ControlBar.NextRequested += () => _ = _session.PlayNext();
        ControlBar.PreviousRequested += () => _ = _session.PlayPrevious();
        ControlBar.GoBackRequested += GoBackInternal;
        ControlBar.TogglePlaylistRequested += () => Playlist.IsVisible = !Playlist.IsVisible;
        ControlBar.ToggleFullscreenRequested += () => ToggleFullscreenRequested?.Invoke();

        _session.CurrentIndexChanged += value => CurrentIndex = value;
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
        _playbackFacade.Initialize();
        _isMediaInitialized = true;
        OnPropertyChanged(nameof(VideoSource));
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
        ControlBar.Cleanup();
        _playback.Cleanup();
        _session.Cleanup();
    }

    private void GoBackInternal()
    {
        _session.SaveProgress();
        GoBackRequested?.Invoke();
    }
}
