using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Shell.Services;

public sealed record ShellPreferencesSnapshot(
    string LanguageCode,
    string FullscreenAnimationCode,
    string ThumbnailPerformanceModeCode,
    string ThumbnailAccelerationModeCode,
    ThumbnailDecodeStatusSnapshot ThumbnailDecodeStatus);
