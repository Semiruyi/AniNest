namespace AniNest.Features.Metadata;

public sealed record MetadataFolderRef(
    string FolderPath,
    string FolderName,
    IReadOnlyList<string> VideoFiles);
