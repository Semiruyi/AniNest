using System.Net;
using System.Net.Http.Json;
using System.Text;
using AniNest.Application.Library;
using AniNest.Contracts.Common;
using AniNest.Contracts.Library;
using AniNest.Contracts.Metadata;
using AniNest.Contracts.Playlist;
using AniNest.Contracts.Session;
using AniNest.Contracts.Settings;
using AniNest.Contracts.Thumbnails;
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
        Assert.Equal("/api/resources/library-cover/sample-folder", payload.Items[0].CoverUrl);
    }

    [Fact]
    public async Task GetResourceCover_ReturnsImageStream()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/resources/library-cover/sample-folder");

        response.EnsureSuccessStatusCode();
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
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
    public async Task AddLibraryFolder_RejectsFolderWithoutVideos()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "AniNest.Backend.Tests", $"{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
        var emptyFolder = Path.Combine(testRoot, "Empty Folder");
        Directory.CreateDirectory(emptyFolder);

        using var client = CreateClient(testRoot);

        var response = await client.PostAsJsonAsync("/api/library/folders", new AddLibraryFolderRequest(emptyFolder));

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AddLibraryFolderResult>();
        Assert.NotNull(payload);
        Assert.Equal("failed", payload.Status);
        Assert.Equal("no_supported_videos", payload.ReasonCode);
    }

    [Fact]
    public async Task BatchAddLibraryFolders_ImportsOnlyFoldersWithVideos()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "AniNest.Backend.Tests", $"{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
        var importRoot = Path.Combine(testRoot, "Import");
        Directory.CreateDirectory(importRoot);
        var validA = Path.Combine(importRoot, "Season A");
        var validB = Path.Combine(importRoot, "Season B");
        var invalid = Path.Combine(importRoot, "Readme");
        Directory.CreateDirectory(validA);
        Directory.CreateDirectory(validB);
        Directory.CreateDirectory(invalid);
        File.WriteAllText(Path.Combine(validA, "Episode 01.mkv"), string.Empty);
        File.WriteAllText(Path.Combine(validB, "Episode 01.mp4"), string.Empty);
        File.WriteAllText(Path.Combine(invalid, "note.txt"), string.Empty);

        using var client = CreateClient(testRoot);

        var response = await client.PostAsJsonAsync("/api/library/folders:batch-add", new BatchAddLibraryFoldersRequest(importRoot));
        response.EnsureSuccessStatusCode();

        var payload = await client.GetFromJsonAsync<LibraryFolderListResponse>("/api/library/folders");
        Assert.NotNull(payload);
        Assert.Contains(payload.Items, item => item.FolderId == "season-a");
        Assert.Contains(payload.Items, item => item.FolderId == "season-b");
        Assert.DoesNotContain(payload.Items, item => item.FolderId == "readme");
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

    [Fact]
    public async Task MetadataStatusSummary_ReturnsSeededCounts()
    {
        using var client = CreateClient();

        var payload = await client.GetFromJsonAsync<MetadataStatusSummaryDto>("/api/metadata/status-summary");

        Assert.NotNull(payload);
        Assert.Equal(1, payload.Ready);
    }

    [Fact]
    public async Task RefreshMetadataFolder_MovesFolderToQueued()
    {
        using var client = CreateClient();

        var response = await client.PostAsync("/api/metadata/folders/sample-folder:refresh", content: null);
        response.EnsureSuccessStatusCode();

        var payload = await client.GetFromJsonAsync<MetadataDto>("/api/metadata/folders/sample-folder");
        Assert.NotNull(payload);
        Assert.Equal(AniNest.Core.Enums.MetadataState.Queued, payload.State);
        Assert.Equal(AniNest.Core.Enums.MetadataFailureKind.None, payload.FailureKind);
    }

    [Fact]
    public async Task RetryFailedMetadata_RequeuesFailedItems()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "AniNest.Backend.Tests", $"{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
        using var client = CreateClient(testRoot, seedFailedMetadata: true);

        var response = await client.PostAsJsonAsync("/api/metadata:retry-failed", new RetryFailedMetadataRequest(true));
        response.EnsureSuccessStatusCode();

        var payload = await client.GetFromJsonAsync<MetadataDto>("/api/metadata/folders/failed-folder");
        Assert.NotNull(payload);
        Assert.Equal(AniNest.Core.Enums.MetadataState.Queued, payload.State);
        Assert.Equal(AniNest.Core.Enums.MetadataFailureKind.None, payload.FailureKind);
    }

    [Fact]
    public async Task EnqueueMissingMetadata_RequeuesMissingItems()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "AniNest.Backend.Tests", $"{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
        using var client = CreateClient(testRoot, seedMissingMetadata: true);

        var response = await client.PostAsync("/api/metadata:enqueue-missing", content: null);
        response.EnsureSuccessStatusCode();

        var payload = await client.GetFromJsonAsync<MetadataDto>("/api/metadata/folders/missing-folder");
        Assert.NotNull(payload);
        Assert.Equal(AniNest.Core.Enums.MetadataState.Queued, payload.State);
    }

    [Fact]
    public async Task ProcessQueuedMetadata_PromotesItemToReady()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "AniNest.Backend.Tests", $"{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
        using var client = CreateClient(testRoot, seedMissingMetadata: true);

        var enqueue = await client.PostAsync("/api/metadata:enqueue-missing", content: null);
        enqueue.EnsureSuccessStatusCode();

        var response = await client.PostAsync("/api/metadata:process-queue?maxItems=1", content: null);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<MetadataProcessingResultDto>();
        Assert.NotNull(result);
        Assert.Equal(1, result.ProcessedCount);
        Assert.Contains("missing-folder", result.FolderIds);

        var payload = await client.GetFromJsonAsync<MetadataDto>("/api/metadata/folders/missing-folder");
        Assert.NotNull(payload);
        Assert.Equal(AniNest.Core.Enums.MetadataState.Ready, payload.State);
        Assert.Equal("Missing Folder", payload.Title);
        Assert.Equal("placeholder", payload.Source);
    }

    [Fact]
    public async Task ThumbnailFolderSummary_ReturnsDerivedCounts()
    {
        using var client = CreateClient();

        var payload = await client.GetFromJsonAsync<ThumbnailFolderSummaryDto>("/api/thumbnails/folders/sample-folder/summary");

        Assert.NotNull(payload);
        Assert.Equal("sample-folder", payload.FolderId);
        Assert.Equal(12, payload.Total);
        Assert.Equal(12, payload.Pending);
        Assert.Equal(0, payload.Ready);
    }

    [Fact]
    public async Task PrioritizeThumbnailFolder_MarksFolderAsGenerating()
    {
        using var client = CreateClient();

        var response = await client.PostAsync("/api/thumbnails/folders/sample-folder:prioritize", content: null);
        response.EnsureSuccessStatusCode();

        var payload = await client.GetFromJsonAsync<ThumbnailFolderSummaryDto>("/api/thumbnails/folders/sample-folder/summary");
        Assert.NotNull(payload);
        Assert.Equal(12, payload.Generating);
        Assert.Equal(0, payload.Pending);
    }

    [Fact]
    public async Task ClearThumbnailFolderCache_RemovesGeneratedStatuses()
    {
        using var client = CreateClient();

        var prioritize = await client.PostAsync("/api/thumbnails/folders/sample-folder:prioritize", content: null);
        prioritize.EnsureSuccessStatusCode();

        var clear = await client.DeleteAsync("/api/thumbnails/folders/sample-folder/cache");
        clear.EnsureSuccessStatusCode();

        var payload = await client.GetFromJsonAsync<ThumbnailFolderSummaryDto>("/api/thumbnails/folders/sample-folder/summary");
        Assert.NotNull(payload);
        Assert.Equal(12, payload.Pending);
        Assert.Equal(0, payload.Generating);
    }

    [Fact]
    public async Task ProcessThumbnailFolder_GeneratesReadyStatuses()
    {
        using var client = CreateClient();

        var response = await client.PostAsync("/api/thumbnails/folders/sample-folder:process?maxItems=3", content: null);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ThumbnailProcessingResultDto>();
        Assert.NotNull(result);
        Assert.Equal("sample-folder", result.FolderId);
        Assert.Equal(3, result.ProcessedCount);
        Assert.Equal(3, result.Summary.Ready);
        Assert.Equal(9, result.Summary.Pending);

        var video = await client.GetFromJsonAsync<ThumbnailStatusDto>("/api/thumbnails/videos/ep-01");
        Assert.NotNull(video);
        Assert.Equal(AniNest.Core.Enums.ThumbnailState.Ready, video.State);
        Assert.Contains("/generated/thumbnails/sample-folder/ep-01.jpg", video.ImagePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EventStream_ReturnsServerSentEventsPayload()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/api/events", HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var firstLine = await reader.ReadLineAsync();
        var secondLine = await reader.ReadLineAsync();

        Assert.Equal("event: host.connected", firstLine);
        Assert.NotNull(secondLine);
        Assert.StartsWith("data: ", secondLine, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SavingPlayerSettings_PublishesSettingsChangedEvent()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/api/events", HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);

        await reader.ReadLineAsync();
        await reader.ReadLineAsync();
        await reader.ReadLineAsync();

        var updateResponse = await client.PutAsJsonAsync("/api/settings/player", new PlayerSettingsDto(1.5, 70, true));
        updateResponse.EnsureSuccessStatusCode();

        var eventLine = await ReadNextNonEmptyLineAsync(reader);
        var dataLine = await ReadNextNonEmptyLineAsync(reader);

        Assert.Equal("event: settings.changed", eventLine);
        Assert.NotNull(dataLine);
        Assert.Contains("\"scope\":\"player\"", dataLine, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshingMetadata_PublishesFolderUpdatedEventWithFrontendFields()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/api/events", HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);

        await reader.ReadLineAsync();
        await reader.ReadLineAsync();
        await reader.ReadLineAsync();

        var refreshResponse = await client.PostAsync("/api/metadata/folders/sample-folder:refresh", content: null);
        refreshResponse.EnsureSuccessStatusCode();

        var eventLine = await ReadNextNonEmptyLineAsync(reader);
        var dataLine = await ReadNextNonEmptyLineAsync(reader);

        Assert.Equal("event: metadata.folder_updated", eventLine);
        Assert.NotNull(dataLine);
        Assert.Contains("\"folderId\":\"sample-folder\"", dataLine, StringComparison.Ordinal);
        Assert.Contains("\"title\":\"Sample Anime\"", dataLine, StringComparison.Ordinal);
        Assert.Contains("\"hasMetadata\":true", dataLine, StringComparison.Ordinal);
        Assert.Contains("\"coverUrl\":\"/api/resources/library-cover/sample-folder\"", dataLine, StringComparison.Ordinal);
        Assert.Contains("\"posterUrl\":null", dataLine, StringComparison.Ordinal);
        Assert.Contains("\"updatedAtUtc\":", dataLine, StringComparison.Ordinal);
    }

    private HttpClient CreateClient(
        string? explicitTestRoot = null,
        bool seedFailedMetadata = false,
        bool seedMissingMetadata = false)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                var testRoot = explicitTestRoot ?? Path.Combine(
                    Path.GetTempPath(),
                    "AniNest.Backend.Tests",
                    $"{Guid.NewGuid():N}");
                var sampleFolderPath = Path.Combine(testRoot, "Sample Anime");
                Directory.CreateDirectory(sampleFolderPath);
                for (int index = 1; index <= 12; index++)
                {
                    File.WriteAllText(Path.Combine(sampleFolderPath, $"Episode {index:00}.mp4"), string.Empty);
                }
                File.WriteAllBytes(Path.Combine(sampleFolderPath, "poster.jpg"), [0xFF, 0xD8, 0xFF, 0xD9]);

                var libraryCatalogPath = Path.Combine(testRoot, "library-catalog.json");
                var libraryStore = new FileLibraryCatalogStore(
                    libraryCatalogPath,
                    [
                        new AniNest.Application.Library.LibraryFolderRecord(
                            "sample-folder",
                            "Sample Anime",
                            sampleFolderPath,
                            12,
                            Path.Combine(sampleFolderPath, "poster.jpg"),
                            new LibraryFolderMetadataSummary("Sample Anime", null, AniNest.Core.Enums.MetadataState.Ready.ToString(), true),
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
                if (seedFailedMetadata)
                {
                    metadataStore.Save(new AniNest.Contracts.Metadata.MetadataDto(
                        "failed-folder",
                        "Failed Folder",
                        null,
                        null,
                        [],
                        null,
                        null,
                        null,
                        "local",
                        AniNest.Core.Enums.MetadataState.NeedsReview,
                        AniNest.Core.Enums.MetadataFailureKind.NetworkError));
                }

                if (seedMissingMetadata)
                {
                    metadataStore.Save(new AniNest.Contracts.Metadata.MetadataDto(
                        "missing-folder",
                        null,
                        null,
                        null,
                        [],
                        null,
                        null,
                        null,
                        null,
                        AniNest.Core.Enums.MetadataState.NeedsMetadata,
                        AniNest.Core.Enums.MetadataFailureKind.None));
                }

                var thumbnailPath = Path.Combine(testRoot, "thumbnails.json");

                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AniNest:SettingsPath"] = Path.Combine(testRoot, "host-settings.json"),
                    ["AniNest:LibraryCatalogPath"] = libraryCatalogPath,
                    ["AniNest:PlaybackProgressPath"] = playbackProgressPath,
                    ["AniNest:MetadataPath"] = metadataPath,
                    ["AniNest:MetadataIndexPath"] = Path.Combine(testRoot, "metadata", "index.json"),
                    ["AniNest:MetadataPayloadRootPath"] = Path.Combine(testRoot, "metadata", "payload"),
                    ["AniNest:MetadataPosterRootPath"] = Path.Combine(testRoot, "metadata", "posters"),
                    ["AniNest:MetadataWorkerEnabled"] = "false",
                    ["AniNest:ThumbnailPath"] = thumbnailPath
                });
            });
        }).CreateClient();

    private static async Task<string?> ReadNextNonEmptyLineAsync(StreamReader reader)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line is null || line.Length > 0)
            {
                return line;
            }
        }
    }
}
