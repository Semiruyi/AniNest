using AniNest.Core.Enums;

namespace AniNest.Contracts.Playlist;

public sealed record PlaylistItemDto(
    string ItemId,
    int Index,
    string Title,
    string FilePath,
    bool IsPlayed,
    bool HasSavedProgress,
    long SavedProgressMs,
    long DurationMs,
    ThumbnailState ThumbnailState);

public sealed record PlaylistDto(
    string FolderId,
    string FolderName,
    string? CurrentItemId,
    int CurrentIndex,
    IReadOnlyList<PlaylistItemDto> Items);
