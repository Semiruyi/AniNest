using AniNest.Application.Playback;
using AniNest.Host.Modules;

namespace AniNest.Backend.Tests;

public sealed class FilePlaybackProgressStoreTests
{
    [Fact]
    public void Load_WhenFileMissing_ReturnsNoProgress()
    {
        var store = CreateStore(CreateTempPath());

        Assert.Null(store.GetVideoProgress("D:/Anime/A/ep01.mp4"));
        Assert.Null(store.GetFolderProgress("folder-01"));
    }

    [Fact]
    public void SaveAndReload_PersistsVideoAndFolderProgress()
    {
        var path = CreateTempPath();
        var store = CreateStore(path);

        store.SaveVideoProgress("D:/Anime/A/ep01.mp4", 123_000, 1_440_000);
        store.SaveFolderProgress("folder-01", "ep-01");
        store.SaveLastSession(new PlaybackSessionState("folder-01", "ep-01", 1.25, 65));

        var reloaded = CreateStore(path);
        var video = reloaded.GetVideoProgress("D:/Anime/A/ep01.mp4");
        var folder = reloaded.GetFolderProgress("folder-01");
        var session = reloaded.GetLastSession();

        Assert.NotNull(video);
        Assert.Equal(123_000, video.Position);
        Assert.False(video.IsPlayed);
        Assert.NotNull(folder);
        Assert.Equal("ep-01", folder.LastItemId);
        Assert.NotNull(session);
        Assert.Equal("folder-01", session.FolderId);
        Assert.Equal("ep-01", session.CurrentItemId);
        Assert.Equal(1.25, session.PreferredRate);
        Assert.Equal(65, session.PreferredVolume);
    }

    [Fact]
    public void MarkVideoPlayed_ClearsSavedPosition()
    {
        var path = CreateTempPath();
        var store = CreateStore(path);

        store.SaveVideoProgress("D:/Anime/A/ep01.mp4", 123_000, 1_440_000);
        store.MarkVideoPlayed("D:/Anime/A/ep01.mp4");

        var reloaded = CreateStore(path);
        var video = reloaded.GetVideoProgress("D:/Anime/A/ep01.mp4");

        Assert.NotNull(video);
        Assert.Equal(0, video.Position);
        Assert.Equal(1_440_000, video.Duration);
        Assert.True(video.IsPlayed);
    }

    [Fact]
    public void ClearLastSession_RemovesPersistedSession()
    {
        var path = CreateTempPath();
        var store = CreateStore(path);

        store.SaveLastSession(new PlaybackSessionState("folder-01", "ep-01", 1.25, 65));
        store.ClearLastSession();

        var reloaded = CreateStore(path);

        Assert.Null(reloaded.GetLastSession());
    }

    private static FilePlaybackProgressStore CreateStore(string path)
        => new(
            path,
            PlaybackProgressDefaults.CreateVideoProgress(),
            PlaybackProgressDefaults.CreateFolderProgress());

    private static string CreateTempPath()
        => Path.Combine(Path.GetTempPath(), "AniNest.Backend.Tests", $"{Guid.NewGuid():N}.progress.json");
}
