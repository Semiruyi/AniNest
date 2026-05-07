namespace AniNest.Features.Player.Services;

public interface IPlayerAppService
{
    Task EnterPlayerAsync(string animationCode, string path, string name);
    Task BeginLeavePlayerAsync();
    void CompleteLeavePlayerTransition();
    void OnPlayerPageTransitionCompleted();
}
