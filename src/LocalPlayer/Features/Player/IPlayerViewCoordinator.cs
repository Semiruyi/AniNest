using System.Threading.Tasks;

namespace LocalPlayer.Features.Player;

public interface IPlayerViewCoordinator
{
    Task EnterPlayerPageAsync(string animationCode);
    Task LeavePlayerPageAsync();
    void LoadFolderSkeleton(string path, string name);
    Task LoadFolderDataAsync();
}
