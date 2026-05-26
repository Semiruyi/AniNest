using AniNest.Application.Playback;
using AniNest.Application.Playlist;
using AniNest.Application.Resources;
using AniNest.Contracts.Playlist;
using AniNest.Contracts.Session;
using AniNest.Contracts.Settings;
using AniNest.Core.Enums;

namespace AniNest.Backend.Tests;

public sealed class PlaybackSessionEngineTests
{
    [Fact]
    public void ActivateFolder_UsesSavedProgressOfCurrentItem()
    {
        var store = new InMemoryPlaybackProgressStore();
        store.SaveFolderProgress("sample-folder", "ep-01");
        store.SaveVideoProgress("D:/Media/Sample Anime/01.mp4", 93_000, 1_440_000);
        var engine = CreateEngine(store);

        var result = engine.ActivateFolder("sample-folder");

        Assert.Equal("ep-01", result.Result.Session.CurrentItemId);
        Assert.Equal(93_000, result.Result.PlaybackTarget.StartPositionMs);
        Assert.Equal(
            "/api/resources/playback-media/sample-folder:ep-01",
            result.Result.PlaybackTarget.MediaUrl);
        Assert.True(result.SessionChanged);
    }

    [Fact]
    public void MoveNext_AdvancesWithinCurrentPlaylist()
    {
        var engine = CreateEngine();
        engine.ActivateFolder("sample-folder");

        var result = engine.MoveNext();

        Assert.Equal("ep-02", result.Session.CurrentItemId);
        Assert.Equal(1, result.Session.CurrentIndex);
        Assert.True(result.Session.HasPrevious);
    }

    [Fact]
    public void SelectItem_WithDuplicateItemIds_StaysInCurrentPlaylist()
    {
        var engine = CreateEngineWithDuplicateItemIds();
        engine.ActivateFolder("sample-folder");

        var result = engine.SelectItem("ep-02");

        Assert.Equal("sample-folder", result.Session.FolderId);
        Assert.Equal("ep-02", result.Session.CurrentItemId);
        Assert.Equal(1, result.Session.CurrentIndex);
        Assert.Equal(
            "/api/resources/playback-media/sample-folder:ep-02",
            result.PlaybackTarget.MediaUrl);
    }

    [Fact]
    public void ReportProgress_WithDuplicateItemIds_UpdatesCurrentPlaylist()
    {
        var store = new InMemoryPlaybackProgressStore();
        var engine = CreateEngineWithDuplicateItemIds(store);
        engine.ActivateFolder("sample-folder");

        engine.ReportProgress(new SessionProgressReportRequest("ep-02", 222_000, 1_440_000, 1.5, 60, false));

        Assert.Equal(222_000, store.GetVideoProgress("D:/Media/Sample Anime/02.mp4")!.Position);
        Assert.Null(store.GetVideoProgress("D:/Media/Other Anime/02.mp4"));
        Assert.Equal("ep-02", store.GetFolderProgress("sample-folder")!.LastItemId);
    }

    [Fact]
    public void Complete_WithDuplicateItemIds_UpdatesCurrentPlaylist()
    {
        var store = new InMemoryPlaybackProgressStore();
        var engine = CreateEngineWithDuplicateItemIds(store);
        engine.ActivateFolder("sample-folder");

        engine.Complete(new SessionCompleteRequest("ep-02"));

        Assert.True(store.GetVideoProgress("D:/Media/Sample Anime/02.mp4")!.IsPlayed);
        Assert.Null(store.GetVideoProgress("D:/Media/Other Anime/02.mp4"));
        Assert.Equal("ep-02", store.GetFolderProgress("sample-folder")!.LastItemId);
    }

    [Fact]
    public void ReportProgress_UpdatesPlaylistItemAndSessionPreferences()
    {
        var store = new InMemoryPlaybackProgressStore();
        var engine = CreateEngine(store);
        engine.ActivateFolder("sample-folder");

        engine.ReportProgress(new SessionProgressReportRequest("ep-01", 222_000, 1_440_000, 1.5, 60, false));

        var current = engine.CurrentSession;
        var playlist = engine.GetCurrentPlaylist();

        Assert.NotNull(current);
        Assert.NotNull(playlist);
        Assert.Equal(222_000, current.SavedProgressMs);
        Assert.Equal(1.5, current.PreferredRate);
        Assert.Equal(60, current.PreferredVolume);
        Assert.True(playlist.Items[0].HasSavedProgress);
        Assert.Equal(222_000, playlist.Items[0].SavedProgressMs);
        Assert.Equal(222_000, store.GetVideoProgress("D:/Media/Sample Anime/01.mp4")!.Position);
        Assert.Equal("ep-01", store.GetFolderProgress("sample-folder")!.LastItemId);
    }

