using System.Windows.Input;

namespace AniNest.Features.Player.Input;

internal static class WpfPlayerInputMapper
{
    public static PlayerInputKey ToPlayerKey(Key key)
        => Enum.IsDefined(typeof(PlayerInputKey), (int)key)
            ? (PlayerInputKey)(int)key
            : PlayerInputKey.Unknown;

    public static PlayerInputMouseButton ToPlayerMouseButton(MouseButton button)
        => (PlayerInputMouseButton)(int)button;

    public static PlayerInputModifiers ToPlayerModifiers(ModifierKeys modifiers)
        => (PlayerInputModifiers)(int)modifiers;
}
