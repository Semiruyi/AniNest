using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AniNest.Infrastructure.Logging;

namespace AniNest.Infrastructure.Thumbnails;

internal sealed class ThumbnailCollectionCoordinator
{
    private static readonly Logger Log = AppLog.For<ThumbnailCollectionCoordinator>();

    private readonly ThumbnailTaskStore _taskStore;
    private readonly ThumbnailWorkerPool _workerPool;
    private readonly ThumbnailIndexRepository _indexRepository;
    private readonly ThumbnailWorkerCancellationCoordinator _workerCancellationCoordinator;
    private readonly Action _ensureLoopRunning;
    private readonly Action _notifyStatusChanged;
    private readonly Action _saveIndex;
    private readonly Action<ThumbnailWorkIntent, string?> _preemptLowerPriorityWorkers;
    private readonly Func<bool> _isFfmpegAvailable;
    private readonly Func<string, string> _computeMd5;

    public ThumbnailCollectionCoordinator(
        ThumbnailTaskStore taskStore,
        ThumbnailWorkerPool workerPool,
        ThumbnailIndexRepository indexRepository,
        ThumbnailWorkerCancellationCoordinator workerCancellationCoordinator,
        Action ensureLoopRunning,
        Action notifyStatusChanged,
        Action saveIndex,
        Action<ThumbnailWorkIntent, string?> preemptLowerPriorityWorkers,
        Func<bool> isFfmpegAvailable,
        Func<string, string> computeMd5)
    {
        _taskStore = taskStore;
        _workerPool = workerPool;
        _indexRepository = indexRepository;
        _workerCancellationCoordinator = workerCancellationCoordinator;
        _ensureLoopRunning = ensureLoopRunning;
        _notifyStatusChanged = notifyStatusChanged;
        _saveIndex = saveIndex;
        _preemptLowerPriorityWorkers = preemptLowerPriorityWorkers;
        _isFfmpegAvailable = isFfmpegAvailable;
        _computeMd5 = computeMd5;
    }

    public void RegisterCollection(LibraryCollectionRef collection, IReadOnlyCollection<string> videoPaths)
    {
        if (!_isFfmpegAvailable())
            return;

        _taskStore.RegisterCollection(collection, videoPaths, _computeMd5);

        Log.Info($"Thumbnail collection registered: id={collection.Id}, kind={collection.Kind}, name={collection.Name}, videos={videoPaths.Count}");
        _ensureLoopRunning();
        _notifyStatusChanged();
    }

    public void RemoveCollection(string collectionId)
        => _taskStore.RemoveCollection(collectionId);

    public void DeleteCollection(string collectionId, IReadOnlyCollection<string>? videoPaths = null)
    {
        IReadOnlyCollection<string>? members = _taskStore.GetCollectionMembers(collectionId);
        if (members is null)
            return;

        var targets = (videoPaths ?? members)
            .Where(members.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var targetSet = new HashSet<string>(targets, StringComparer.OrdinalIgnoreCase);
        var workersToCancel = _workerPool.MarkForCancellation(
            workers => workers
                .Where(worker =>
                    !worker.Execution.IsCompleted &&
                    targetSet.Contains(worker.Task.VideoPath))
                .ToList(),
            _ => $"collection-deleted:{collectionId}");
        _workerCancellationCoordinator.CancelWithReason(
            workersToCancel,
            $"collection-deleted:{collectionId}",
            $"Thumbnail collection delete cancel: id={collectionId}");

        IReadOnlyList<ThumbnailTask> removedTasks = _taskStore.DeleteForCollection(collectionId, targets);
        foreach (ThumbnailTask task in removedTasks)
            _indexRepository.DeleteTaskDirectory(task.Md5Dir);

        RemoveCollection(collectionId);
        _saveIndex();
        _notifyStatusChanged();
    }

    public void FocusCollection(string collectionId)
    {
        int promotedCount = _taskStore.ApplyIntentToCollection(collectionId, ThumbnailWorkIntent.FocusedCollection);
        bool shouldPreempt = ThumbnailWorkerPreemption.ShouldPreemptForIncomingIntent(
            _workerPool.SnapshotWorkers(),
            ThumbnailWorkIntent.FocusedCollection);

        Log.Info($"Thumbnail collection focused: id={collectionId}, promoted={promotedCount}, shouldPreempt={shouldPreempt}");
        if (shouldPreempt)
            _preemptLowerPriorityWorkers(ThumbnailWorkIntent.FocusedCollection, null);
        _ensureLoopRunning();
        _notifyStatusChanged();
    }

    public void BoostCollection(string collectionId)
    {
        int promotedCount = _taskStore.ApplyIntentToCollection(collectionId, ThumbnailWorkIntent.ManualCollection);
        bool shouldPreempt = ThumbnailWorkerPreemption.ShouldPreemptForIncomingIntent(
            _workerPool.SnapshotWorkers(),
            ThumbnailWorkIntent.ManualCollection);

        Log.Info($"Thumbnail collection boosted: id={collectionId}, promoted={promotedCount}, shouldPreempt={shouldPreempt}");
        if (shouldPreempt)
            _preemptLowerPriorityWorkers(ThumbnailWorkIntent.ManualCollection, null);
        _ensureLoopRunning();
        _notifyStatusChanged();
    }

    public void ResetCollection(string collectionId, bool boostAfterReset)
    {
        long updatedAtTicks = DateTime.UtcNow.Ticks;
        _taskStore.ResetCollection(collectionId, boostAfterReset, updatedAtTicks,
            out var thumbnailDirsToDelete, out bool changed, out bool shouldPreempt,
            out _, out _);

        foreach (string thumbnailDir in thumbnailDirsToDelete.Distinct(StringComparer.OrdinalIgnoreCase))
            _indexRepository.DeleteThumbnailDirectory(thumbnailDir);

        if (!changed)
            return;

        if (shouldPreempt)
            _preemptLowerPriorityWorkers(ThumbnailWorkIntent.ManualCollection, null);
        _saveIndex();
        _ensureLoopRunning();
        _notifyStatusChanged();
    }

}
