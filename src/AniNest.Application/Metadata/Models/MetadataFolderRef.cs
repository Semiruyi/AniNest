namespace AniNest.Application.Metadata;

public sealed record MetadataFolderRef(
    string FolderId,
    string FolderPath,
    string FolderName,
    string? ParentFolderName,
    IReadOnlyList<string> VideoFiles,
    int VideoCount);
