using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using AniNest.Features.Player.Models;

namespace AniNest.Features.Player;

public partial class PlayerDisplayStateViewModel : ObservableObject
{
    private readonly PlayerSessionController _session;
    private readonly PlayerPlaybackStateController _playback;
    private readonly ControlBarViewModel _controlBar;
    private readonly PlaylistViewModel _playlist;
    private readonly PropertyChangedEventHandler _playbackPropertyChangedHandler;
    private bool _savedPlaylistVisible;

    [ObservableProperty]
    private bool _isFullscreen;

    [ObservableProperty]
    private int _currentIndex = -1;

    public PlayerDisplayStateViewModel(
        PlayerSessionController session,
        PlayerPlaybackStateController playback,
        ControlBarViewModel controlBar,
        PlaylistViewModel playlist)
    {
        _session = session;
        _playback = playback;
        _controlBar = controlBar;
        _playlist = playlist;
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

        _session.CurrentIndexChanged += OnSessionCurrentIndexChanged;
        _playback.PropertyChanged += _playbackPropertyChangedHandler;
    }

    public bool IsPlaying => _playback.IsPlaying;
    public PlaylistItem? CurrentItem => _session.CurrentItem;
    public string? CurrentVideoPath => _playback.CurrentVideoPath;
    public string CurrentVideoFileName => string.IsNullOrWhiteSpace(CurrentVideoPath)
        ? string.Empty
        : Path.GetFileName(CurrentVideoPath);

    partial void OnCurrentIndexChanged(int value)
        => OnPropertyChanged(nameof(CurrentItem));

    public void SetFullscreen(bool value)
    {
        IsFullscreen = value;
        _controlBar.IsFullscreen = value;

        if (value)
        {
            _savedPlaylistVisible = _playlist.IsVisible;
            _playlist.IsVisible = false;
        }
        else
        {
            _playlist.IsVisible = _savedPlaylistVisible;
        }
    }

    public void TogglePlaylistVisibility()
        => _playlist.IsVisible = !_playlist.IsVisible;

    public void Cleanup()
    {
        _session.CurrentIndexChanged -= OnSessionCurrentIndexChanged;
        _playback.PropertyChanged -= _playbackPropertyChangedHandler;
    }

    private void OnSessionCurrentIndexChanged(int value)
    {
        CurrentIndex = value;
    }
}
