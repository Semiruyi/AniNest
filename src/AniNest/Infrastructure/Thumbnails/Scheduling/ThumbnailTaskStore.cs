using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AniNest.Infrastructure.Paths;

namespace AniNest.Infrastructure.Thumbnails;

internal sealed class ThumbnailTaskStore
{
    private readonly object _lock = new();
    private readonly List<ThumbnailTask> _tasks = new();
    private readonly Dictionary<string, ThumbnailTask> _videoToTask = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _collectionToVideos = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _videoToCollections = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LibraryCollectionRef> _collections = new(StringComparer.OrdinalIgnoreCase);

    private int _readyCount;
    private int _totalCount;
    private string? _currentForegroundTargetVideoPath;
    private string? _currentForegroundTargetIntent;

    public int ReadyCount
    {
        get { lock (_lock) return _readyCount; }
    }

    public int TotalCount
    {
        get { lock (_lock) return _totalCount; }
    }

    public string? CurrentForegroundTargetVideoPath
    {
        get { lock (_lock) return _currentForegroundTargetVideoPath; }
        set { lock (_lock) _currentForegroundTargetVideoPath = value; }
    }

    public string? CurrentForegroundTargetIntent
    {
        get { lock (_lock) return _currentForegroundTargetIntent; }
        set { lock (_lock) _currentForegroundTargetIntent = value; }
    }

    public ThumbnailGenerationStatusSnapshot CreateSnapshot(
        bool isPaused,
        bool isPlayerActive,
        int activeWorkers)
    {
        lock (_lock)
        {
            return new ThumbnailGenerationStatusSnapshot(
                isPaused,
                isPlayerActive,
                activeWorkers,
                _readyCount,
                _totalCount,
                CountTasksByStateUnsafe(ThumbnailState.Pending),
                CountForegroundPendingUnsafe(),
                GetCurrentForegroundTargetNameUnsafe(),
                _currentForegroundTargetIntent);
        }
    }

    public void RegisterCollection(LibraryCollectionRef collection, IReadOnlyCollection<string> videoPaths, Func<string, string> computeMd5)
    {
        lock (_lock)
            RegisterCollectionUnsafe(collection, videoPaths, computeMd5);
    }

    public void RemoveCollection(string collectionId)
    {
        lock (_lock)
        {
            if (!_collectionToVideos.TryGetValue(collectionId, out var members))
                return;

            foreach (string videoPath in members)
            {
                if (_videoToCollections.TryGetValue(videoPath, out var collectionIds))
                {
                    collectionIds.Remove(collectionId);
                    if (collectionIds.Count == 0)
                        _videoToCollections.Remove(videoPath);
                }
            }

            _collectionToVideos.Remove(collectionId);
            _collections.Remove(collectionId);
        }
    }

    public int ApplyIntentToCollection(string collectionId, ThumbnailWorkIntent intent)
    {
        lock (_lock)
            return ApplyIntentToCollectionUnsafe(collectionId, intent);
    }

    public IntentApplyOutcome ApplyIntentToVideo(string videoPath, ThumbnailWorkIntent intent, string? sourceCollectionId, long updatedAtTicks)
    {
        lock (_lock)
        {
            if (_videoToTask.TryGetValue(videoPath, out var task))
                return ApplyIntentUnsafe(task, intent, sourceCollectionId, updatedAtTicks);

            return IntentApplyOutcome.MissingTask;
        }
    }

    public bool TryGetTask(string videoPath, out ThumbnailTask task)
    {
        lock (_lock)
            return _videoToTask.TryGetValue(videoPath, out task!);
    }

    public void SetTaskState(ThumbnailTask task, ThumbnailState newState)
    {
        lock (_lock)
            SetTaskStateUnsafe(task, newState);
    }

    public ThumbnailState GetState(string videoPath)
    {
        lock (_lock)
            return _videoToTask.TryGetValue(videoPath, out var task) ? task.State : ThumbnailState.Pending;
    }

