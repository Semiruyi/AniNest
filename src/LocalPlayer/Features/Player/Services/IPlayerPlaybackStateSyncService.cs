namespace AniNest.Features.Player.Services;

public interface IPlayerPlaybackStateSyncService
{
    void Attach(PlayerPlaybackStateController controller);
    void Detach(PlayerPlaybackStateController controller);
}
