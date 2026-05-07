namespace LocalPlayer.Features.Shell.Services;

public interface IShellPreferencesService
{
    string CurrentLanguageCode { get; }
    string CurrentFullscreenAnimationCode { get; }

    void SetLanguage(string code);
    void SetFullscreenAnimation(string code);
}
