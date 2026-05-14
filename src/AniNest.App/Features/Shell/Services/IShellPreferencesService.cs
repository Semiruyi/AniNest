using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Shell.Services;

public interface IShellPreferencesService
{
    string CurrentLanguageCode { get; }
    string CurrentFullscreenAnimationCode { get; }
    string CurrentThumbnailPerformanceModeCode { get; }
    string CurrentThumbnailAccelerationModeCode { get; }
    ThumbnailDecodeStatusSnapshot CurrentThumbnailDecodeStatus { get; }
}
