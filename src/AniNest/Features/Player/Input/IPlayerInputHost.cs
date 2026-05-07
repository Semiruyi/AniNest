namespace AniNest.Features.Player.Input;

public interface IPlayerInputHost
{
    IPlayerInputService InputService { get; }
    bool TryHandleInput(PlayerInputAction action);
}
