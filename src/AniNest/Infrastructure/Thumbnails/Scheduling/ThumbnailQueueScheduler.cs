using System.Linq;

namespace AniNest.Infrastructure.Thumbnails;

internal static class ThumbnailQueueScheduler
{
    public static ThumbnailTask? SelectNextTask(
        ThumbnailTaskStore taskStore,
        ThumbnailWorkerPool workerPool,
        bool isGenerationPaused,
        ThumbnailPerformanceMode performanceMode,
        bool isPlayerActive)
    {
        if (!CanStartMoreWorkers(workerPool, isGenerationPaused, performanceMode, isPlayerActive))
            return null;

        return taskStore.SnapshotTasks()
            .Where(static task => task.State == ThumbnailState.Pending)
            .OrderByDescending(static task => ThumbnailWorkIntentPriority.GetRank(task.Intent))
            .ThenByDescending(static task => task.IntentUpdatedAtUtcTicks)
            .ThenBy(static task => task.VideoPath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public static bool CanStartMoreWorkers(
        ThumbnailWorkerPool workerPool,
        bool isGenerationPaused,
        ThumbnailPerformanceMode performanceMode,
        bool isPlayerActive)
    {
        if (isGenerationPaused)
            return false;

        ThumbnailExecutionPolicy policy = ThumbnailPerformancePolicy.Create(performanceMode, isPlayerActive);
        if (!policy.AllowStartNewJobs)
            return false;

        return workerPool.Count < policy.MaxConcurrency;
    }

    public static string BuildSnapshot(
        ThumbnailWorkerPool workerPool,
        ThumbnailTaskStore taskStore,
        bool isGenerationPaused,
        bool isPlayerActive,
        ThumbnailPerformanceMode performanceMode)
    {
        ThumbnailExecutionPolicy policy = ThumbnailPerformancePolicy.Create(performanceMode, isPlayerActive);
        return
            $"playerActive={isPlayerActive}, mode={performanceMode}, paused={isGenerationPaused}, maxConcurrency={policy.MaxConcurrency}, " +
            $"allowStartNewJobs={policy.AllowStartNewJobs}, activeWorkers={workerPool.Count}, " +
            $"pendingTasks={taskStore.CountTasksByState(ThumbnailState.Pending)}, ready={taskStore.ReadyCount}, total={taskStore.TotalCount}, " +
            $"foregroundPending={taskStore.CountForegroundPending()}";
    }

    public static string GetBlockedReason(
        ThumbnailWorkerPool workerPool,
        bool isGenerationPaused,
        ThumbnailPerformanceMode performanceMode,
        bool isPlayerActive)
    {
        if (isGenerationPaused)
            return "blocked-generation-paused";

        ThumbnailExecutionPolicy policy = ThumbnailPerformancePolicy.Create(performanceMode, isPlayerActive);
        if (!policy.AllowStartNewJobs)
            return isPlayerActive
                ? "blocked-player-active-no-new-jobs"
                : "blocked-policy-no-new-jobs";

        if (workerPool.Count >= policy.MaxConcurrency)
            return "blocked-max-concurrency";

        return "blocked-unknown";
    }
}
