using System.Threading.Tasks;

namespace AniNest.Infrastructure.Interop;

public interface ITaskbarAutoHideCoordinator
{
    Task EnterPlayerPageAsync(string animationCode);
    Task LeavePlayerPageAsync();
    void RestoreIfNeeded();
}
