using System.Net;
using System.Net.Http.Json;
using AniNest.Contracts.Common;
using AniNest.Contracts.Library;
using AniNest.Contracts.Playlist;
using AniNest.Contracts.Session;
using AniNest.Contracts.Settings;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AniNest.Backend.Tests;

public sealed class HostScaffoldTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HostScaffoldTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

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
        => _factory.WithWebHostBuilder(_ => { }).CreateClient();
}
