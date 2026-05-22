using AniNest.Contracts.Playlist;

namespace AniNest.Application.Playlist;

public sealed class PlaylistCatalogService
{
    private readonly IPlaylistCatalogStore _store;

    public PlaylistCatalogService(IPlaylistCatalogStore store)
    {
        _store = store;
    }

    public PlaylistDto GetPlaylist(string folderId)
        => _store.GetPlaylist(folderId)
            ?? throw new KeyNotFoundException($"Playlist not found for folder '{folderId}'.");

    public IReadOnlyList<PlaylistDto> GetAll()
        => _store.GetPlaylists();

    public void Save(PlaylistDto playlist)
        => _store.SavePlaylist(playlist);

    public (PlaylistDto Playlist, PlaylistItemDto Item) ResolveItem(string itemId)
    {
        foreach (var playlist in _store.GetPlaylists())
        {
            var item = playlist.Items.FirstOrDefault(candidate => string.Equals(candidate.ItemId, itemId, StringComparison.OrdinalIgnoreCase));
            if (item is not null)
                return (playlist, item);
        }

        throw new KeyNotFoundException($"Playlist item '{itemId}' was not found.");
    }
}
