using System.Threading.Tasks;

namespace LocalPlayer.Infrastructure.Interop;

public interface ITaskbarAutoHideCoordinator
{
    Task EnterPlayerPageAsync(string animationCode);
    Task LeavePlayerPageAsync();
    void RestoreIfNeeded();
}
