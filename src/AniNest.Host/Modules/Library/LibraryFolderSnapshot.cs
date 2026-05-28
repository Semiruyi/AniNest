using AniNest.Application.Metadata;
using AniNest.Contracts.Library;

namespace AniNest.Host.Modules;

internal sealed record LibraryFolderSnapshot(
    LibraryFolderDto Folder,
    string FolderPath,
    string FolderName,
    string? ParentFolderName,
    IReadOnlyList<string> VideoFiles,
    int VideoCount)
{
    public MetadataFolderRef? ToMetadataFolderRef()
    {
        if (string.IsNullOrWhiteSpace(FolderPath))
            return null;

        return new MetadataFolderRef(
            Folder.FolderId,
            FolderPath,
            FolderName,
            ParentFolderName,
            VideoFiles,
            VideoCount);
    }
}
