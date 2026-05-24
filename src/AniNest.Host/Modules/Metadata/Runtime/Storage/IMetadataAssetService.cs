using AniNest.Application.Metadata;
using AniNest.Contracts.Metadata;

namespace AniNest.Host.Modules;

internal interface IMetadataAssetService
{
    string? CreateLegacyPayload(MetadataDto metadata);
    Task<MetadataAssetSnapshot> SaveResolvedPayloadAsync(
        string folderId,
        FolderMetadataPayload payload,
        string? currentMetadataFilePath,
        string? currentPosterFilePath,
        CancellationToken cancellationToken);
    MetadataAssetSnapshot SavePlaceholderPayload(
        MetadataRecord record,
        FolderMetadataPayload payload);
    void DeleteAssets(MetadataRecord? record);
    void DeleteAssets(string? metadataFilePath, string? posterFilePath);
}
