using AniNest.Application.Metadata;
using AniNest.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Modules;

internal sealed class MetadataRuntimeBootstrapService : IMetadataRuntimeBootstrapService
{
    private readonly IMetadataStore _legacyStore;
    private readonly IMetadataRecordStore _recordStore;
    private readonly IMetadataAssetService _assets;
    private readonly ILogger<MetadataRuntimeBootstrapService> _logger;

    public MetadataRuntimeBootstrapService(
        IMetadataStore legacyStore,
        IMetadataRecordStore recordStore,
        IMetadataAssetService assets,
        ILogger<MetadataRuntimeBootstrapService> logger)
    {
        _legacyStore = legacyStore;
        _recordStore = recordStore;
        _assets = assets;
        _logger = logger;
    }

    public void EnsureInitialized()
    {
        if (_recordStore.GetAll().Count > 0)
            return;

        _logger.LogInformation("Metadata record store empty. Importing legacy metadata records.");
        foreach (var metadata in _legacyStore.GetAll())
        {
            var payloadPath = _assets.CreateLegacyPayload(metadata);

            _recordStore.Save(new MetadataRecord(
                metadata.FolderId,
                metadata.FolderId,
                metadata.Title ?? metadata.FolderId,
                string.Empty,
                metadata.State,
                metadata.FailureKind,
                null,
                null,
                metadata.State == MetadataState.Ready ? DateTime.UtcNow : null,
                null,
                payloadPath,
                metadata.PosterPath));

            _logger.LogInformation(
                "Metadata legacy record imported. FolderId={FolderId}, State={State}",
                metadata.FolderId,
                metadata.State);
        }
    }

    public void NormalizeTransientStates()
    {
        foreach (var record in _recordStore.GetAll())
        {
            if (record.State is not (MetadataState.Queued or MetadataState.Scraping))
                continue;

            _logger.LogInformation(
                "Metadata stale runtime state normalized. FolderId={FolderId}, PreviousState={PreviousState}",
                record.FolderId,
                record.State);
            _recordStore.Save(record with
            {
                State = MetadataState.NeedsMetadata,
                FailureKind = MetadataFailureKind.None
            });
        }
    }
}
