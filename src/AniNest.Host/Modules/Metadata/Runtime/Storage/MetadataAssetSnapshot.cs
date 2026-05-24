using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal sealed record MetadataAssetSnapshot(
    FolderMetadataPayload Payload,
    string PayloadPath,
    string? PosterFilePath);
