using System;
using System.Threading.Tasks;

namespace AniNest.Features.Player.Playback;

public interface IPlaybackEngine : IDisposable
{
    PlaybackStateSnapshot State { get; }

    Task InitializeAsync();
    Task WarmupAsync();
    bool TryLoad(string filePath, long startTimeMs, out string? errorMessage);
    void TogglePlayPause();
    void Stop();
    void ResetSession();
    void SeekForward(long milliseconds);
    void SeekBackward(long milliseconds);
    void SeekTo(long time);
    void SetRate(float rate);
    void SetVolume(int volume);
    void SetMute(bool isMuted);

    event EventHandler? Playing;
    event EventHandler? Paused;
    event EventHandler? Stopped;
    event EventHandler<PlaybackProgressChangedEventArgs>? ProgressChanged;
}
