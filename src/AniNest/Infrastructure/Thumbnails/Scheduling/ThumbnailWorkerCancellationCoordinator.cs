using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AniNest.Infrastructure.Logging;

namespace AniNest.Infrastructure.Thumbnails;

internal sealed class ThumbnailWorkerCancellationCoordinator
{
    private static readonly Logger Log = AppLog.For<ThumbnailWorkerCancellationCoordinator>();

    private readonly Func<string> _buildSnapshot;

    public ThumbnailWorkerCancellationCoordinator(Func<string> buildSnapshot)
    {
        _buildSnapshot = buildSnapshot;
    }

    public void CancelWithReason(
        IReadOnlyCollection<ThumbnailGeneratorWorker> workers,
        string reason,
        string logPrefix)
    {
        if (workers.Count == 0)
            return;

        string snapshot = _buildSnapshot();
        string files = string.Join(", ", workers.Select(worker => Path.GetFileName(worker.Task.VideoPath)));
        Log.Info($"{logPrefix}: reason={reason}, workers={workers.Count}, files=[{files}], {snapshot}");

        foreach (var worker in workers)
        {
            try
            {
                worker.CancellationReason = reason;
                worker.Cancellation.Cancel();
            }
            catch
            {
            }
        }
    }

    public void CancelWithComputedReasons(
        IReadOnlyCollection<ThumbnailGeneratorWorker> workers,
        Func<ThumbnailGeneratorWorker, string> reasonFactory,
        string logPrefix)
    {
        if (workers.Count == 0)
            return;

        string snapshot = _buildSnapshot();
        string files = string.Join(", ", workers.Select(worker => $"{Path.GetFileName(worker.Task.VideoPath)}:{worker.Task.Intent}"));
        Log.Info($"{logPrefix}: workers={workers.Count}, files=[{files}], {snapshot}");

        foreach (var worker in workers)
        {
            try
            {
                worker.CancellationReason = reasonFactory(worker);
                worker.Cancellation.Cancel();
            }
            catch
            {
            }
        }
    }
}
