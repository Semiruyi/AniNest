using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Player.Services;

public interface IPlayerPlaybackFacade
{
    bool IsPlaying { get; }
    long MediaLength { get; }
    float Rate { get; set; }

    Task InitializeAsync();
    void TogglePlayPause();
    void Stop();
    void SeekForward(long milliseconds);
    void SeekBackward(long milliseconds);
    void SeekTo(long time);
    string FormatTime(long ms);
    ThumbnailState GetThumbnailState(string videoPath);
    byte[]? GetThumbnailBytes(string videoPath, long positionMs);
}
