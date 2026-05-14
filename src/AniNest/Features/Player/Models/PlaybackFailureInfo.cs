namespace AniNest.Features.Player.Models;

public sealed record PlaybackFailureInfo(
    string FilePath,
    string? ErrorMessage);
