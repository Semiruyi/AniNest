using AniNest.Contracts.Library;

namespace AniNest.Application.Library;

public sealed record LibraryFolderRecord(
    string FolderId,
    string Name,
    string Path,
    int VideoCount,
    string? CoverPath,
    LibraryMetadataSummaryDto? MetadataSummary,
    int Order);
