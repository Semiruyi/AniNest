namespace AniNest.Application.Library;

public sealed record LibraryFolderMetadataSummary(
    string? Title,
    string? PosterPath,
    string State,
    bool HasMetadata);
