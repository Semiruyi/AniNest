using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AniNest.Infrastructure.Logging;

namespace AniNest.Infrastructure.Thumbnails;

internal sealed class ThumbnailWorkerSuspensionCoordinator
{
    private static readonly Logger Log = AppLog.For<ThumbnailWorkerSuspensionCoordinator>();

    private readonly IThumbnailProcessController _processController;
    private readonly ThumbnailWorkerCancellationCoordinator _workerCancellationCoordinator;
    private readonly Func<string> _buildSnapshot;

    public ThumbnailWorkerSuspensionCoordinator(
        IThumbnailProcessController processController,
        ThumbnailWorkerCancellationCoordinator workerCancellationCoordinator,
        Func<string> buildSnapshot)
    {
        _processController = processController;
        _workerCancellationCoordinator = workerCancellationCoordinator;
        _buildSnapshot = buildSnapshot;
    }

    public void SuspendActiveWorkers(
        IReadOnlyCollection<ThumbnailGeneratorWorker> workers,
        Action<ThumbnailTask, ThumbnailState> setTaskState)
    {
        List<ThumbnailGeneratorWorker> fallbackWorkers = [];

        foreach (var worker in workers.Where(static worker => !worker.Execution.IsCompleted))
        {
            if (!worker.ProcessId.HasValue)
            {
                fallbackWorkers.Add(worker);
                Log.Warning(
                    $"Thumbnail worker suspend fallback: file={Path.GetFileName(worker.Task.VideoPath)}, reason=missing-process-id, {_buildSnapshot()}");
                continue;
            }

            try
            {
                _processController.Suspend(worker.ProcessId.Value);
                worker.IsSuspended = true;
                setTaskState(worker.Task, ThumbnailState.PausedGenerating);
                Log.Info(
                    $"Thumbnail worker suspended: file={Path.GetFileName(worker.Task.VideoPath)}, pid={worker.ProcessId.Value}, intent={worker.Task.Intent}, state=Generating->PausedGenerating, {_buildSnapshot()}");
            }
            catch (Exception ex)
            {
                fallbackWorkers.Add(worker);
                Log.Warning(
                    $"Thumbnail worker suspend failed: file={Path.GetFileName(worker.Task.VideoPath)}, pid={worker.ProcessId.Value}, reason={ex.Message}, {_buildSnapshot()}");
            }
        }

        _workerCancellationCoordinator.CancelWithReason(
            fallbackWorkers,
            "generation-paused",
            "Thumbnail worker suspend fallback cancel");
    }

    public void ResumePausedWorkers(
        IReadOnlyCollection<ThumbnailGeneratorWorker> workers,
        Action<ThumbnailTask, ThumbnailState> setTaskState)
    {
        List<ThumbnailGeneratorWorker> fallbackWorkers = [];

        foreach (var worker in workers.Where(static worker => !worker.Execution.IsCompleted))
        {
            if (worker.Task.State != ThumbnailState.PausedGenerating)
                continue;

            if (!worker.ProcessId.HasValue || !worker.IsSuspended)
            {
                fallbackWorkers.Add(worker);
                Log.Warning(
                    $"Thumbnail worker resume fallback: file={Path.GetFileName(worker.Task.VideoPath)}, reason=missing-suspended-process, {_buildSnapshot()}");
                continue;
            }

            try
            {
                _processController.Resume(worker.ProcessId.Value);
                worker.IsSuspended = false;
                setTaskState(worker.Task, ThumbnailState.Generating);
                Log.Info(
                    $"Thumbnail worker resumed: file={Path.GetFileName(worker.Task.VideoPath)}, pid={worker.ProcessId.Value}, intent={worker.Task.Intent}, state=PausedGenerating->Generating, {_buildSnapshot()}");
            }
            catch (Exception ex)
            {
                fallbackWorkers.Add(worker);
                Log.Warning(
                    $"Thumbnail worker resume failed: file={Path.GetFileName(worker.Task.VideoPath)}, pid={worker.ProcessId.Value}, reason={ex.Message}, {_buildSnapshot()}");
            }
        }

        _workerCancellationCoordinator.CancelWithReason(
            fallbackWorkers,
            "generation-resume-failed",
            "Thumbnail worker resume fallback cancel");
    }
}
