namespace AniNest.Infrastructure.Thumbnails;

public sealed record FolderScanResult(
    int VideoCount,
    string? CoverPath,
    string[] VideoFiles);
