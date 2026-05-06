using System.Threading.Tasks;
using LocalPlayer.Infrastructure.Interop;

namespace LocalPlayer.Features.Player;

public class PlayerViewCoordinator : IPlayerViewCoordinator
{
    private readonly ITaskbarAutoHideCoordinator _taskbarAutoHide;
    private readonly PlayerSessionController _session;

    public PlayerViewCoordinator(
        ITaskbarAutoHideCoordinator taskbarAutoHide,
        PlayerSessionController session)
    {
        _taskbarAutoHide = taskbarAutoHide;
        _session = session;
    }

    public Task EnterPlayerPageAsync(string animationCode)
        => _taskbarAutoHide.EnterPlayerPageAsync(animationCode);

    public Task LeavePlayerPageAsync()
        => _taskbarAutoHide.LeavePlayerPageAsync();

    public void LoadFolderSkeleton(string path, string name)
        => _session.LoadFolderSkeleton(path, name);

    public Task LoadFolderDataAsync()
        => _session.LoadFolderDataAsync();
}
