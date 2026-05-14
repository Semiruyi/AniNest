using System;

namespace AniNest.Features.Player.Playback;

public sealed class PlaybackProgressChangedEventArgs : EventArgs
{
    public PlaybackProgressChangedEventArgs(long currentTime, long totalTime)
    {
        CurrentTime = currentTime;
        TotalTime = totalTime;
    }

    public long CurrentTime { get; }
    public long TotalTime { get; }
}
