using System.Net;
using System.Net.Http.Json;
using AniNest.Contracts.Library;
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
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/settings");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AppSettingsDto>();
        Assert.NotNull(payload);
        Assert.Equal(1.0, payload.Player.PreferredRate);
        Assert.Equal(80, payload.Player.PreferredVolume);
    }

    [Fact]
    public async Task GetLibraryFolders_ReturnsSampleFolder()
    {
        using var client = _factory.CreateClient();

        var payload = await client.GetFromJsonAsync<LibraryFolderListResponse>("/api/library/folders");

        Assert.NotNull(payload);
        Assert.Single(payload.Items);
        Assert.Equal("sample-folder", payload.Items[0].FolderId);
    }

    [Fact]
    public async Task OpenSessionFolder_ReturnsPlaybackTarget()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/session/open-folder", new SessionOpenFolderRequest("sample-folder"));

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<SessionOpenResultDto>();
        Assert.NotNull(payload);
        Assert.Equal("sample-folder", payload.Session.FolderId);
        Assert.Equal("ep-01", payload.PlaybackTarget.ItemId);
    }

    [Fact]
    public async Task CurrentPlaylistEndpoint_IsNotImplementedYet()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/playlist/current");

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }
}
