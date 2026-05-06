using System.Windows.Input;

namespace LocalPlayer.Features.Player.Input;

public sealed class PlayerMouseTrigger
{
    public MouseButton? Button { get; init; }
    public ModifierKeys Modifiers { get; init; }
    public required PlayerInputTriggerKind Kind { get; init; }
}
