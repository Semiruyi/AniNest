namespace AniNest.Host.Events;

internal sealed record MetadataFolderUpdatedEventPayload(
    string FolderId,
    string? State,
    string? FailureKind,
    bool HasMetadata,
    string? MatchedTitle,
    string? OriginalTitle,
    string? Title,
    string? PosterUrl,
    string? CoverUrl,
    DateTimeOffset UpdatedAtUtc);
