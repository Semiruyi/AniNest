using System.Windows.Input;

namespace LocalPlayer.Features.Player.Input;

public sealed class PlayerKeyTrigger
{
    public required Key Key { get; init; }
    public ModifierKeys Modifiers { get; init; }
    public bool AllowRepeat { get; init; } = true;

    public PlayerKeyTrigger Clone() => new()
    {
        Key = Key,
        Modifiers = Modifiers,
        AllowRepeat = AllowRepeat
    };
}
