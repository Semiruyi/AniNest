namespace AniNest.Features.Player.Input;

public sealed class PlayerInputKeyEvent
{
    public required PlayerInputKey Key { get; init; }
    public PlayerInputModifiers Modifiers { get; init; }
    public bool IsRepeat { get; init; }
    public bool ShouldSkip { get; init; }
}
