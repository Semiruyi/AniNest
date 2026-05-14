using System;
using AniNest.Features.Player.Playback;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Player.Services;

public sealed class PlayerPlaybackFacade : IPlayerPlaybackFacade
{
    private readonly IPlaybackEngine _playbackEngine;
    private readonly IThumbnailGenerator _thumbnailGenerator;

    public PlayerPlaybackFacade(
        IPlaybackEngine playbackEngine,
        IThumbnailGenerator thumbnailGenerator)
    {
        _playbackEngine = playbackEngine;
        _thumbnailGenerator = thumbnailGenerator;
    }

    public bool IsPlaying => _playbackEngine.State.IsPlaying;
    public long MediaLength => _playbackEngine.State.TotalTime;
    public float Rate
    {
        get => _playbackEngine.State.Rate;
        set => _playbackEngine.SetRate(value);
    }

    public int Volume
    {
        get => _playbackEngine.State.Volume;
        set => _playbackEngine.SetVolume(value);
    }

    public bool IsMuted
    {
        get => _playbackEngine.State.IsMuted;
        set => _playbackEngine.SetMute(value);
    }

    public Task InitializeAsync()
        => _playbackEngine.InitializeAsync();

    public void TogglePlayPause()
        => _playbackEngine.TogglePlayPause();

    public void Stop()
        => _playbackEngine.Stop();

    public void SeekForward(long milliseconds)
        => _playbackEngine.SeekForward(milliseconds);

    public void SeekBackward(long milliseconds)
        => _playbackEngine.SeekBackward(milliseconds);

    public void SeekTo(long time)
        => _playbackEngine.SeekTo(time);

    public string FormatTime(long ms)
    {
        TimeSpan time = TimeSpan.FromMilliseconds(ms);
        return time.TotalHours >= 1
            ? time.ToString(@"hh\:mm\:ss")
            : time.ToString(@"mm\:ss");
    }

    public ThumbnailState GetThumbnailState(string videoPath)
        => _thumbnailGenerator.GetThumbnailState(videoPath);

    public byte[]? GetThumbnailBytes(string videoPath, long positionMs)
        => _thumbnailGenerator.GetThumbnailBytes(videoPath, positionMs);
}
