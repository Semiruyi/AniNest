namespace AniNest.Features.Shell.Services;

public interface IShellSettingsAppService
{
    void SetLanguage(string code);
    void SetFullscreenAnimation(string code);
    void SetThumbnailAccelerationMode(string code);
}
