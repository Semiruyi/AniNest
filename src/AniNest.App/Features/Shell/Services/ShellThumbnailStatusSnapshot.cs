using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Shell.Services;

public sealed record ShellThumbnailStatusSnapshot(
    ThumbnailDecodeStatusSnapshot DecodeStatus,
    ThumbnailGenerationStatusSnapshot GenerationStatus,
    string GenerationStatusCode);
