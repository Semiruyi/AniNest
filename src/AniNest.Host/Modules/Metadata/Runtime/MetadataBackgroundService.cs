using AniNest.Application.Metadata;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Modules;

internal sealed class MetadataBackgroundService : BackgroundService
{
    private readonly IMetadataTaskScheduler _scheduler;
    private readonly IMetadataRecordStore _recordStore;
    private readonly MetadataLifecycleService _lifecycle;
    private readonly ILogger<MetadataBackgroundService> _logger;

    public MetadataBackgroundService(
        IMetadataTaskScheduler scheduler,
        IMetadataRecordStore recordStore,
        IMetadataLifecycleService lifecycle,
        ILogger<MetadataBackgroundService> logger)
    {
        _scheduler = scheduler;
        _recordStore = recordStore;
        _lifecycle = (MetadataLifecycleService)lifecycle;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Metadata background service starting.");
        NormalizeStaleStates();

        while (!stoppingToken.IsCancellationRequested)
        {
            var plan = await _scheduler.DequeueAsync(stoppingToken);
            _logger.LogInformation("Metadata background worker received task. FolderId={FolderId}, Reason={Reason}", plan.FolderId, plan.Reason);
            var record = _recordStore.GetByFolderId(plan.FolderId);
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

            _lifecycle.ExecutePlaceholder(record);
        }
    }

    private void NormalizeStaleStates()
    {
        foreach (var record in _recordStore.GetAll())
        {
            if (record.State is not (AniNest.Core.Enums.MetadataState.Queued or AniNest.Core.Enums.MetadataState.Scraping))
                continue;

            _logger.LogInformation("Metadata background service normalized stale state. FolderId={FolderId}, PreviousState={PreviousState}", record.FolderId, record.State);
            _recordStore.Save(record with
            {
                State = AniNest.Core.Enums.MetadataState.NeedsMetadata,
                FailureKind = AniNest.Core.Enums.MetadataFailureKind.None
            });
        }
    }
}
