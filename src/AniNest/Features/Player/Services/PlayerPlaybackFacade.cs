using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Player.Services;

public sealed class PlayerPlaybackFacade : IPlayerPlaybackFacade
{
    private readonly IMediaPlayerController _media;
    private readonly IThumbnailGenerator _thumbnailGenerator;

    public PlayerPlaybackFacade(
        IMediaPlayerController media,
        IThumbnailGenerator thumbnailGenerator)
    {
        _media = media;
        _thumbnailGenerator = thumbnailGenerator;
    }

    public bool IsPlaying => _media.IsPlaying;
    public long MediaLength => _media.Length;
    public float Rate
    {
        get => _media.Rate;
        set => _media.Rate = value;
    }

    public Task InitializeAsync()
        => _media.InitializeAsync();

    public void TogglePlayPause()
        => _media.TogglePlayPause();

    public void Stop()
        => _media.Stop();

    public void SeekForward(long milliseconds)
        => _media.SeekForward(milliseconds);

    public void SeekBackward(long milliseconds)
        => _media.SeekBackward(milliseconds);

    public void SeekTo(long time)
        => _media.SeekTo(time);

    public string FormatTime(long ms)
        => MediaPlayerController.FormatTime(ms);

    public ThumbnailState GetThumbnailState(string videoPath)
        => _thumbnailGenerator.GetState(videoPath);

    public byte[]? GetThumbnailBytes(string videoPath, long positionMs)
        => _thumbnailGenerator.GetThumbnailBytes(videoPath, positionMs);
}
