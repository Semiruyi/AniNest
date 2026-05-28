namespace AniNest.Host.Events;

internal sealed record LibraryFolderEventFolderPayload(
    string FolderId,
    string Name,
    int VideoCount,
    string? CoverUrl,
    int PlayedCount,
    string WatchStatus,
    bool IsFavorite,
    DateTimeOffset AddedAtUtc,
    LibraryFolderEventMetadataPayload? MetadataSummary);
