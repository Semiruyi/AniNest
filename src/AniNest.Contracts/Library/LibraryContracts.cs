using AniNest.Core.Enums;

namespace AniNest.Contracts.Library;

public sealed record LibraryMetadataSummaryDto(
    string? MatchedTitle,
    string? OriginalTitle,
    string? PosterUrl,
    string State,
    bool HasMetadata);

public sealed record LibraryFolderDto(
    string FolderId,
    string Name,
    string Path,
    int VideoCount,
    string? CoverUrl,
    int PlayedCount,
    WatchStatus WatchStatus,
    bool IsFavorite,
    DateTimeOffset AddedAtUtc,
    LibraryMetadataSummaryDto? MetadataSummary);

public sealed record LibraryFolderListResponse(
    IReadOnlyList<LibraryFolderDto> Items);

public sealed record AddLibraryFolderRequest(
    string Path);

public sealed record AddLibraryFolderResult(
    string Status,
    string Message,
    string? ReasonCode,
    LibraryFolderDto? Folder);

public sealed record BatchAddLibraryFoldersRequest(
    string RootPath);

public sealed record LibraryBrowserDirectoryDto(
    string Name,
    string Path);

public sealed record LibraryBrowserResponse(
    string RootPath,
    string CurrentPath,
    string? ParentPath,
    bool CanSelect,
    IReadOnlyList<LibraryBrowserDirectoryDto> Directories);

public sealed record SetFavoriteRequest(
    bool IsFavorite);

public sealed record SetWatchStatusRequest(
    WatchStatus Status);
