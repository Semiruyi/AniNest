using System.Windows.Input;

namespace AniNest.Features.Player.Input;

public interface IPlayerInputService
{
    PlayerInputProfile CurrentProfile { get; }
    void ReloadProfile();
    void SaveProfile(PlayerInputProfile profile);

    bool TryHandlePreviewKeyDown(IPlayerInputHost host, KeyEventArgs args);
    bool TryHandlePreviewMouseDown(IPlayerInputHost host, MouseButtonEventArgs args);
    bool TryHandlePreviewMouseUp(IPlayerInputHost host, MouseButtonEventArgs args);
    bool TryHandlePreviewMouseWheel(IPlayerInputHost host, MouseWheelEventArgs args);
}
