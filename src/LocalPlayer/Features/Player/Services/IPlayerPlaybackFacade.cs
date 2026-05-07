using LocalPlayer.Infrastructure.Thumbnails;

namespace LocalPlayer.Features.Player.Services;

public interface IPlayerPlaybackFacade
{
    bool IsPlaying { get; }
    long MediaLength { get; }
    float Rate { get; set; }

    void Initialize();
    void TogglePlayPause();
    void Stop();
    void SeekForward(long milliseconds);
    void SeekBackward(long milliseconds);
    void SeekTo(long time);
    string FormatTime(long ms);
    ThumbnailState GetThumbnailState(string videoPath);
    string? GetThumbnailPath(string videoPath, int second);
}
