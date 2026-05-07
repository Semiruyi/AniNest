using LocalPlayer.Infrastructure.Interop;

namespace LocalPlayer.Features.Player.Services;

public sealed class PlayerAppService : IPlayerAppService
{
    private readonly ITaskbarAutoHideCoordinator _taskbarAutoHide;
    private readonly PlayerSessionController _session;

    public PlayerAppService(
        ITaskbarAutoHideCoordinator taskbarAutoHide,
        PlayerSessionController session)
    {
        _taskbarAutoHide = taskbarAutoHide;
        _session = session;
    }

    public async Task EnterPlayerAsync(string animationCode, string path, string name)
    {
        _ = _taskbarAutoHide.EnterPlayerPageAsync(animationCode);
        await _session.LoadFolderAsync(path, name);
    }

    public Task LeavePlayerAsync()
        => _taskbarAutoHide.LeavePlayerPageAsync();
}
