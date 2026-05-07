namespace AniNest.Features.Player.Input;

public sealed class PlayerInputBinding
{
    public required PlayerInputAction Action { get; init; }
    public PlayerKeyTrigger? KeyTrigger { get; init; }
    public PlayerMouseTrigger? MouseTrigger { get; init; }
    public bool IsEnabled { get; init; } = true;

    public PlayerInputBinding Clone() => new()
    {
        Action = Action,
        KeyTrigger = KeyTrigger?.Clone(),
        MouseTrigger = MouseTrigger?.Clone(),
        IsEnabled = IsEnabled
    };
}
