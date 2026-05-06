using LocalPlayer.Infrastructure.Localization;

namespace LocalPlayer.Features.Player.Input;

public static class PlayerInputDisplayNames
{
    public static string GetActionDisplayName(ILocalizationService localization, PlayerInputAction action)
        => localization[GetActionKey(action)];

    public static string GetActionKey(PlayerInputAction action) => action switch
    {
        PlayerInputAction.PlayPause => "Player.PlayPause",
        PlayerInputAction.Stop => "Player.Stop",
        PlayerInputAction.Next => "Player.Next",
        PlayerInputAction.Previous => "Player.Previous",
        PlayerInputAction.ToggleFullscreen => "Player.Fullscreen",
        PlayerInputAction.ExitFullscreenOrBack => "Player.Input.ExitFullscreenOrBack",
        PlayerInputAction.TogglePlaylist => "Player.Playlist",
        PlayerInputAction.SeekForwardSmall => "Player.Input.SeekForwardSmall",
        PlayerInputAction.SeekBackwardSmall => "Player.Input.SeekBackwardSmall",
        PlayerInputAction.SeekForwardLarge => "Player.Input.SeekForwardLarge",
        PlayerInputAction.SeekBackwardLarge => "Player.Input.SeekBackwardLarge",
        PlayerInputAction.BoostSpeedHold => "Player.Input.BoostSpeedHold",
        PlayerInputAction.BoostSpeedRelease => "Player.Input.BoostSpeedRelease",
        _ => action.ToString()
    };
}
