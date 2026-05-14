namespace AniNest.Features.Player.Input;

public sealed class PlayerInputMouseWheelEvent
{
    public required int Delta { get; init; }
    public PlayerInputModifiers Modifiers { get; init; }
    public bool ShouldSkip { get; init; }
}
