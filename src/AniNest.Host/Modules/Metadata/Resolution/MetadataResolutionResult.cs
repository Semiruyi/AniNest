using AniNest.Application.Metadata;
using AniNest.Core.Enums;

namespace AniNest.Host.Modules;

internal sealed record MetadataResolutionResult(
    MetadataState NextState,
    MetadataFailureKind FailureKind,
    FolderMetadataPayload? Payload,
    string? SourceId,
    string? PosterUrl,
    string? Reason);
