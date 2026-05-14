using System;

namespace AniNest.Features.Player.Input;

[Flags]
public enum PlayerInputModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}
