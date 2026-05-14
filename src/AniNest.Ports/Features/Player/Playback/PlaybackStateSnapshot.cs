namespace AniNest.Features.Player.Playback;

public readonly record struct PlaybackStateSnapshot(
    bool IsPlaying,
    long CurrentTime,
    long TotalTime,
    string? CurrentFilePath,
    float Rate,
    int Volume,
    bool IsMuted);
