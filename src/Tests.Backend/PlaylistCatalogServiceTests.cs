using AniNest.Application.Playlist;
using AniNest.Contracts.Playlist;
using AniNest.Core.Enums;

namespace AniNest.Backend.Tests;

public sealed class PlaylistCatalogServiceTests
{
    [Fact]
    public void GetPlaylist_ReturnsRequestedFolderPlaylist()
    {
        var service = CreateService();

        var playlist = service.GetPlaylist("sample-folder");

        Assert.Equal("sample-folder", playlist.FolderId);
        Assert.Equal(3, playlist.Items.Count);
    }

    [Fact]
    public void ResolveItem_FindsItemAcrossStoredPlaylists()
    {
        var service = CreateService();

        var result = service.ResolveItem("ep-02");

        Assert.Equal("sample-folder", result.Playlist.FolderId);
        Assert.Equal("ep-02", result.Item.ItemId);
    }

    [Fact]
    public void Save_ReplacesPlaylistSnapshot()
    {
        var service = CreateService();
        var playlist = service.GetPlaylist("sample-folder");
        var updated = playlist with { CurrentItemId = "ep-03", CurrentIndex = 2 };

        service.Save(updated);

        var reloaded = service.GetPlaylist("sample-folder");
        Assert.Equal("ep-03", reloaded.CurrentItemId);
        Assert.Equal(2, reloaded.CurrentIndex);
    }

    private static PlaylistCatalogService CreateService()
    {
        var items = Enumerable.Range(1, 3)
            .Select(index => new PlaylistItemDto(
                $"ep-{index:00}",
                index - 1,
                $"Episode {index}",
                $"D:/Media/Sample Anime/{index:00}.mp4",
                false,
                false,
                0,
                1_440_000,
                ThumbnailState.Ready))
            .ToArray();

        var playlist = new PlaylistDto(
            "sample-folder",
            "Sample Anime",
            items[0].ItemId,
            0,
            items);

        return new PlaylistCatalogService(new InMemoryPlaylistCatalogStore([playlist]));
    }
}
