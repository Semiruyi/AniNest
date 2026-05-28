namespace AniNest.Host.Events;

internal sealed record LibraryFolderEventPayload(
    string FolderId,
    bool? IsFavorite,
    string? WatchStatus,
    int? Position,
    LibraryFolderEventFolderPayload? Folder);
