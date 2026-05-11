using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Shell.Services;

public sealed class ShellPreferencesService : IShellPreferencesService
{
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _localization;
    private readonly IThumbnailDecodeStrategyService _thumbnailDecodeStrategyService;

    public ShellPreferencesService(
        ISettingsService settings,
        ILocalizationService localization,
        IThumbnailDecodeStrategyService thumbnailDecodeStrategyService)
    {
        _settings = settings;
        _localization = localization;
        _thumbnailDecodeStrategyService = thumbnailDecodeStrategyService;
    }

    public string CurrentLanguageCode => _localization.CurrentLanguage;
    public string CurrentFullscreenAnimationCode => _settings.Load().FullscreenAnimation;
    public string CurrentThumbnailPerformanceModeCode => _settings.GetThumbnailPerformanceMode().ToString().ToLowerInvariant();
    public string CurrentThumbnailAccelerationModeCode => _settings.GetThumbnailAccelerationMode().ToString().ToLowerInvariant();
    public ThumbnailDecodeStatusSnapshot CurrentThumbnailDecodeStatus => _thumbnailDecodeStrategyService.GetStatusSnapshot();

    public void SetLanguage(string code)
    {
        _localization.SetLanguage(code);
        var settings = _settings.Load();
        settings.Language = code;
        _settings.Save();
    }

    public void SetFullscreenAnimation(string code)
    {
        var settings = _settings.Load();
        settings.FullscreenAnimation = code;
        _settings.Save();
    }

    public void SetThumbnailPerformanceMode(string code)
    {
        ThumbnailPerformanceMode mode = code.ToLowerInvariant() switch
        {
            "paused" => ThumbnailPerformanceMode.Paused,
            "quiet" => ThumbnailPerformanceMode.Quiet,
            "fast" => ThumbnailPerformanceMode.Fast,
            _ => ThumbnailPerformanceMode.Balanced
        };

        _settings.SetThumbnailPerformanceMode(mode);
    }

    public void SetThumbnailAccelerationMode(string code)
    {
        ThumbnailAccelerationMode mode = code.ToLowerInvariant() switch
        {
            "compatible" => ThumbnailAccelerationMode.Compatible,
            _ => ThumbnailAccelerationMode.Auto
        };

        _settings.SetThumbnailAccelerationMode(mode);
    }
}
