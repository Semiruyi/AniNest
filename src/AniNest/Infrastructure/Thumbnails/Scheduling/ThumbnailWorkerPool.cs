using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AniNest.Infrastructure.Thumbnails;

internal sealed class ThumbnailWorkerPool
{
    private readonly object _lock = new();
    private readonly List<ThumbnailGeneratorWorker> _activeWorkers = new();

    public int Count
    {
        get { lock (_lock) return _activeWorkers.Count; }
    }

    public ThumbnailGeneratorWorker[] SnapshotWorkers()
    {
        lock (_lock)
            return _activeWorkers.ToArray();
    }

    public void Add(ThumbnailGeneratorWorker worker)
    {
        lock (_lock)
            _activeWorkers.Add(worker);
    }

    public bool DrainCompletedWorkers()
    {
        bool changed = false;

        lock (_lock)
        {
            for (int i = _activeWorkers.Count - 1; i >= 0; i--)
            {
                if (!_activeWorkers[i].Execution.IsCompleted)
                    continue;

                _activeWorkers[i].Cancellation.Dispose();
                _activeWorkers.RemoveAt(i);
                changed = true;
            }
        }

        return changed;
    }

    public Task[] SnapshotExecutionTasks()
    {
        lock (_lock)
            return _activeWorkers.Select(static worker => worker.Execution).ToArray();
    }

    public bool IsCancellationRequested(string videoPath)
    {
        lock (_lock)
        {
            var worker = _activeWorkers.FirstOrDefault(activeWorker =>
                string.Equals(activeWorker.Task.VideoPath, videoPath, StringComparison.OrdinalIgnoreCase));
            return worker?.Cancellation.IsCancellationRequested ?? false;
        }
    }

    public void AddTestWorker(ThumbnailTask task, string cancellationReason, int? processId = null)
    {
        lock (_lock)
        {
            _activeWorkers.Add(new ThumbnailGeneratorWorker
            {
                Task = task,
                Execution = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task,
                Cancellation = new CancellationTokenSource(),
                CancellationReason = cancellationReason,
                ProcessId = processId
            });
        }
    }

    public List<ThumbnailGeneratorWorker> MarkAllActiveForCancellation(string reason)
    {
        lock (_lock)
        {
            var workers = _activeWorkers
                .Where(static worker => !worker.Execution.IsCompleted)
                .ToList();

            foreach (var worker in workers)
                worker.CancellationReason = reason;

            return workers;
        }
    }

    public List<ThumbnailGeneratorWorker> MarkForCancellation(
        Func<IReadOnlyCollection<ThumbnailGeneratorWorker>, List<ThumbnailGeneratorWorker>> selector,
        Func<ThumbnailGeneratorWorker, string?> reasonFactory)
    {
        lock (_lock)
        {
            var workers = selector(_activeWorkers);
            foreach (var worker in workers)
                worker.CancellationReason = reasonFactory(worker);

            return workers;
        }
    }

    public List<ThumbnailGeneratorWorker> MarkAllForShutdown()
    {
        lock (_lock)
        {
            var workers = _activeWorkers.ToList();
            foreach (var worker in workers)
                worker.CancellationReason ??= "shutdown";

            return workers;
        }
    }

    public bool IsSuspended(string videoPath)
    {
        lock (_lock)
        {
            var worker = _activeWorkers.FirstOrDefault(activeWorker =>
                string.Equals(activeWorker.Task.VideoPath, videoPath, StringComparison.OrdinalIgnoreCase));
            return worker?.IsSuspended ?? false;
        }
    }
}
