namespace LocalPlayer.Features.Library.Services;

public sealed record LibraryFolderDto(
    string Name,
    string Path,
    int VideoCount,
    string? CoverPath);

public enum OpenFolderFailure
{
    None,
    NoVideos
}

public sealed record OpenFolderResult(
    bool Success,
    string FolderName,
    OpenFolderFailure Failure = OpenFolderFailure.None);

public enum AddFolderFailure
{
    None,
    NoVideos,
    Duplicate,
    Unknown
}

public sealed record AddFolderResult(
    bool Success,
    LibraryFolderDto? Folder,
    AddFolderFailure Failure = AddFolderFailure.None,
    string? ErrorMessage = null);

public sealed record BatchAddFoldersResult(
    IReadOnlyList<LibraryFolderDto> AddedFolders,
    int SkippedCount);

public enum ThumbnailExpirySaveOutcome
{
    InvalidInput,
    SavedDays,
    SavedNever
}

public sealed record ThumbnailExpirySaveResult(
    bool Success,
    ThumbnailExpirySaveOutcome Outcome,
    int? Days = null);
