using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Paths;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Thumbnails;
using AniNest.Infrastructure.Interop;

namespace AniNest.Infrastructure.Media;

public interface IMediaPlayerController : IDisposable
{
    bool IsPlaying { get; }
    long Time { get; }
    long Length { get; }
    string? CurrentFilePath { get; }
    WriteableBitmap? VideoBitmap { get; }
    float Rate { get; set; }

    void Initialize();
    Task WarmupAsync();
    void Play(string filePath, long startTimeMs = 0);
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
