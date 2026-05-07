using LocalPlayer.Infrastructure.Localization;
using LocalPlayer.Infrastructure.Persistence;

namespace LocalPlayer.Features.Shell.Services;

public sealed class ShellPreferencesService : IShellPreferencesService
{
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _localization;

    public ShellPreferencesService(
        ISettingsService settings,
        ILocalizationService localization)
    {
        _settings = settings;
        _localization = localization;
    }

    public string CurrentLanguageCode => _localization.CurrentLanguage;
    public string CurrentFullscreenAnimationCode => _settings.Load().FullscreenAnimation;

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
}
