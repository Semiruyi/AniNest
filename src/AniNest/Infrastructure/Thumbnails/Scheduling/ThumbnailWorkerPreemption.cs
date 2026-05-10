using System;
using System.Collections.Generic;
using System.Linq;

namespace AniNest.Infrastructure.Thumbnails;

internal static class ThumbnailWorkerPreemption
{
    public static bool ShouldPreemptForIncomingIntent(
        IReadOnlyCollection<ThumbnailGeneratorWorker> activeWorkers,
        ThumbnailWorkIntent incomingIntent,
        string? protectedVideoPath = null)
    {
        int incomingRank = ThumbnailWorkIntentPriority.GetRank(incomingIntent);
        foreach (var worker in activeWorkers)
        {
            if (worker.Execution.IsCompleted)
                continue;

            if (!string.IsNullOrWhiteSpace(protectedVideoPath) &&
                string.Equals(worker.Task.VideoPath, protectedVideoPath, StringComparison.OrdinalIgnoreCase))
                continue;

            if (ThumbnailWorkIntentPriority.GetRank(worker.Task.Intent) < incomingRank)
                return true;
        }

        return false;
    }

    public static int CountStalePlaybackWorkers(
        IReadOnlyCollection<ThumbnailGeneratorWorker> activeWorkers,
        string currentVideoPath,
        string? keepPlaybackWorkerVideoPath = null)
        => activeWorkers.Count(worker =>
            !worker.Execution.IsCompleted &&
            ThumbnailWorkIntentPriority.IsPlaybackIntent(worker.Task.Intent) &&
            !string.Equals(worker.Task.VideoPath, currentVideoPath, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(keepPlaybackWorkerVideoPath) ||
             !string.Equals(worker.Task.VideoPath, keepPlaybackWorkerVideoPath, StringComparison.OrdinalIgnoreCase)));

    public static List<ThumbnailGeneratorWorker> SelectLowerPriorityWorkers(
        IReadOnlyCollection<ThumbnailGeneratorWorker> activeWorkers,
        ThumbnailWorkIntent incomingIntent,
        string? protectedVideoPath = null)
    {
        int incomingRank = ThumbnailWorkIntentPriority.GetRank(incomingIntent);
        return activeWorkers
            .Where(static worker => !worker.Execution.IsCompleted)
            .Where(worker => string.IsNullOrWhiteSpace(protectedVideoPath) ||
                !string.Equals(worker.Task.VideoPath, protectedVideoPath, StringComparison.OrdinalIgnoreCase))
            .Where(worker => ThumbnailWorkIntentPriority.GetRank(worker.Task.Intent) < incomingRank)
            .ToList();
    }

    public static List<ThumbnailGeneratorWorker> SelectStalePlaybackWorkers(
        IReadOnlyCollection<ThumbnailGeneratorWorker> activeWorkers,
        string currentVideoPath,
        string? keepPlaybackWorkerVideoPath)
    {
        return activeWorkers
            .Where(static worker => !worker.Execution.IsCompleted)
            .Where(worker => ThumbnailWorkIntentPriority.IsPlaybackIntent(worker.Task.Intent))
            .Where(worker => !string.Equals(worker.Task.VideoPath, currentVideoPath, StringComparison.OrdinalIgnoreCase))
            .Where(worker => string.IsNullOrWhiteSpace(keepPlaybackWorkerVideoPath) ||
                !string.Equals(worker.Task.VideoPath, keepPlaybackWorkerVideoPath, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
