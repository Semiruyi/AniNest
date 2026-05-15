using AniNest.Infrastructure.Localization;

namespace AniNest.Features.Shell.Services;

public sealed class ShellSettingsStateService : IShellSettingsStateService
{
    private readonly IShellPreferencesService _preferencesService;

    public ShellSettingsStateService(IShellPreferencesService preferencesService)
    {
        _preferencesService = preferencesService;
    }

    public ShellSettingsStateSnapshot GetStateSnapshot()
    {
        var preferences = _preferencesService.GetSnapshot();
        var languageSelectedIndex = string.Equals(preferences.LanguageCode, "en-US", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        var fullscreenAnimationSelectedIndex = string.Equals(preferences.FullscreenAnimationCode, "continuous", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        var thumbnailPerformanceSelectedIndex = preferences.ThumbnailPerformanceModeCode switch
        {
            "paused" => 0,
            "quiet" => 1,
            "fast" => 3,
            _ => 2
        };
        var thumbnailAccelerationSelectedIndex = string.Equals(preferences.ThumbnailAccelerationModeCode, "compatible", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        return new ShellSettingsStateSnapshot(
            preferences,
            languageSelectedIndex,
            fullscreenAnimationSelectedIndex,
            thumbnailPerformanceSelectedIndex,
            thumbnailAccelerationSelectedIndex,
            languageSelectedIndex == 0,
            languageSelectedIndex == 1,
            fullscreenAnimationSelectedIndex == 0,
            fullscreenAnimationSelectedIndex == 1,
            thumbnailPerformanceSelectedIndex == 0,
            thumbnailPerformanceSelectedIndex == 1,
            thumbnailPerformanceSelectedIndex == 2,
            thumbnailPerformanceSelectedIndex == 3,
            string.Equals(preferences.ThumbnailPerformanceModeCode, "paused", StringComparison.OrdinalIgnoreCase),
            thumbnailAccelerationSelectedIndex == 0,
            thumbnailAccelerationSelectedIndex == 1);
    }

    public ShellOptionsPanelSnapshot GetOptionsPanelSnapshot(ILocalizationService localization)
    {
        var state = GetStateSnapshot();

        return new ShellOptionsPanelSnapshot(
            new[]
            {
                new ShellSelectableOptionSnapshot("zh-CN", "\u7b80\u4f53\u4e2d\u6587", state.IsLanguageChineseSelected),
                new ShellSelectableOptionSnapshot("en-US", "English", state.IsLanguageEnglishSelected)
            },
            new[]
            {
                new ShellSelectableOptionSnapshot("none", localization["Settings.FullscreenAnimation.NoAnimation"], state.IsFullscreenAnimationNoneSelected),
                new ShellSelectableOptionSnapshot("continuous", localization["Settings.FullscreenAnimation.ContinuousAnimation"], state.IsFullscreenAnimationContinuousSelected)
            });
    }
}
