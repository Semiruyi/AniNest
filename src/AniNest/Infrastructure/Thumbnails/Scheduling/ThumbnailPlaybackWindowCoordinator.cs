using System;
using System.Collections.Generic;
using System.IO;

namespace AniNest.Infrastructure.Thumbnails;

internal static class ThumbnailPlaybackWindowCoordinator
{
    public static ThumbnailPlaybackWindowUpdate Apply(
        ThumbnailTaskStore taskStore,
        IReadOnlyCollection<ThumbnailGeneratorWorker> activeWorkers,
        IReadOnlyList<string> orderedVideoPaths,
        int currentIndex,
        int lookaheadCount,
        long updatedAtTicks)
    {
        string currentVideoPath = orderedVideoPaths[currentIndex];
        IntentApplyOutcome currentOutcome = IntentApplyOutcome.MissingTask;
        int nearbyApplied = 0;
        int nearbyReady = 0;
        int nearbyHigherIntent = 0;
        int nearbyMissing = 0;
        string? keepPlaybackWorkerVideoPath = null;

        int start = currentIndex;
        int end = Math.Min(orderedVideoPaths.Count - 1, currentIndex + Math.Max(0, lookaheadCount));
        List<string> candidateWindow = [];
        for (int i = start; i <= end; i++)
        {
            string videoPath = orderedVideoPaths[i];
            if (!taskStore.TryGetTask(videoPath, out var task))
            {
                nearbyMissing++;
                continue;
            }

            if (task.State == ThumbnailState.Ready)
            {
                nearbyReady++;
                continue;
            }

            candidateWindow.Add($"{i}:{Path.GetFileName(videoPath)}{(i == currentIndex ? "*" : "")}");
            keepPlaybackWorkerVideoPath ??= videoPath;

            IntentApplyOutcome outcome = i == currentIndex
                ? taskStore.ApplyIntentToVideo(videoPath, ThumbnailWorkIntent.PlaybackCurrent, task.SourceCollectionId, updatedAtTicks)
                : taskStore.ApplyIntentToVideo(videoPath, ThumbnailWorkIntent.PlaybackNearby, task.SourceCollectionId, updatedAtTicks);

            if (i == currentIndex)
                currentOutcome = outcome;

            switch (outcome)
            {
                case IntentApplyOutcome.Applied:
                    nearbyApplied++;
                    break;
                case IntentApplyOutcome.AlreadyReady:
                    nearbyReady++;
                    break;
                case IntentApplyOutcome.HigherIntentAlreadyPresent:
                    nearbyHigherIntent++;
                    break;
                default:
                    nearbyMissing++;
                    break;
            }
        }

        if (currentOutcome == IntentApplyOutcome.MissingTask &&
            taskStore.TryGetTask(currentVideoPath, out var currentTask) &&
            currentTask.State == ThumbnailState.Ready)
        {
            currentOutcome = IntentApplyOutcome.AlreadyReady;
        }

        taskStore.CurrentForegroundTargetVideoPath = currentVideoPath;
        taskStore.CurrentForegroundTargetIntent = ThumbnailWorkIntent.PlaybackCurrent.ToString();

        int stalePlaybackWorkers = ThumbnailWorkerPreemption.CountStalePlaybackWorkers(
            activeWorkers,
            currentVideoPath,
            keepPlaybackWorkerVideoPath);

        bool shouldPrioritizeCurrentWorker =
            currentOutcome is IntentApplyOutcome.Applied or IntentApplyOutcome.HigherIntentAlreadyPresent;

        return new ThumbnailPlaybackWindowUpdate
        {
            CurrentVideoPath = currentVideoPath,
            CurrentOutcome = currentOutcome,
            KeepPlaybackWorkerVideoPath = keepPlaybackWorkerVideoPath,
            CandidateWindowSummary = candidateWindow.Count == 0 ? "-" : string.Join(", ", candidateWindow),
            NearbyApplied = nearbyApplied,
            NearbyReady = nearbyReady,
            NearbyHigherIntent = nearbyHigherIntent,
            NearbyMissing = nearbyMissing,
            StalePlaybackWorkers = stalePlaybackWorkers,
            ShouldPreemptLowerPriority = shouldPrioritizeCurrentWorker &&
                stalePlaybackWorkers == 0 &&
                ThumbnailWorkerPreemption.ShouldPreemptForIncomingIntent(activeWorkers, ThumbnailWorkIntent.PlaybackCurrent)
        };
    }
}