    [Fact]
    public void Complete_ClearsSavedProgressAndMarksItemPlayed()
    {
        var store = new InMemoryPlaybackProgressStore();
        var engine = CreateEngine(store);
        engine.ActivateFolder("sample-folder");

        engine.Complete(new SessionCompleteRequest("ep-01"));

        var current = engine.CurrentSession;
        var playlist = engine.GetCurrentPlaylist();

        Assert.NotNull(current);
        Assert.NotNull(playlist);
        Assert.Equal(0, current.SavedProgressMs);
        Assert.True(playlist.Items[0].IsPlayed);
        Assert.False(playlist.Items[0].HasSavedProgress);
        Assert.Equal(0, playlist.Items[0].SavedProgressMs);
        Assert.True(store.GetVideoProgress("D:/Media/Sample Anime/01.mp4")!.IsPlayed);
    }

    [Fact]
    public void ActivateFolder_WhenSavedProgressExceedsNinetyPercent_StartsFromZero()
    {
        var store = new InMemoryPlaybackProgressStore();
        store.SaveFolderProgress("sample-folder", "ep-01");
        store.SaveVideoProgress("D:/Media/Sample Anime/01.mp4", 1_350_000, 1_440_000);
        var engine = CreateEngine(store);

        var result = engine.ActivateFolder("sample-folder");

        Assert.Equal(0, result.Result.PlaybackTarget.StartPositionMs);
    }

    [Fact]
    public void ActivateFolder_UsesFolderProgressToRestoreLastItem()
    {
        var store = new InMemoryPlaybackProgressStore();
        store.SaveFolderProgress("sample-folder", "ep-03");
        var engine = CreateEngine(store);

        var result = engine.ActivateFolder("sample-folder");

        Assert.Equal("ep-03", result.Result.Session.CurrentItemId);
        Assert.Equal(2, result.Result.Session.CurrentIndex);
    }

    [Fact]
    public void ActivateFolder_WhenTargetStateMatchesCurrentSession_DoesNotRebuildSession()
    {
        var store = new InMemoryPlaybackProgressStore();
        store.SaveFolderProgress("sample-folder", "ep-01");
        store.SaveVideoProgress("D:/Media/Sample Anime/01.mp4", 93_000, 1_440_000);
        var engine = CreateEngine(store);

        var first = engine.ActivateFolder("sample-folder");
        var second = engine.ActivateFolder("sample-folder");

        Assert.True(first.SessionChanged);
        Assert.False(second.SessionChanged);
        Assert.Equal(first.Result.Session, second.Result.Session);
        Assert.Equal(first.Result.PlaybackTarget, second.Result.PlaybackTarget);
    }

    [Fact]
    public void Close_PersistsCurrentItemAsFolderProgress()
    {
        var store = new InMemoryPlaybackProgressStore();
        var engine = CreateEngine(store);
        engine.ActivateFolder("sample-folder");
        engine.MoveNext();

        engine.Close();

        Assert.Equal("ep-02", store.GetFolderProgress("sample-folder")!.LastItemId);
    }

    private static PlaybackSessionEngine CreateEngine(InMemoryPlaybackProgressStore? store = null)
    {
        var items = Enumerable.Range(1, 3)
            .Select(index => new PlaylistItemDto(
                $"ep-{index:00}",
                index - 1,
                $"Episode {index}",
                $"D:/Media/Sample Anime/{index:00}.mp4",
                index == 1,
                index == 1,
                index == 1 ? 93_000 : 0,
                1_440_000,
                ThumbnailState.Ready))
            .ToArray();

        var playlist = new PlaylistDto(
            "sample-folder",
            "Sample Anime",
            items[0].ItemId,
            0,
            items);

        var playlistCatalog = new PlaylistCatalogService(
            new InMemoryPlaylistCatalogStore([playlist]));

        return new PlaybackSessionEngine(
            playlistCatalog,
            new PlayerSettingsDto(1.0, 80, true),
            store ?? new InMemoryPlaybackProgressStore(),
            new FakeResourceUrlService());
    }

    private static PlaybackSessionEngine CreateEngineWithDuplicateItemIds(InMemoryPlaybackProgressStore? store = null)
    {
        static PlaylistDto BuildPlaylist(string folderId, string folderName, string path)
        {
            var items = Enumerable.Range(1, 3)
                .Select(index => new PlaylistItemDto(
                    $"ep-{index:00}",
                    index - 1,
                    $"Episode {index}",
                    $"{path}/{index:00}.mp4",
                    false,
                    false,
                    0,
                    1_440_000,
                    ThumbnailState.Ready))
                .ToArray();

            return new PlaylistDto(
                folderId,
                folderName,
                items[0].ItemId,
                0,
                items);
        }

        var playlistCatalog = new PlaylistCatalogService(
            new InMemoryPlaylistCatalogStore(
                [
                    BuildPlaylist("other-folder", "Other Anime", "D:/Media/Other Anime"),
                    BuildPlaylist("sample-folder", "Sample Anime", "D:/Media/Sample Anime")
                ]));

        return new PlaybackSessionEngine(
            playlistCatalog,
            new PlayerSettingsDto(1.0, 80, true),
            store ?? new InMemoryPlaybackProgressStore(),
            new FakeResourceUrlService());
    }

    private sealed class FakeResourceUrlService : IResourceUrlService
    {
        public string GetUrl(ResourceKey key)
            => $"/api/resources/{key.Kind switch
            {
                ResourceKind.PlaybackMedia => "playback-media",
                _ => "unknown"
            }}/{key.OwnerId}";
    }
}
