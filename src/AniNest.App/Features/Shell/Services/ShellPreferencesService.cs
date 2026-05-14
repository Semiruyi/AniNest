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
}
