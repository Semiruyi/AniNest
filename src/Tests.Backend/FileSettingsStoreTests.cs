using AniNest.Contracts.Settings;
using AniNest.Host.Modules;

namespace AniNest.Backend.Tests;

public sealed class FileSettingsStoreTests
{
    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaultSettings()
    {
        var path = CreateTempPath();
        var store = new FileSettingsStore(path, CreateDefaults());

        var settings = store.Load();

        Assert.Equal(1.0, settings.Player.PreferredRate);
        Assert.Equal(80, settings.Player.PreferredVolume);
    }

    [Fact]
    public void Save_PersistsSettingsToDisk()
    {
        var path = CreateTempPath();
        var store = new FileSettingsStore(path, CreateDefaults());
        var updated = new AppSettingsDto(
            new LibrarySettingsDto(["D:/Anime/A"]),
            new PlayerSettingsDto(1.5, 55, false),
            new MetadataSettingsDto(false),
            new ThumbnailSettingsDto(7, false));

        store.Save(updated);

        var reloaded = new FileSettingsStore(path, CreateDefaults()).Load();
        Assert.Equal(1.5, reloaded.Player.PreferredRate);
        Assert.Equal(55, reloaded.Player.PreferredVolume);
        Assert.False(reloaded.Metadata.AutoScrapeMetadata);
        Assert.Equal(7, reloaded.Thumbnails.ExpiryDays);
    }

    private static string CreateTempPath()
        => Path.Combine(Path.GetTempPath(), "AniNest.Backend.Tests", $"{Guid.NewGuid():N}.json");

    private static AppSettingsDto CreateDefaults()
        => new(
            new LibrarySettingsDto(Array.Empty<string>()),
            new PlayerSettingsDto(1.0, 80, true),
            new MetadataSettingsDto(true),
            new ThumbnailSettingsDto(30, true));
}
