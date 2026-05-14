namespace AniNest.Features.Player.Input;

public sealed class PlayerKeyTrigger
{
    public required PlayerInputKey Key { get; init; }
    public PlayerInputModifiers Modifiers { get; init; }
    public bool AllowRepeat { get; init; } = true;

    public PlayerKeyTrigger Clone() => new()
    {
        Key = Key,
        Modifiers = Modifiers,
        AllowRepeat = AllowRepeat
    };
}
