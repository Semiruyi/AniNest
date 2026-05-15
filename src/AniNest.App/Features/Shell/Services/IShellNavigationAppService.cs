namespace AniNest.Features.Shell.Services;

public interface IShellNavigationAppService
{
    bool CanEnterPlayerPage(bool isTransitionPending, bool isOnMainPage);
    bool CanLeavePlayerPage(bool isTransitionPending, bool isOnPlayerPage);
    Task BeginEnterPlayerPageAsync(string animationCode, string path, string name);
    Task BeginLeavePlayerPageAsync();
    void CompletePlayerPageTransition(bool isPlayerPageActive);
}
