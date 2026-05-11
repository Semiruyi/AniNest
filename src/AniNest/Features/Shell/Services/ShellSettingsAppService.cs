using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Shell.Services;

public sealed class ShellSettingsAppService : IShellSettingsAppService
{
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _localization;
    private readonly IThumbnailGenerator _thumbnailGenerator;

    public ShellSettingsAppService(
        ISettingsService settings,
        ILocalizationService localization,
        IThumbnailGenerator thumbnailGenerator)
    {
        _settings = settings;
        _localization = localization;
        _thumbnailGenerator = thumbnailGenerator;
    }

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

    public void SetThumbnailAccelerationMode(string code)
    {
        ThumbnailAccelerationMode mode = code.ToLowerInvariant() switch
        {
            "compatible" => ThumbnailAccelerationMode.Compatible,
            _ => ThumbnailAccelerationMode.Auto
        };

        _settings.SetThumbnailAccelerationMode(mode);
        _thumbnailGenerator.RefreshDecodeStrategy();
    }
}