    public ThumbnailWorkIntent? GetIntent(string videoPath)
    {
        lock (_lock)
            return _videoToTask.TryGetValue(videoPath, out var task) ? task.Intent : null;
    }

    public IReadOnlyList<string> GetTaskVideoPathsInOrder()
    {
        lock (_lock)
        {
            return _tasks
                .OrderByDescending(static task => ThumbnailWorkIntentPriority.GetRank(task.Intent))
                .ThenByDescending(static task => task.IntentUpdatedAtUtcTicks)
                .ThenBy(static task => task.VideoPath, StringComparer.OrdinalIgnoreCase)
                .Select(static task => task.VideoPath)
                .ToArray();
        }
    }

    public int CountTasksByState(ThumbnailState state)
    {
        lock (_lock)
            return CountTasksByStateUnsafe(state);
    }

    public int CountForegroundPending()
    {
        lock (_lock)
            return CountForegroundPendingUnsafe();
    }

    public int DemotePlaybackIntents()
    {
        lock (_lock)
        {
            int demoted = 0;
            foreach (var task in _tasks)
            {
                if (task.Intent is not (ThumbnailWorkIntent.PlaybackCurrent or ThumbnailWorkIntent.PlaybackNearby))
                    continue;

                task.Intent = ThumbnailWorkIntent.BackgroundFill;
                task.IntentUpdatedAtUtcTicks = 0;
                demoted++;
            }

            return demoted;
        }
    }

    public int DemotePlaybackIntentsOutside(IReadOnlyCollection<string> retainedVideoPaths)
    {
        lock (_lock)
        {
            var retained = new HashSet<string>(retainedVideoPaths, StringComparer.OrdinalIgnoreCase);
            int demoted = 0;
            foreach (var task in _tasks)
            {
                if (task.Intent is not (ThumbnailWorkIntent.PlaybackCurrent or ThumbnailWorkIntent.PlaybackNearby))
                    continue;

                if (retained.Contains(task.VideoPath))
                    continue;

                task.Intent = ThumbnailWorkIntent.BackgroundFill;
                task.IntentUpdatedAtUtcTicks = 0;
                demoted++;
            }

            return demoted;
        }
    }

    public void ClearPlaybackForegroundTarget()
    {
        lock (_lock)
        {
            _currentForegroundTargetVideoPath = null;
            _currentForegroundTargetIntent = null;
        }
    }

    public void ForceTaskState(string videoPath, ThumbnailState state)
    {
        lock (_lock)
        {
            if (_videoToTask.TryGetValue(videoPath, out var task))
                SetTaskStateUnsafe(task, state);
        }
    }

    public bool TryRequeueTask(string videoPath)
    {
        lock (_lock)
        {
            if (_videoToTask.TryGetValue(videoPath, out var task) &&
                task.State is ThumbnailState.Generating or ThumbnailState.PausedGenerating)
            {
                SetTaskStateUnsafe(task, ThumbnailState.Pending);
                return true;
            }

            return false;
        }
    }

    public IReadOnlyList<ThumbnailTask> SnapshotTasks()
    {
        lock (_lock)
            return _tasks.ToArray();
    }

    public IReadOnlyCollection<string>? GetCollectionMembers(string collectionId)
    {
        lock (_lock)
            return _collectionToVideos.TryGetValue(collectionId, out var members) ? members.ToArray() : null;
    }

