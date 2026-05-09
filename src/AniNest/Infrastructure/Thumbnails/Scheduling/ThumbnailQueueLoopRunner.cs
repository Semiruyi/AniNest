using System;
using System.Threading;
using System.Threading.Tasks;

namespace AniNest.Infrastructure.Thumbnails;

internal sealed class ThumbnailQueueLoopRunner
{
    private readonly Func<Task> _waitForInitialization;
    private readonly Func<bool> _isShuttingDown;
    private readonly Action _drainCompletedWorkers;
    private readonly Func<bool> _hasPending;
    private readonly Func<bool> _canStartWorkers;
    private readonly Func<ThumbnailTask?> _selectNextTask;
    private readonly Action<ThumbnailTask, CancellationToken> _startWorker;
    private readonly Action<string> _reportSchedulerState;
    private readonly Func<string> _getBlockedSchedulerReason;
    private readonly Func<int> _getActiveWorkerCount;
    private readonly Func<Task> _waitForWorkersAsync;

    public ThumbnailQueueLoopRunner(
        Func<Task> waitForInitialization,
        Func<bool> isShuttingDown,
        Action drainCompletedWorkers,
        Func<bool> hasPending,
        Func<bool> canStartWorkers,
        Func<ThumbnailTask?> selectNextTask,
        Action<ThumbnailTask, CancellationToken> startWorker,
        Action<string> reportSchedulerState,
        Func<string> getBlockedSchedulerReason,
        Func<int> getActiveWorkerCount,
        Func<Task> waitForWorkersAsync)
    {
        _waitForInitialization = waitForInitialization;
        _isShuttingDown = isShuttingDown;
        _drainCompletedWorkers = drainCompletedWorkers;
        _hasPending = hasPending;
        _canStartWorkers = canStartWorkers;
        _selectNextTask = selectNextTask;
        _startWorker = startWorker;
        _reportSchedulerState = reportSchedulerState;
        _getBlockedSchedulerReason = getBlockedSchedulerReason;
        _getActiveWorkerCount = getActiveWorkerCount;
        _waitForWorkersAsync = waitForWorkersAsync;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        await _waitForInitialization();

        while (!ct.IsCancellationRequested && !_isShuttingDown())
        {
            _drainCompletedWorkers();

            bool hasPending = _hasPending();
            bool canStartWorkers = _canStartWorkers();

            var task = _selectNextTask();
            if (task != null)
            {
                _reportSchedulerState("starting-workers");
                _startWorker(task, ct);
                continue;
            }

            if (!hasPending && _getActiveWorkerCount() == 0)
            {
                _reportSchedulerState("idle");
                try { await Task.Delay(2000, ct); } catch { break; }
                continue;
            }

            _reportSchedulerState(canStartWorkers ? "waiting-for-pending-selection" : _getBlockedSchedulerReason());
            int waitDelayMs = canStartWorkers ? 200 : 500;
            try { await Task.Delay(waitDelayMs, ct); } catch { break; }
        }

        await _waitForWorkersAsync();
    }
}
