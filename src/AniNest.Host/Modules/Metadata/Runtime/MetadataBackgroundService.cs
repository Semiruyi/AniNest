using AniNest.Application.Metadata;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Modules;

internal sealed class MetadataBackgroundService : BackgroundService
{
    private readonly IMetadataTaskQueue _queue;
    private readonly IMetadataRuntimeStateService _state;
    private readonly ILogger<MetadataBackgroundService> _logger;

    public MetadataBackgroundService(
        IMetadataTaskQueue queue,
        IMetadataRuntimeStateService state,
        ILogger<MetadataBackgroundService> logger)
    {
        _queue = queue;
        _state = state;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Metadata background service starting.");
        _state.EnsureInitialized();
        _state.NormalizeTransientStates();

        while (!stoppingToken.IsCancellationRequested)
        {
            var plan = await _queue.DequeueAsync(stoppingToken);
            _logger.LogInformation("Metadata background worker received task. FolderId={FolderId}, Reason={Reason}", plan.FolderId, plan.Reason);
            var record = _state.GetRecord(plan.FolderId);
            if (record is null)
            {
                _logger.LogWarning("Metadata background worker skipped missing record. FolderId={FolderId}", plan.FolderId);
                continue;
            }

            if (!plan.BypassCooldown &&
                record.CooldownUntilUtc is not null &&
                record.CooldownUntilUtc > DateTime.UtcNow)
            {
                _logger.LogInformation("Metadata background worker skipped due to cooldown. FolderId={FolderId}, CooldownUntilUtc={CooldownUntilUtc}", plan.FolderId, record.CooldownUntilUtc);
                continue;
            }

            await _state.ExecuteAsync(record, stoppingToken);
        }
    }
}
