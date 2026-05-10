using System;
using System.Threading;
using System.Threading.Tasks;
using AniNest.Infrastructure.Persistence;

namespace AniNest.Infrastructure.Thumbnails;

internal sealed class ThumbnailGeneratorComponents
{
    public required ThumbnailIndexRepository IndexRepository { get; init; }
    public required ThumbnailCacheMaintenance CacheMaintenance { get; init; }
    public required ThumbnailStatusTracker StatusTracker { get; init; }
    public required ThumbnailGenerationRunner GenerationRunner { get; init; }
    public required ThumbnailWorkerExecutionHost WorkerExecutionHost { get; init; }
    public required ThumbnailWorkerCancellationCoordinator WorkerCancellationCoordinator { get; init; }
    public required ThumbnailWorkerSuspensionCoordinator WorkerSuspensionCoordinator { get; init; }
    public required ThumbnailQueueLoopRunner QueueLoopRunner { get; init; }

    public static ThumbnailGeneratorComponents Create(
        string thumbBaseDir,
        string ffmpegPath,
        ISettingsService settings,
        IThumbnailDecodeStrategyService decodeStrategyService,
        IThumbnailProcessController? processController,
        ThumbnailTaskStore taskStore,
        ThumbnailWorkerPool workerPool,
        Task initTask,
        Func<bool> isShuttingDown,
        Func<bool> hasPending,
        Func<bool> canStartWorkers,
        Func<ThumbnailTask?> selectNextTask,
        Action<ThumbnailTask, CancellationToken> startWorker,
        Action drainCompletedWorkers,
        Action<string> reportSchedulerState,
        Func<string> getBlockedSchedulerReason,
        Func<int> getActiveWorkerCount,
        Func<Task> waitForWorkersAsync,
        Action<ThumbnailTask, ThumbnailState> setTaskState,
        Action saveIndex,
        Action<ThumbnailProgressEventArgs> onProgressChanged,
        Action onStatusChanged,
        Action<string, int>? onVideoProgress,
        Action<string>? onVideoReady,
        Func<string> buildSchedulerSnapshot,
        Func<string, double> getVideoDuration)
    {
        var indexRepository = new ThumbnailIndexRepository(thumbBaseDir);
        var cacheMaintenance = new ThumbnailCacheMaintenance(indexRepository, settings);
        var statusTracker = new ThumbnailStatusTracker(taskStore, onProgressChanged, onStatusChanged);
        var renderer = new ThumbnailRenderer(ffmpegPath, thumbBaseDir, getVideoDuration);
        var generationRunner = new ThumbnailGenerationRunner(decodeStrategyService, renderer);
        var workerExecutionHost = new ThumbnailWorkerExecutionHost(
            generationRunner,
            taskStore,
            setTaskState,
            saveIndex,
            statusTracker.UpdateProgress,
            buildSchedulerSnapshot,
            onVideoProgress,
            onVideoReady);
        var workerCancellationCoordinator = new ThumbnailWorkerCancellationCoordinator(buildSchedulerSnapshot);
        processController ??= new WindowsThumbnailProcessController();
        var workerSuspensionCoordinator = new ThumbnailWorkerSuspensionCoordinator(
            processController,
            workerCancellationCoordinator,
            buildSchedulerSnapshot);
        var queueLoopRunner = new ThumbnailQueueLoopRunner(
            () => initTask,
            isShuttingDown,
            drainCompletedWorkers,
            hasPending,
            canStartWorkers,
            selectNextTask,
            startWorker,
            reportSchedulerState,
            getBlockedSchedulerReason,
            getActiveWorkerCount,
            waitForWorkersAsync);

        return new ThumbnailGeneratorComponents
        {
            IndexRepository = indexRepository,
            CacheMaintenance = cacheMaintenance,
            StatusTracker = statusTracker,
            GenerationRunner = generationRunner,
            WorkerExecutionHost = workerExecutionHost,
            WorkerCancellationCoordinator = workerCancellationCoordinator,
            WorkerSuspensionCoordinator = workerSuspensionCoordinator,
            QueueLoopRunner = queueLoopRunner
        };
    }
}
