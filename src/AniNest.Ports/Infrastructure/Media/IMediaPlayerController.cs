using System;
using System.Threading.Tasks;

namespace AniNest.Infrastructure.Media;

public interface IMediaPlayerController : IDisposable
{
    bool IsPlaying { get; }
    long Time { get; }
    long Length { get; }
    string? CurrentFilePath { get; }
    float Rate { get; set; }
    int Volume { get; set; }
    bool IsMuted { get; set; }

    Task InitializeAsync();
    Task WarmupAsync();
    bool TryPlay(string filePath, long startTimeMs, out string? errorMessage);
    void TogglePlayPause();
    void Stop();
    void ResetSession();
    void SeekForward(long milliseconds);
    void SeekBackward(long milliseconds);
    void SeekTo(long time);

    event EventHandler? Playing;
    event EventHandler? Paused;
    event EventHandler? Stopped;
    event EventHandler<ProgressUpdatedEventArgs>? ProgressUpdated;
}
