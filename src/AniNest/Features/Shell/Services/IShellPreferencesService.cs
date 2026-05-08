namespace AniNest.Features.Shell.Services;

public interface IShellPreferencesService
{
    string CurrentLanguageCode { get; }
    string CurrentFullscreenAnimationCode { get; }
    string CurrentThumbnailPerformanceModeCode { get; }

    void SetLanguage(string code);
    void SetFullscreenAnimation(string code);
    void SetThumbnailPerformanceMode(string code);
}