    public void ResetCollection(string collectionId, bool boostAfterReset, long updatedAtTicks,
        out List<string> thumbnailDirsToDelete, out bool changed, out bool shouldPreempt,
        out string? foregroundTargetVideoPath, out string? foregroundTargetIntent)
    {
        thumbnailDirsToDelete = [];
        changed = false;
        shouldPreempt = false;
        foregroundTargetVideoPath = null;
        foregroundTargetIntent = null;

        lock (_lock)
        {
            if (!_collectionToVideos.TryGetValue(collectionId, out var members))
                return;

            foreach (string videoPath in members)
            {
                if (!_videoToTask.TryGetValue(videoPath, out var task))
                    continue;

                if (task.State == ThumbnailState.Ready)
                    _readyCount--;

                task.State = ThumbnailState.Pending;
                task.TotalFrames = 0;
                task.MarkedForDeletionAt = 0;
                task.Intent = boostAfterReset ? ThumbnailWorkIntent.ManualCollection : ThumbnailWorkIntent.BackgroundFill;
                task.IntentUpdatedAtUtcTicks = boostAfterReset ? updatedAtTicks : 0;
                task.SourceCollectionId = collectionId;
                thumbnailDirsToDelete.Add(Path.Combine(AppPaths.ThumbnailDirectory, task.Md5Dir));
                changed = true;
            }

            if (boostAfterReset && members.Count > 0)
            {
                foregroundTargetVideoPath = members.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
                foregroundTargetIntent = ThumbnailWorkIntent.ManualCollection.ToString();
                _currentForegroundTargetVideoPath = foregroundTargetVideoPath;
                _currentForegroundTargetIntent = foregroundTargetIntent;
                shouldPreempt = true;
            }
        }
    }

    public void DeleteForCollection(string collectionId, IReadOnlyCollection<string>? videoPaths = null)
    {
        lock (_lock)
        {
            if (!_collectionToVideos.TryGetValue(collectionId, out var members))
                return;

            IReadOnlyCollection<string> targets = videoPaths ?? members.ToArray();
            foreach (string videoPath in targets)
            {
                if (members.Contains(videoPath))
                    MarkForDeletionUnsafe(videoPath);
            }
        }
    }

    public void MergeLoadedTasks(IReadOnlyList<ThumbnailTask> tasks)
    {
        lock (_lock)
        {
            foreach (var task in tasks)
            {
                if (_videoToTask.TryGetValue(task.VideoPath, out var existing))
                {
                    SetTaskStateUnsafe(existing, task.State);
                    existing.Md5Dir = task.Md5Dir;
                    existing.TotalFrames = task.TotalFrames;
                    existing.MarkedForDeletionAt = task.MarkedForDeletionAt;
                    continue;
                }

                _tasks.Add(task);
                task.Intent = ThumbnailWorkIntent.BackgroundFill;
                task.IntentUpdatedAtUtcTicks = 0;
                _videoToTask[task.VideoPath] = task;
                _totalCount++;
                if (task.State == ThumbnailState.Ready)
                    _readyCount++;
            }
        }
    }

    public void RemoveTasks(IReadOnlyCollection<ThumbnailTask> tasks)
    {
        lock (_lock)
        {
            foreach (var task in tasks)
            {
                _tasks.Remove(task);
                _videoToTask.Remove(task.VideoPath);
                if (task.State == ThumbnailState.Ready)
                    _readyCount--;
                _totalCount--;
            }
        }
    }

    public bool TryRegisterVideo(string videoPath, Func<string, string> computeMd5, out bool revived)
    {
        lock (_lock)
            return TryRegisterVideoUnsafe(videoPath, computeMd5, out revived);
    }

    private void RegisterCollectionUnsafe(LibraryCollectionRef collection, IReadOnlyCollection<string> videoPaths, Func<string, string> computeMd5)
    {
        _collections[collection.Id] = collection;

        if (_collectionToVideos.TryGetValue(collection.Id, out var previousMembers))
        {
            foreach (string videoPath in previousMembers)
            {
                if (_videoToCollections.TryGetValue(videoPath, out var collectionIds))
                {
                    collectionIds.Remove(collection.Id);
                    if (collectionIds.Count == 0)
                        _videoToCollections.Remove(videoPath);
                }
            }
        }

        var members = new HashSet<string>(videoPaths, StringComparer.OrdinalIgnoreCase);
        _collectionToVideos[collection.Id] = members;

        foreach (string videoPath in members)
        {
            TryRegisterVideoUnsafe(videoPath, computeMd5, out _);

            if (_videoToTask.TryGetValue(videoPath, out var task))
                task.SourceCollectionId ??= collection.Id;

            if (!_videoToCollections.TryGetValue(videoPath, out var collectionIds))
            {
                collectionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _videoToCollections[videoPath] = collectionIds;
            }

            collectionIds.Add(collection.Id);
        }
    }

