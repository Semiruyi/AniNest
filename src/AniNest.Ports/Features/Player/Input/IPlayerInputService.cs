namespace AniNest.Features.Player.Input;

public interface IPlayerInputService
{
    PlayerInputProfile CurrentProfile { get; }
    void ReloadProfile();
    void SaveProfile(PlayerInputProfile profile);

    bool TryHandleKeyDown(IPlayerInputHost host, PlayerInputKeyEvent inputEvent);
    bool TryHandleMouseDown(IPlayerInputHost host, PlayerInputMouseButtonEvent inputEvent);
    bool TryHandleMouseUp(IPlayerInputHost host, PlayerInputMouseButtonEvent inputEvent);
    bool TryHandleMouseWheel(IPlayerInputHost host, PlayerInputMouseWheelEvent inputEvent);
}
