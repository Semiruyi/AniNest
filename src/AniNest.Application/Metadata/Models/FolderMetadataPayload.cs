namespace AniNest.Application.Metadata;

public sealed record FolderMetadataPayload(
    string FolderId,
    string? SourceId,
    string? Title,
    string? OriginalTitle,
    string? Summary,
    string? PosterUrl,
    string? LocalPosterPath,
    string? AirDate,
    int? Year,
    double? Rating,
    int? EpisodeCount,
    IReadOnlyList<string> Tags,
    string? Source,
    DateTime ScrapedAtUtc);
