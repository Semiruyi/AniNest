using System.Net;
using System.Net.Http.Json;
using AniNest.Contracts.Common;
using AniNest.Contracts.Library;
using AniNest.Contracts.Playlist;
using AniNest.Contracts.Session;
using AniNest.Contracts.Settings;
using AniNest.Host.Modules;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AniNest.Backend.Tests;

public sealed class HostScaffoldTests
{
    [Fact]
    public async Task GetSettings_ReturnsSeededSettings()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/settings");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AppSettingsDto>();
        Assert.NotNull(payload);
        Assert.Equal(1.0, payload.Player.PreferredRate);
        Assert.Equal(80, payload.Player.PreferredVolume);
    }

    [Fact]
    public async Task SavePlayerSettings_UpdatesPlayerSnapshot()
    {
        using var client = CreateClient();

        var updateResponse = await client.PutAsJsonAsync("/api/settings/player", new PlayerSettingsDto(3.5, 66, false));
        updateResponse.EnsureSuccessStatusCode();

        var payload = await client.GetFromJsonAsync<PlayerSettingsDto>("/api/settings/player");

        Assert.NotNull(payload);
        Assert.Equal(3.5, payload.PreferredRate);
        Assert.Equal(66, payload.PreferredVolume);
        Assert.False(payload.ResumePlayback);
    }

    [Fact]
    public async Task GetLibraryFolders_ReturnsSampleFolder()
    {
        using var client = CreateClient();

        var payload = await client.GetFromJsonAsync<LibraryFolderListResponse>("/api/library/folders");

        Assert.NotNull(payload);
        Assert.Single(payload.Items);
        Assert.Equal("sample-folder", payload.Items[0].FolderId);
    }

    [Fact]
    public async Task SetLibraryFavorite_UpdatesFolderState()
    {
        using var client = CreateClient();

        var updateResponse = await client.PostAsJsonAsync("/api/library/folders/sample-folder:favorite", new SetFavoriteRequest(false));
        updateResponse.EnsureSuccessStatusCode();

        var payload = await client.GetFromJsonAsync<LibraryFolderListResponse>("/api/library/folders");

        Assert.NotNull(payload);
        Assert.False(payload.Items[0].IsFavorite);
    }

    [Fact]
    public async Task OpenSessionFolder_ReturnsPlaybackTarget()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/session/open-folder", new SessionOpenFolderRequest("sample-folder"));

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<SessionOpenResultDto>();
        Assert.NotNull(payload);
        Assert.Equal("sample-folder", payload.Session.FolderId);
        Assert.Equal("ep-01", payload.PlaybackTarget.ItemId);
    }

    [Fact]
    public async Task CurrentPlaylistEndpoint_ReturnsPlaylist()
    {
        using var client = CreateClient();

        var payload = await client.GetFromJsonAsync<PlaylistDto>("/api/playlist/current");

        Assert.NotNull(payload);
        Assert.Equal("sample-folder", payload.FolderId);
        Assert.Equal("ep-01", payload.CurrentItemId);
        Assert.Equal(12, payload.Items.Count);
    }

    [Fact]
    public async Task ReportProgress_UpdatesCurrentSessionProgress()
    {
        using var client = CreateClient();

        var progress = new SessionProgressReportRequest("ep-01", 123_456, 1_440_000, 1.25, 65, false);
        var progressResponse = await client.PostAsJsonAsync("/api/session/progress", progress);
        progressResponse.EnsureSuccessStatusCode();

        var current = await client.GetFromJsonAsync<SessionStateDto>("/api/session");

        Assert.NotNull(current);
        Assert.Equal(123_456, current.SavedProgressMs);
        Assert.Equal(1.25, current.PreferredRate);
        Assert.Equal(65, current.PreferredVolume);
    }

    [Fact]
    public async Task SelectPlaylistItem_UpdatesSessionAndPlaybackTarget()
    {
        using var client = CreateClient();

        var response = await client.PostAsync("/api/playlist/current/items/ep-03:select", content: null);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SessionOpenResultDto>();

        Assert.NotNull(payload);
        Assert.Equal("ep-03", payload.Session.CurrentItemId);
        Assert.Equal(2, payload.Session.CurrentIndex);
        Assert.Equal("ep-03", payload.PlaybackTarget.ItemId);
    }

    [Fact]
    public async Task MissingPlaylistFolder_ReturnsStructuredNotFoundError()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/playlist/by-folder/missing-folder");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("resource.not_found", payload.Code);
    }

    [Fact]
    public async Task MoveNextWithoutSession_ReturnsConflictError()
    {
        using var client = CreateClient();

        var closeResponse = await client.PostAsync("/api/session/close", content: null);
        closeResponse.EnsureSuccessStatusCode();

        var response = await client.PostAsync("/api/session/next", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("request.conflict", payload.Code);
    }

    private HttpClient CreateClient()
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                var testRoot = Path.Combine(
                    Path.GetTempPath(),
                    "AniNest.Backend.Tests",
                    $"{Guid.NewGuid():N}");
                var sampleFolderPath = Path.Combine(testRoot, "Sample Anime");
                Directory.CreateDirectory(sampleFolderPath);
                for (int index = 1; index <= 12; index++)
                {
                    File.WriteAllText(Path.Combine(sampleFolderPath, $"Episode {index:00}.mp4"), string.Empty);
                }

                var libraryCatalogPath = Path.Combine(testRoot, "library-catalog.json");
                var libraryStore = new FileLibraryCatalogStore(
                    libraryCatalogPath,
                    [
                        new AniNest.Application.Library.LibraryFolderRecord(
                            "sample-folder",
                            "Sample Anime",
                            sampleFolderPath,
                            12,
                            null,
                            new LibraryMetadataSummaryDto("Sample Anime", null),
                            0)
                    ],
                    new Dictionary<string, AniNest.Core.Enums.WatchStatus>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["sample-folder"] = AniNest.Core.Enums.WatchStatus.Watching
                    },
                    new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["sample-folder"] = true
                    });
                libraryStore.SaveFolders(libraryStore.GetFolders());

                var playbackProgressPath = Path.Combine(testRoot, "playback-progress.json");
                var progressStore = new FilePlaybackProgressStore(
                    playbackProgressPath,
                    PlaybackProgressDefaults.CreateVideoProgress(),
                    PlaybackProgressDefaults.CreateFolderProgress());
                progressStore.SaveFolderProgress("sample-folder", "ep-01");
                progressStore.SaveVideoProgress(Path.Combine(sampleFolderPath, "Episode 01.mp4"), 93_000, 1_440_000);

                var metadataPath = Path.Combine(testRoot, "metadata.json");
                var metadataStore = new FileMetadataStore(metadataPath, MetadataDefaults.Create());
                metadataStore.Save(new AniNest.Contracts.Metadata.MetadataDto(
                    "sample-folder",
                    "Sample Anime",
                    "Sample Anime",
                    "A sample metadata record used by the backend scaffold.",
                    ["slice-of-life"],
                    null,
                    "S1",
                    12,
                    "local",
                    AniNest.Core.Enums.MetadataState.Ready,
                    AniNest.Core.Enums.MetadataFailureKind.None));

                var thumbnailPath = Path.Combine(testRoot, "thumbnails.json");

                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AniNest:SettingsPath"] = Path.Combine(testRoot, "host-settings.json"),
                    ["AniNest:LibraryCatalogPath"] = libraryCatalogPath,
                    ["AniNest:PlaybackProgressPath"] = playbackProgressPath,
                    ["AniNest:MetadataPath"] = metadataPath,
                    ["AniNest:ThumbnailPath"] = thumbnailPath
                });
            });
        }).CreateClient();
}
