using AniNest.Application.Metadata;
using AniNest.Contracts.Metadata;

namespace AniNest.Host.Modules;

internal sealed class MetadataLegacySyncService : IMetadataLegacySyncService
{
    private readonly IMetadataStore _legacyStore;

    public MetadataLegacySyncService(IMetadataStore legacyStore)
    {
        _legacyStore = legacyStore;
    }

    public void SaveResolved(MetadataRecord record, FolderMetadataPayload payload)
    {
        _legacyStore.Save(new MetadataDto(
            record.FolderId,
            payload.Title,
            payload.OriginalTitle,
            payload.Summary,
            payload.Tags,
            payload.LocalPosterPath,
            null,
            payload.EpisodeCount,
            payload.Source,
            record.State,
            record.FailureKind,
            payload.AirDate,
            payload.Year,
            payload.Rating));
    }

    public void SaveState(MetadataRecord record, string? summary)
    {
        _legacyStore.Save(new MetadataDto(
            record.FolderId,
            null,
            null,
            summary,
            [],
            null,
            null,
            null,
            null,
            record.State,
            record.FailureKind));
    }
}
