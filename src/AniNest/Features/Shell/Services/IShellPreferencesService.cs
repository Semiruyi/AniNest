using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Shell.Services;

public interface IShellPreferencesService
{
    string CurrentLanguageCode { get; }
    string CurrentFullscreenAnimationCode { get; }
    string CurrentThumbnailPerformanceModeCode { get; }
    string CurrentThumbnailAccelerationModeCode { get; }
    bool IsThumbnailGenerationPaused { get; }
    ThumbnailDecodeStatusSnapshot CurrentThumbnailDecodeStatus { get; }

    void SetLanguage(string code);
    void SetFullscreenAnimation(string code);
    void SetThumbnailPerformanceMode(string code);
    void SetThumbnailGenerationPaused(bool paused);
    void SetThumbnailAccelerationMode(string code);
}
