using AniNest.Contracts.Settings;

namespace AniNest.Host.Modules;

internal static class SettingsDefaults
{
    public static AppSettingsDto Create()
        => new(
            new LibrarySettingsDto(Array.Empty<string>()),
            new PlayerSettingsDto(1.0, 80, true),
            new MetadataSettingsDto(true, null),
            new ThumbnailSettingsDto(30, true));
}
