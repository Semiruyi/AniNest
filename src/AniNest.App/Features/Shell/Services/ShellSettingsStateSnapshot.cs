namespace AniNest.Features.Shell.Services;

public sealed record ShellSettingsStateSnapshot(
    ShellPreferencesSnapshot Preferences,
    int LanguageSelectedIndex,
    int FullscreenAnimationSelectedIndex,
    int ThumbnailPerformanceSelectedIndex,
    int ThumbnailAccelerationSelectedIndex,
    bool IsLanguageChineseSelected,
    bool IsLanguageEnglishSelected,
    bool IsFullscreenAnimationNoneSelected,
    bool IsFullscreenAnimationContinuousSelected,
    bool IsThumbnailPerformancePausedSelected,
    bool IsThumbnailPerformanceQuietSelected,
    bool IsThumbnailPerformanceBalancedSelected,
    bool IsThumbnailPerformanceFastSelected,
    bool IsThumbnailPerformancePaused,
    bool IsThumbnailAccelerationAutoSelected,
    bool IsThumbnailAccelerationCompatibleSelected);
