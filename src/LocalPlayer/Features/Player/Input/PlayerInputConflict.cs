namespace AniNest.Features.Player.Input;

public sealed class PlayerInputConflict
{
    public required int ExistingIndex { get; init; }
    public required PlayerInputBinding ExistingBinding { get; init; }
    public required PlayerInputBinding IncomingBinding { get; init; }
}
