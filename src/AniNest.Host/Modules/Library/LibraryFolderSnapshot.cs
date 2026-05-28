using AniNest.Contracts.Library;

namespace AniNest.Host.Modules;

internal sealed record LibraryFolderSnapshot(
    LibraryFolderDto Folder,
    string FolderPath,
    string FolderName,
    string? ParentFolderName,
    IReadOnlyList<string> VideoFiles,
    int VideoCount);
