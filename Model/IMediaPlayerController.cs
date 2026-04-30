using System;
using System.Windows.Media.Imaging;

namespace LocalPlayer.Model;

public interface IMediaPlayerController : IDisposable
{
    bool IsPlaying { get; }
    long Time { get; }
    long Length { get; }
    string? CurrentFilePath { get; }
    WriteableBitmap? VideoBitmap { get; }
    float Rate { get; set; }

    void Initialize();
    void Play(string filePath, long startTimeMs = 0);
    void TogglePlayPause();
    void Stop();
    void SeekForward(long milliseconds);
    void SeekBackward(long milliseconds);
    void SeekTo(long time);

    event EventHandler? Playing;
    event EventHandler? Paused;
    event EventHandler? Stopped;
    event EventHandler<ProgressUpdatedEventArgs>? ProgressUpdated;
}
