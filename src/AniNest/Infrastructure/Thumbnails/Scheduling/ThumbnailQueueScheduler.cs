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
            .OrderBy(static task => task, TaskPriorityComparer.Instance)
            .FirstOrDefault();
    }

    public static bool IsTaskOutrankedByPendingWork(
        ThumbnailTaskStore taskStore,
        ThumbnailTask task)
    {
        return taskStore.SnapshotTasks()
            .Where(static pendingTask => pendingTask.State == ThumbnailState.Pending)
            .Any(pendingTask => TaskPriorityComparer.Instance.Compare(pendingTask, task) < 0);
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

    private sealed class TaskPriorityComparer : IComparer<ThumbnailTask>
    {
        public static TaskPriorityComparer Instance { get; } = new();

        public int Compare(ThumbnailTask? x, ThumbnailTask? y)
        {
            if (ReferenceEquals(x, y))
                return 0;

            if (x == null)
                return 1;

            if (y == null)
                return -1;

            int rankComparison = ThumbnailWorkIntentPriority.GetRank(y.Intent)
                .CompareTo(ThumbnailWorkIntentPriority.GetRank(x.Intent));
            if (rankComparison != 0)
                return rankComparison;

            int updatedAtComparison = y.IntentUpdatedAtUtcTicks.CompareTo(x.IntentUpdatedAtUtcTicks);
            if (updatedAtComparison != 0)
                return updatedAtComparison;

            return StringComparer.OrdinalIgnoreCase.Compare(x.VideoPath, y.VideoPath);
        }
    }
}
