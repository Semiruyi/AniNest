namespace AniNest.Host.Events;

internal sealed record LibraryFolderEventMetadataPayload(
    string? MatchedTitle,
    string? OriginalTitle,
    string? PosterUrl,
    string State,
    bool HasMetadata);
