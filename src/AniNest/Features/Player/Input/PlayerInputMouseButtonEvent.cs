namespace AniNest.Features.Player.Input;

public sealed class PlayerInputMouseButtonEvent
{
    public required PlayerInputMouseButton Button { get; init; }
    public PlayerInputModifiers Modifiers { get; init; }
    public int ClickCount { get; init; }
    public bool ShouldSkip { get; init; }
    public bool IsInVideoSurface { get; init; }
}
