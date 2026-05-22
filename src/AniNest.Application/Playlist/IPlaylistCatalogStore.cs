using AniNest.Contracts.Playlist;

namespace AniNest.Application.Playlist;

public interface IPlaylistCatalogStore
{
    IReadOnlyList<PlaylistDto> GetPlaylists();
    PlaylistDto? GetPlaylist(string folderId);
    void SavePlaylist(PlaylistDto playlist);
}