    private bool TryRegisterVideoUnsafe(string videoPath, Func<string, string> computeMd5, out bool revived)
    {
        revived = false;

        if (_videoToTask.TryGetValue(videoPath, out var existing))
        {
            if (existing.MarkedForDeletionAt != 0)
            {
                existing.MarkedForDeletionAt = 0;
                revived = true;
            }

            return false;
        }

        var task = new ThumbnailTask
        {
            VideoPath = videoPath,
            Md5Dir = computeMd5(videoPath),
            State = ThumbnailState.Pending,
            Intent = ThumbnailWorkIntent.BackgroundFill,
            IntentUpdatedAtUtcTicks = 0
        };

        _tasks.Add(task);
        _videoToTask[videoPath] = task;
        _totalCount++;
        return true;
    }

    private int ApplyIntentToCollectionUnsafe(string collectionId, ThumbnailWorkIntent intent)
    {
        if (!_collectionToVideos.TryGetValue(collectionId, out var members))
            return 0;

        long updatedAtTicks = DateTime.UtcNow.Ticks;
        int applied = 0;
        foreach (string videoPath in members)
        {
            if (_videoToTask.TryGetValue(videoPath, out var task))
            {
                var outcome = ApplyIntentUnsafe(task, intent, collectionId, updatedAtTicks);
                if (outcome == IntentApplyOutcome.Applied)
                    applied++;
            }
        }

        return applied;
    }

    private IntentApplyOutcome ApplyIntentUnsafe(ThumbnailTask task, ThumbnailWorkIntent intent, string? sourceCollectionId, long updatedAtTicks)
    {
        if (task.State == ThumbnailState.Ready)
            return IntentApplyOutcome.AlreadyReady;

        if (ThumbnailWorkIntentPriority.GetRank(intent) < ThumbnailWorkIntentPriority.GetRank(task.Intent))
            return IntentApplyOutcome.HigherIntentAlreadyPresent;

        task.Intent = intent;
        task.SourceCollectionId = sourceCollectionId ?? task.SourceCollectionId;
        task.IntentUpdatedAtUtcTicks = updatedAtTicks;
        return IntentApplyOutcome.Applied;
    }

    private void SetTaskStateUnsafe(ThumbnailTask task, ThumbnailState newState)
    {
        if (task.State == newState)
            return;

        if (task.State == ThumbnailState.Ready && newState != ThumbnailState.Ready)
            _readyCount--;

        task.State = newState;

        if (newState == ThumbnailState.Ready)
            _readyCount++;
    }

    private void MarkForDeletionUnsafe(string videoPath)
    {
        if (_videoToTask.TryGetValue(videoPath, out var task) &&
            task.State == ThumbnailState.Ready &&
            task.MarkedForDeletionAt == 0)
        {
            task.MarkedForDeletionAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }

    private int CountTasksByStateUnsafe(ThumbnailState state)
        => _tasks.Count(task => task.State == state);

    private int CountForegroundPendingUnsafe()
        => _tasks.Count(task =>
            task.State == ThumbnailState.Pending &&
            task.Intent != ThumbnailWorkIntent.BackgroundFill);

    private string? GetCurrentForegroundTargetNameUnsafe()
        => string.IsNullOrWhiteSpace(_currentForegroundTargetVideoPath)
            ? null
            : Path.GetFileName(_currentForegroundTargetVideoPath);
}
