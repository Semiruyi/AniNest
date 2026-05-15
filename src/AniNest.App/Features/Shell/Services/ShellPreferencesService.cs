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

    public ShellPreferencesSnapshot GetSnapshot()
        => new(
            _localization.CurrentLanguage,
            _settings.Load().FullscreenAnimation,
            _settings.GetThumbnailPerformanceMode().ToString().ToLowerInvariant(),
            _settings.GetThumbnailAccelerationMode().ToString().ToLowerInvariant(),
            _thumbnailDecodeStrategyService.GetStatusSnapshot());
}
