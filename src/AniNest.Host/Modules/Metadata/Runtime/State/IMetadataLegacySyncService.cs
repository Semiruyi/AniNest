using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal interface IMetadataLegacySyncService
{
    void SaveResolved(MetadataRecord record, FolderMetadataPayload payload);
    void SaveState(MetadataRecord record, string? summary);
}
