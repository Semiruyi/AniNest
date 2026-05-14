using System;
using CommunityToolkit.Mvvm.ComponentModel;
using AniNest.Features.Player.Models;
using AniNest.Features.Player.Services;
using AniNest.Infrastructure.Media;

namespace AniNest.Features.Player;

public partial class PlayerPlaybackStateController : ObservableObject
{
    private readonly IMediaPlayerController _media;
    private readonly IPlayerPlaybackStateSyncService _syncService;
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

    public long MediaLength => _media.Length;

    public PlayerPlaybackStateController(
        IMediaPlayerController media,
        IPlayerPlaybackStateSyncService syncService)
    {
        _media = media;
        _syncService = syncService;
        _syncService.Attach(this);
    }

    public void SetSeeking(bool value)
    {
        if (IsSeeking == value)
            return;

        IsSeeking = value;
    }

    public void NotifyStateChanged(string propertyName)
        => OnPropertyChanged(propertyName);

    public void Cleanup()
    {
        if (_isCleanedUp)
            return;

        _isCleanedUp = true;
        _syncService.Detach(this);
    }

    public void SetCurrentVideoPath(string path)
    {
        CurrentVideoPath = path;
    }

    public void SetPlayingState(bool value)
    {
        IsPlaying = value;
    }

    public void ResetSession()
    {
        IsPlaying = false;
        CurrentTime = 0;
        TotalTime = 0;
        CurrentTimeText = "00:00";
        TotalTimeText = "00:00";
        BufferedPosition = 0;
        IsSeeking = false;
        CurrentVideoPath = null;
    }

    public void UpdateProgress(ProgressUpdatedEventArgs args)
    {
        if (IsSeeking)
            return;

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
        CurrentTimeText = FormatTime(args.CurrentTime);
        TotalTimeText = FormatTime(args.TotalTime);
    }

    private static string FormatTime(long milliseconds)
    {
        TimeSpan time = TimeSpan.FromMilliseconds(milliseconds);
        return time.TotalHours >= 1
            ? time.ToString(@"hh\:mm\:ss")
            : time.ToString(@"mm\:ss");
    }
}
