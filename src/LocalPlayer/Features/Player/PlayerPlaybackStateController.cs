using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LocalPlayer.Features.Player.Models;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Media;

namespace LocalPlayer.Features.Player;

public partial class PlayerPlaybackStateController : ObservableObject
{
    private static readonly Logger Log = AppLog.For<PlayerPlaybackStateController>();

    private readonly IMediaPlayerController _media;
    private readonly Action<string> _videoPathChangedHandler;
    private readonly EventHandler _playingHandler;
    private readonly EventHandler _pausedHandler;
    private readonly EventHandler _stoppedHandler;
    private readonly EventHandler<ProgressUpdatedEventArgs> _progressUpdatedHandler;
    private bool _isCleanedUp;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private long _currentTime;

    [ObservableProperty]
    private long _totalTime;

    [ObservableProperty]
    private string _currentTimeText = "00:00";

    [ObservableProperty]
    private string _totalTimeText = "00:00";

    [ObservableProperty]
    private long _bufferedPosition;

    [ObservableProperty]
    private bool _isSeeking;

    [ObservableProperty]
    private string? _currentVideoPath;

    public ImageSource? VideoSource => _media.VideoBitmap;
    public long MediaLength => _media.Length;

    public PlayerPlaybackStateController(PlayerSessionController session, IMediaPlayerController media)
    {
        _media = media;
        _videoPathChangedHandler = OnSessionCurrentVideoPathChanged;
        _playingHandler = (_, _) => Application.Current.Dispatcher.Invoke(() => SetIsPlaying(true));
        _pausedHandler = (_, _) => Application.Current.Dispatcher.Invoke(() => SetIsPlaying(false));
        _stoppedHandler = (_, _) => Application.Current.Dispatcher.Invoke(() =>
        {
            SetIsPlaying(false);
            RefreshVideoSource();
        });
        _progressUpdatedHandler = (_, args) => Application.Current.Dispatcher.Invoke(() => OnProgressUpdated(args));

        session.CurrentVideoPathChanged += _videoPathChangedHandler;
        _media.Playing += _playingHandler;
        _media.Paused += _pausedHandler;
        _media.Stopped += _stoppedHandler;
        _media.ProgressUpdated += _progressUpdatedHandler;

        Log.Info("PlayerPlaybackStateController initialized");
        RefreshVideoSource();
    }

    public void SetSeeking(bool value)
        => IsSeeking = value;

    public void RefreshVideoSource()
        => OnPropertyChanged(nameof(VideoSource));

    public void NotifyStateChanged(string propertyName)
        => OnPropertyChanged(propertyName);

    public void Cleanup(PlayerSessionController session)
    {
        if (_isCleanedUp)
            return;

        _isCleanedUp = true;
        session.CurrentVideoPathChanged -= _videoPathChangedHandler;
        _media.Playing -= _playingHandler;
        _media.Paused -= _pausedHandler;
        _media.Stopped -= _stoppedHandler;
        _media.ProgressUpdated -= _progressUpdatedHandler;
    }

    private void OnSessionCurrentVideoPathChanged(string path)
    {
        CurrentVideoPath = path;
        RefreshVideoSource();
    }

    private void SetIsPlaying(bool value)
    {
        IsPlaying = value;
        RefreshVideoSource();
    }

    private void OnProgressUpdated(ProgressUpdatedEventArgs args)
    {
        if (IsSeeking) return;

        if (args.CurrentTime == 0 && IsPlaying)
        {
            if (CurrentTime > 0)
                OnPropertyChanged(nameof(CurrentTime));
            TotalTime = args.TotalTime;
            return;
        }

        CurrentTime = args.CurrentTime;
        TotalTime = args.TotalTime;
        BufferedPosition = args.TotalTime;
        CurrentTimeText = MediaPlayerController.FormatTime(args.CurrentTime);
        TotalTimeText = MediaPlayerController.FormatTime(args.TotalTime);
    }
}
