using AniNest.Application.Playlist;
using AniNest.Contracts.Playlist;
using AniNest.Core.Enums;

namespace AniNest.Host.Modules;

internal sealed class InMemoryPlaylistCatalogStore : IPlaylistCatalogStore
{
    private readonly Dictionary<string, PlaylistDto> _playlists;

    public InMemoryPlaylistCatalogStore()
    {
        var sampleItems = Enumerable.Range(1, 12)
            .Select(index => new PlaylistItemDto(
                $"ep-{index:00}",
                index - 1,
                $"Episode {index}",
                $"D:/Media/Sample Anime/{index:00}.mp4",
                index < 4,
                index == 1,
                index == 1 ? 93_000 : 0,
                1_440_000,
                index < 3 ? ThumbnailState.Ready : ThumbnailState.Pending))
            .ToArray();

        _playlists = new Dictionary<string, PlaylistDto>(StringComparer.OrdinalIgnoreCase)
        {
            ["sample-folder"] = new PlaylistDto(
                "sample-folder",
                "Sample Anime",
                sampleItems[0].ItemId,
                0,
                sampleItems)
        };
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
