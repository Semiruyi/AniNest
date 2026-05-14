namespace AniNest.Features.Player.Input;

public sealed class PlayerMouseTrigger
{
    public PlayerInputMouseButton? Button { get; init; }
    public PlayerInputModifiers Modifiers { get; init; }
    public required PlayerInputTriggerKind Kind { get; init; }

    public PlayerMouseTrigger Clone() => new()
    {
        Button = Button,
        Modifiers = Modifiers,
        Kind = Kind
    };
}
