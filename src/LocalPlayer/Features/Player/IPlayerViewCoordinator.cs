using System.Threading.Tasks;

namespace LocalPlayer.Features.Player;

public interface IPlayerViewCoordinator
{
    Task EnterPlayerPageAsync(string animationCode);
    Task LeavePlayerPageAsync();
    Task LoadFolderAsync(string path, string name);
}
