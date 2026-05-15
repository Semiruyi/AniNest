using AniNest.Features.Player.Services;
using AniNest.Infrastructure.Logging;

namespace AniNest.Features.Shell.Services;

public sealed class ShellNavigationAppService : IShellNavigationAppService
{
    private static readonly Logger Log = AppLog.For<ShellNavigationAppService>();
    private readonly IPlayerAppService _playerAppService;

    public ShellNavigationAppService(IPlayerAppService playerAppService)
    {
        _playerAppService = playerAppService;
    }

    public bool CanEnterPlayerPage(bool isTransitionPending, bool isOnMainPage)
        => !isTransitionPending && isOnMainPage;

    public bool CanLeavePlayerPage(bool isTransitionPending, bool isOnPlayerPage)
        => !isTransitionPending && isOnPlayerPage;

    public async Task BeginEnterPlayerPageAsync(string animationCode, string path, string name)
    {
        try
        {
            await _playerAppService.EnterPlayerAsync(animationCode, path, name);
            Log.Info($"BeginEnterPlayerPageAsync finished: name={name}, path={path}");
        }
        catch (Exception ex)
        {
            Log.Error($"BeginEnterPlayerPageAsync failed: name={name}, path={path}", ex);
        }
    }

    public async Task BeginLeavePlayerPageAsync()
    {
        try
        {
            await _playerAppService.BeginLeavePlayerAsync();
            Log.Info("BeginLeavePlayerPageAsync finished");
        }
        catch (Exception ex)
        {
            Log.Error("BeginLeavePlayerPageAsync failed", ex);
        }
    }

    public void CompletePlayerPageTransition(bool isPlayerPageActive)
    {
        if (isPlayerPageActive)
        {
            _playerAppService.OnPlayerPageTransitionCompleted();
            return;
        }

        _playerAppService.CompleteLeavePlayerTransition();
    }
}
