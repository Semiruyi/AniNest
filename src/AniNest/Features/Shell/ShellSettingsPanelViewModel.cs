using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AniNest.Features.Shell.Services;
using AniNest.Infrastructure.Localization;

namespace AniNest.Features.Shell;

public partial class ShellSettingsPanelViewModel : ObservableObject
{
    private readonly ILocalizationService _localization;
    private readonly IShellSettingsStateService _shellSettingsStateService;
    private readonly IShellSettingsAppService _shellSettingsAppService;
    private readonly IShellThumbnailPerformanceAppService _thumbnailPerformanceAppService;
    private ShellSettingsStateSnapshot _settingsStateSnapshot;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSelectThumbnailPerformanceMode))]
    private bool _isApplyingThumbnailPerformanceMode;

    public ShellSettingsPanelViewModel(
        ILocalizationService localization,
        IShellSettingsStateService shellSettingsStateService,
        IShellSettingsAppService shellSettingsAppService,
        IShellThumbnailPerformanceAppService thumbnailPerformanceAppService)
    {
        _localization = localization;
        _shellSettingsStateService = shellSettingsStateService;
        _shellSettingsAppService = shellSettingsAppService;
        _thumbnailPerformanceAppService = thumbnailPerformanceAppService;
        _settingsStateSnapshot = _shellSettingsStateService.GetStateSnapshot();

        InitializeSelectableOptions();
        RefreshSelectableOptionContent();
        RefreshSelectableOptionSelectionState();
    }

    public event Action? LocalizedDisplayTextChanged;

    public ObservableCollection<SelectableOptionItem> LanguageOptions { get; } = new();
    public ObservableCollection<SelectableOptionItem> FullscreenAnimationOptions { get; } = new();

    public string CurrentLanguageCode => _settingsStateSnapshot.Preferences.LanguageCode;
    public string CurrentAnimationCode => _settingsStateSnapshot.Preferences.FullscreenAnimationCode;
    public string CurrentThumbnailPerformanceModeCode => _settingsStateSnapshot.Preferences.ThumbnailPerformanceModeCode;
    public string CurrentThumbnailAccelerationModeCode => _settingsStateSnapshot.Preferences.ThumbnailAccelerationModeCode;
    public string ThumbnailPerformanceSummary => IsThumbnailPerformancePaused
        ? _localization["Settings.ThumbnailPerformance.Paused"]
        : _localization[$"Settings.ThumbnailPerformance.{CapitalizeCode(CurrentThumbnailPerformanceModeCode)}"];
    public string ThumbnailAccelerationSummary => _localization[$"Settings.ThumbnailAcceleration.{CapitalizeCode(CurrentThumbnailAccelerationModeCode)}"];
    public int LanguageSelectedIndex => _settingsStateSnapshot.LanguageSelectedIndex;
    public int FullscreenAnimationSelectedIndex => _settingsStateSnapshot.FullscreenAnimationSelectedIndex;
    public int ThumbnailPerformanceSelectedIndex => _settingsStateSnapshot.ThumbnailPerformanceSelectedIndex;
    public int ThumbnailAccelerationSelectedIndex => _settingsStateSnapshot.ThumbnailAccelerationSelectedIndex;
    public bool IsLanguageChineseSelected => _settingsStateSnapshot.IsLanguageChineseSelected;
    public bool IsLanguageEnglishSelected => _settingsStateSnapshot.IsLanguageEnglishSelected;
    public bool IsFullscreenAnimationNoneSelected => _settingsStateSnapshot.IsFullscreenAnimationNoneSelected;
    public bool IsFullscreenAnimationContinuousSelected => _settingsStateSnapshot.IsFullscreenAnimationContinuousSelected;
    public bool IsThumbnailPerformancePausedSelected => _settingsStateSnapshot.IsThumbnailPerformancePausedSelected;
    public bool IsThumbnailPerformanceQuietSelected => _settingsStateSnapshot.IsThumbnailPerformanceQuietSelected;
    public bool IsThumbnailPerformanceBalancedSelected => _settingsStateSnapshot.IsThumbnailPerformanceBalancedSelected;
    public bool IsThumbnailPerformanceFastSelected => _settingsStateSnapshot.IsThumbnailPerformanceFastSelected;
    public bool IsThumbnailPerformancePaused => _settingsStateSnapshot.IsThumbnailPerformancePaused;
    public bool CanSelectThumbnailPerformanceMode => !IsApplyingThumbnailPerformanceMode;
    public bool IsThumbnailAccelerationAutoSelected => _settingsStateSnapshot.IsThumbnailAccelerationAutoSelected;
    public bool IsThumbnailAccelerationCompatibleSelected => _settingsStateSnapshot.IsThumbnailAccelerationCompatibleSelected;

    [RelayCommand]
    private void SwitchLanguage(string code)
    {
        _shellSettingsAppService.SetLanguage(code);
        RefreshState(includeLocalizedDisplayText: true);
    }

    [RelayCommand]
    private void SelectFullscreenAnimation(string code)
    {
        _shellSettingsAppService.SetFullscreenAnimation(code);
        RefreshState(includeLocalizedDisplayText: false);
    }

    [RelayCommand(CanExecute = nameof(CanSelectThumbnailPerformanceMode))]
    private async Task SelectThumbnailPerformanceModeAsync(string code)
    {
        if (IsApplyingThumbnailPerformanceMode)
            return;

        try
        {
            IsApplyingThumbnailPerformanceMode = true;
            SelectThumbnailPerformanceModeCommand.NotifyCanExecuteChanged();
            await _thumbnailPerformanceAppService.TrySetPerformanceModeAsync(code);
        }
        finally
        {
            RefreshState(includeLocalizedDisplayText: false);
            IsApplyingThumbnailPerformanceMode = false;
            SelectThumbnailPerformanceModeCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void SelectThumbnailAccelerationMode(string code)
    {
        _shellSettingsAppService.SetThumbnailAccelerationMode(code);
        RefreshState(includeLocalizedDisplayText: false);
    }

    private void RefreshState(bool includeLocalizedDisplayText)
    {
        RefreshPreferencesSnapshot();
        NotifyAllSettingsPropertiesChanged();
        RefreshSelectableOptionSelectionState();

        if (!includeLocalizedDisplayText)
            return;

        RefreshSelectableOptionContent();
        LocalizedDisplayTextChanged?.Invoke();
    }

    private void InitializeSelectableOptions()
    {
        var optionsSnapshot = _shellSettingsStateService.GetOptionsPanelSnapshot(_localization);
        SyncSelectableOptions(LanguageOptions, optionsSnapshot.LanguageOptions);
        SyncSelectableOptions(FullscreenAnimationOptions, optionsSnapshot.FullscreenAnimationOptions);
    }

    private void RefreshSelectableOptionContent()
    {
        var optionsSnapshot = _shellSettingsStateService.GetOptionsPanelSnapshot(_localization);
        SyncSelectableOptions(LanguageOptions, optionsSnapshot.LanguageOptions);
        SyncSelectableOptions(FullscreenAnimationOptions, optionsSnapshot.FullscreenAnimationOptions);
    }

    private void RefreshSelectableOptionSelectionState()
    {
        var optionsSnapshot = _shellSettingsStateService.GetOptionsPanelSnapshot(_localization);
        SyncSelectableOptions(LanguageOptions, optionsSnapshot.LanguageOptions);
        SyncSelectableOptions(FullscreenAnimationOptions, optionsSnapshot.FullscreenAnimationOptions);
    }

    private void RefreshPreferencesSnapshot()
    {
        _settingsStateSnapshot = _shellSettingsStateService.GetStateSnapshot();
    }

    private void NotifyAllSettingsPropertiesChanged()
    {
        OnPropertyChanged(nameof(CurrentLanguageCode));
        OnPropertyChanged(nameof(LanguageSelectedIndex));
        OnPropertyChanged(nameof(IsLanguageChineseSelected));
        OnPropertyChanged(nameof(IsLanguageEnglishSelected));
        OnPropertyChanged(nameof(CurrentAnimationCode));
        OnPropertyChanged(nameof(FullscreenAnimationSelectedIndex));
        OnPropertyChanged(nameof(IsFullscreenAnimationNoneSelected));
        OnPropertyChanged(nameof(IsFullscreenAnimationContinuousSelected));
        OnPropertyChanged(nameof(CurrentThumbnailPerformanceModeCode));
        OnPropertyChanged(nameof(ThumbnailPerformanceSelectedIndex));
        OnPropertyChanged(nameof(ThumbnailPerformanceSummary));
        OnPropertyChanged(nameof(IsThumbnailPerformancePausedSelected));
        OnPropertyChanged(nameof(IsThumbnailPerformanceQuietSelected));
        OnPropertyChanged(nameof(IsThumbnailPerformanceBalancedSelected));
        OnPropertyChanged(nameof(IsThumbnailPerformanceFastSelected));
        OnPropertyChanged(nameof(IsThumbnailPerformancePaused));
        OnPropertyChanged(nameof(CurrentThumbnailAccelerationModeCode));
        OnPropertyChanged(nameof(ThumbnailAccelerationSelectedIndex));
        OnPropertyChanged(nameof(ThumbnailAccelerationSummary));
        OnPropertyChanged(nameof(IsThumbnailAccelerationAutoSelected));
        OnPropertyChanged(nameof(IsThumbnailAccelerationCompatibleSelected));
    }

    private static string CapitalizeCode(string code)
        => string.IsNullOrWhiteSpace(code) ? string.Empty : char.ToUpperInvariant(code[0]) + code[1..];

    private static void SyncSelectableOptions(
        ObservableCollection<SelectableOptionItem> target,
        IReadOnlyList<ShellSelectableOptionSnapshot> source)
    {
        while (target.Count > source.Count)
            target.RemoveAt(target.Count - 1);

        for (int i = 0; i < source.Count; i++)
        {
            var snapshot = source[i];
            if (i >= target.Count)
            {
                target.Add(new SelectableOptionItem(snapshot.Code, snapshot.DisplayName)
                {
                    IsSelected = snapshot.IsSelected
                });
                continue;
            }

            var item = target[i];
            if (!string.Equals(item.Code, snapshot.Code, StringComparison.Ordinal))
            {
                target[i] = new SelectableOptionItem(snapshot.Code, snapshot.DisplayName)
                {
                    IsSelected = snapshot.IsSelected
                };
                continue;
            }

            item.DisplayName = snapshot.DisplayName;
            item.IsSelected = snapshot.IsSelected;
        }
    }

}
