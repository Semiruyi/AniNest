namespace LocalPlayer.Features.Player.Services;

public interface IPlayerThumbnailSyncService
{
    void Attach(PlaylistViewModel playlist);
    void Detach(PlaylistViewModel playlist);
}
