using System;

namespace AniNest.Infrastructure.Media;

public class ProgressUpdatedEventArgs : EventArgs
{
    public long CurrentTime { get; }
    public long TotalTime { get; }

    public ProgressUpdatedEventArgs(long currentTime, long totalTime)
    {
        CurrentTime = currentTime;
        TotalTime = totalTime;
    }
}
