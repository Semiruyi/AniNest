using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AniNest.Infrastructure.Logging;

namespace AniNest.Infrastructure.Thumbnails;

internal sealed class ThumbnailWorkerExecutionHost
{
    private static readonly Logger Log = AppLog.For<ThumbnailWorkerExecutionHost>();

    private readonly ThumbnailGenerationRunner _generationRunner;
    private readonly ThumbnailTaskStore _taskStore;
    private readonly Action<ThumbnailTask, ThumbnailState> _setTaskState;
    private readonly Action _saveIndex;
    private readonly Action _updateProgress;
    private readonly Func<string> _buildSnapshot;
    private readonly Action<string, int>? _videoProgress;
    private readonly Action<string>? _videoReady;

    public ThumbnailWorkerExecutionHost(
        ThumbnailGenerationRunner generationRunner,
        ThumbnailTaskStore taskStore,
        Action<ThumbnailTask, ThumbnailState> setTaskState,
        Action saveIndex,
        Action updateProgress,
        Func<string> buildSnapshot,
        Action<string, int>? videoProgress,
        Action<string>? videoReady)
    {
        _generationRunner = generationRunner;
        _taskStore = taskStore;
        _setTaskState = setTaskState;
        _saveIndex = saveIndex;
        _updateProgress = updateProgress;
        _buildSnapshot = buildSnapshot;
        _videoProgress = videoProgress;
        _videoReady = videoReady;
    }

    public async Task RunAsync(ThumbnailGeneratorWorker worker, CancellationToken ct)
    {
        var task = worker.Task;
        try
        {
            await GenerateForTaskAsync(worker, ct);
            LogWorkerCompletion(task, "completed");
        }
        catch (OperationCanceledException)
        {
            string cancelReason = string.IsNullOrWhiteSpace(worker.CancellationReason)
                ? "unspecified"
                : worker.CancellationReason;
            LogWorkerCompletion(task, $"canceled({cancelReason})");
        }
        catch (Exception ex)
        {
            Log.Error("Generate thumbnail failed", ex);
            _setTaskState(task, ThumbnailState.Failed);
            LogWorkerCompletion(task, "faulted");
        }
        finally
        {
            var finallySw = Stopwatch.StartNew();
            _saveIndex();
            _updateProgress();
            finallySw.Stop();
            Log.Info($"Thumbnail task finalize: file={Path.GetFileName(task.VideoPath)}, elapsed={finallySw.ElapsedMilliseconds}ms, state={task.State}");
        }
    }

    private async Task GenerateForTaskAsync(ThumbnailGeneratorWorker worker, CancellationToken ct)
    {
        var task = worker.Task;
        _setTaskState(task, ThumbnailState.Generating);
        _saveIndex();
        Log.Info($"Thumbnail task generating: file={Path.GetFileName(task.VideoPath)}, {_buildSnapshot()}");

        try
        {
            var result = await _generationRunner.GenerateAsync(
                task,
                ct,
                _videoProgress,
                processId => worker.ProcessId = processId);

            if (result.State == ThumbnailState.Ready)
            {
                task.TotalFrames = result.FrameCount;
                _setTaskState(task, ThumbnailState.Ready);
                _videoProgress?.Invoke(task.VideoPath, 100);
                _videoReady?.Invoke(task.VideoPath);
                worker.ProcessId = null;
                worker.IsSuspended = false;
            }
            else
            {
                _setTaskState(task, ThumbnailState.Failed);
                worker.ProcessId = null;
                worker.IsSuspended = false;
            }
        }
        catch (OperationCanceledException)
        {
            var cancelSw = Stopwatch.StartNew();
            _taskStore.TryRequeueTask(task.VideoPath);
            cancelSw.Stop();
            worker.ProcessId = null;
            worker.IsSuspended = false;
            Log.Info($"Thumbnail task canceled: file={Path.GetFileName(task.VideoPath)}, elapsed={cancelSw.ElapsedMilliseconds}ms, newState={task.State}");
            throw;
        }
    }

    private void LogWorkerCompletion(ThumbnailTask task, string outcome)
    {
        Log.Info(
            $"Thumbnail worker end: file={Path.GetFileName(task.VideoPath)}, outcome={outcome}, " +
            $"state={task.State}, frames={task.TotalFrames}, {_buildSnapshot()}");
    }
}
