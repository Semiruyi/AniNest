using System.Windows.Input;

namespace AniNest.Features.Player.Input;

public sealed class PlayerMouseTrigger
{
    public MouseButton? Button { get; init; }
    public ModifierKeys Modifiers { get; init; }
    public required PlayerInputTriggerKind Kind { get; init; }

    public PlayerMouseTrigger Clone() => new()
    {
        Button = Button,
        Modifiers = Modifiers,
        Kind = Kind
    };
}
