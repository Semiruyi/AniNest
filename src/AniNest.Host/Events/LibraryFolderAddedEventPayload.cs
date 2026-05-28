namespace AniNest.Host.Events;

internal sealed record LibraryFolderAddedEventPayload(
    string? FolderId,
    string? Path,
    LibraryFolderEventFolderPayload? Folder);
