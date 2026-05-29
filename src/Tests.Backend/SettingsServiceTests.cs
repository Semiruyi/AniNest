using AniNest.Application.Settings;
using AniNest.Contracts.Settings;

namespace AniNest.Backend.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public void SavePlayer_ClampsRateAndVolume()
    {
        var service = CreateService();

        service.SavePlayer(new PlayerSettingsDto(9.0, 180, true));

        var player = service.GetPlayer();
        Assert.Equal(4.0, player.PreferredRate);
        Assert.Equal(100, player.PreferredVolume);
    }

    [Fact]
    public void SaveThumbnails_ClampsExpiryDays()
    {
        var service = CreateService();

        service.SaveThumbnails(new ThumbnailSettingsDto(999, false));

        var thumbnails = service.GetThumbnails();
        Assert.Equal(365, thumbnails.ExpiryDays);
        Assert.False(thumbnails.GenerateOnImport);
    }

    [Fact]
    public void SaveMetadata_TrimsProxyUrl()
    {
        var service = CreateService();

        service.SaveMetadata(new MetadataSettingsDto(true, null, "  http://127.0.0.1:7890  "));

        var metadata = service.GetMetadata();
        Assert.Equal("http://127.0.0.1:7890", metadata.MetadataProxyUrl);
    }

    [Fact]
    public void Save_ReplacesWholeSettingsSnapshot()
    {
        var service = CreateService();
        var updated = new AppSettingsDto(
            new LibrarySettingsDto(["D:/Anime/A", "D:/Anime/B"]),
            new PlayerSettingsDto(1.25, 55, false),
            new MetadataSettingsDto(false, "token-a", "http://127.0.0.1:7890"),
            new ThumbnailSettingsDto(7, false));

        service.Save(updated);

        var settings = service.Get();
        Assert.Equal(2, settings.Library.FolderPaths.Count);
        Assert.Equal(1.25, settings.Player.PreferredRate);
        Assert.False(settings.Metadata.AutoScrapeMetadata);
        Assert.Equal("token-a", settings.Metadata.BangumiAccessToken);
        Assert.Equal("http://127.0.0.1:7890", settings.Metadata.MetadataProxyUrl);
        Assert.Equal(7, settings.Thumbnails.ExpiryDays);
    }

    private static SettingsService CreateService()
    {
        var store = new InMemorySettingsStore(
            new AppSettingsDto(
                new LibrarySettingsDto(Array.Empty<string>()),
                new PlayerSettingsDto(1.0, 80, true),
                new MetadataSettingsDto(true, null, null),
                new ThumbnailSettingsDto(30, true)));

        return new SettingsService(store);
    }
}
