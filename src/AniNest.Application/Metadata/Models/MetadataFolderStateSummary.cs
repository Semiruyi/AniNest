using AniNest.Core.Enums;

namespace AniNest.Application.Metadata;

public sealed record MetadataFolderStateSummary(
    bool HasMetadata,
    MetadataState State,
    string? Title,
    string? PosterPath);
