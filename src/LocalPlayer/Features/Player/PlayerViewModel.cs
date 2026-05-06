using System;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalPlayer.Features.Player.Models;
using LocalPlayer.Infrastructure.Localization;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Thumbnails;
using LocalPlayer.Presentation.Primitives;

namespace LocalPlayer.Features.Player;

public partial class PlayerViewModel : ObservableObject, ITransitioningContentLifecycle
{
    private readonly PlayerSessionController _session;
    private readonly PlayerPlaybackStateController _playback;
    private readonly IMediaPlayerController _media;
    private bool _isMediaInitialized;

    private float _savedRate = 1.0f;

    public ControlBarViewModel ControlBar { get; }
    public PlaylistViewModel Playlist => _session.Playlist;

    public event Action? ToggleFullscreenRequested;
    public event Action? GoBackRequested;

    public ImageSource? VideoSource => _playback.VideoSource;
    public PlaylistItem? CurrentItem => _session.CurrentItem;
    public System.Collections.ObjectModel.ObservableCollection<PlaylistItem> PlaylistItems => _session.PlaylistItems;

    [ObservableProperty]
    private bool _isFullscreen;

    [ObservableProperty]
    private int _currentIndex = -1;

    private bool _savedPlaylistVisible;

    public static string FormatTime(long ms) => MediaPlayerController.FormatTime(ms);

    public PlayerViewModel(
        PlayerSessionController session,
        PlayerPlaybackStateController playback,
        IThumbnailGenerator thumbnailGenerator,
        IMediaPlayerController media,
        ILocalizationService loc)
    {
        _session = session;
        _playback = playback;
        _media = media;

        ControlBar = new ControlBarViewModel(media, thumbnailGenerator, loc, playback);

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
        _media.Rate = rate;
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
        _media.Initialize();
        _isMediaInitialized = true;
        OnPropertyChanged(nameof(VideoSource));
    }

    public void OnDisappearing()
    {
        if (_media.IsPlaying)
            _media.TogglePlayPause();
    }

    [RelayCommand]
    private void PlayPause() => _media.TogglePlayPause();

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
        _media.Rate = 3.0f;
    }

    [RelayCommand]
    private void ExitRightHold()
    {
        ControlBar.SetRate(_savedRate);
        _media.Rate = _savedRate;
    }

    [RelayCommand]
    private void Cleanup()
    {
        ControlBar.Cleanup();
        _playback.Cleanup(_session);
        _session.Cleanup();
    }

    private void GoBackInternal()
    {
        _session.SaveProgress();
        GoBackRequested?.Invoke();
    }
}
