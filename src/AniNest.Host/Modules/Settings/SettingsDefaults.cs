using AniNest.Contracts.Settings;

namespace AniNest.Host.Modules;

using Microsoft.Extensions.Configuration;

internal static class SettingsDefaults
{
    public static AppSettingsDto Create(IConfiguration configuration)
        => new(
            new LibrarySettingsDto(Array.Empty<string>()),
            new PlayerSettingsDto(1.0, 80, true),
            new MetadataSettingsDto(
                true,
                null,
                configuration["AniNest:MetadataProxyUrl"]),
            new ThumbnailSettingsDto(30, true));
}
