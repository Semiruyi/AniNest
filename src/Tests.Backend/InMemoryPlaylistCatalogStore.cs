using AniNest.Application.Playlist;
using AniNest.Contracts.Playlist;

namespace AniNest.Backend.Tests;

internal sealed class InMemoryPlaylistCatalogStore : IPlaylistCatalogStore
{
    private readonly Dictionary<string, PlaylistDto> _playlists;

    public InMemoryPlaylistCatalogStore(IEnumerable<PlaylistDto> playlists)
    {
        _playlists = playlists.ToDictionary(playlist => playlist.FolderId, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<PlaylistDto> GetPlaylists()
        => _playlists.Values.ToArray();

    public PlaylistDto? GetPlaylist(string folderId)
        => _playlists.TryGetValue(folderId, out var playlist) ? playlist : null;

    public void SavePlaylist(PlaylistDto playlist)
    {
        _playlists[playlist.FolderId] = playlist;
    }
}
