using AniNest.Infrastructure.Localization;

namespace AniNest.Features.Shell.Services;

public interface IShellSettingsStateService
{
    ShellSettingsStateSnapshot GetStateSnapshot();
    ShellOptionsPanelSnapshot GetOptionsPanelSnapshot(ILocalizationService localization);
}
