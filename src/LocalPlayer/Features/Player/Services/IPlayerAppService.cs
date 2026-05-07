namespace LocalPlayer.Features.Player.Services;

public interface IPlayerAppService
{
    Task EnterPlayerAsync(string animationCode, string path, string name);
    Task LeavePlayerAsync();
    void OnPlayerPageTransitionCompleted();
}
