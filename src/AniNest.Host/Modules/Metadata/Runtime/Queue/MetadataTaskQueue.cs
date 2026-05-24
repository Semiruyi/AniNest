using System.Threading.Channels;
using AniNest.Application.Metadata;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Modules;

internal sealed class MetadataTaskQueue : IMetadataTaskQueue
{
    private readonly Lock _sync = new();
    private readonly PriorityQueue<MetadataTaskPlan, int> _queue = new();
    private readonly HashSet<string> _queuedFolderIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Channel<bool> _signal = Channel.CreateUnbounded<bool>();
    private readonly ILogger<MetadataTaskQueue> _logger;

    public MetadataTaskQueue(ILogger<MetadataTaskQueue> logger)
    {
        _logger = logger;
    }

    public bool Enqueue(MetadataTaskPlan plan)
    {
        lock (_sync)
        {
            if (!_queuedFolderIds.Add(plan.FolderId))
            {
                _logger.LogDebug(
                    "Metadata task skipped because folder is already queued. FolderId={FolderId}, Reason={Reason}",
                    plan.FolderId,
                    plan.Reason);
                return false;
            }

            _queue.Enqueue(plan, plan.Priority);
        }

        _logger.LogInformation(
            "Metadata task enqueued. FolderId={FolderId}, Reason={Reason}, Priority={Priority}, BypassCooldown={BypassCooldown}",
            plan.FolderId,
            plan.Reason,
            plan.Priority,
            plan.BypassCooldown);
        _signal.Writer.TryWrite(true);
        return true;
    }

    public async ValueTask<MetadataTaskPlan> DequeueAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            lock (_sync)
            {
                if (_queue.TryDequeue(out var plan, out _))
                {
                    _queuedFolderIds.Remove(plan.FolderId);
                    _logger.LogInformation(
                        "Metadata task dequeued. FolderId={FolderId}, Reason={Reason}, Priority={Priority}",
                        plan.FolderId,
                        plan.Reason,
                        plan.Priority);
                    return plan;
                }
            }

            await _signal.Reader.ReadAsync(cancellationToken);
        }
    }
}
