namespace AniNest.Contracts.Settings;

public sealed record LibrarySettingsDto(
    IReadOnlyList<string> FolderPaths);

public sealed record PlayerSettingsDto(
    double PreferredRate,
    int PreferredVolume,
    bool ResumePlayback);

public sealed record MetadataSettingsDto(
    bool AutoScrapeMetadata,
    string? BangumiAccessToken,
    string? MetadataProxyUrl);

public sealed record ThumbnailSettingsDto(
    int ExpiryDays,
    bool GenerateOnImport);

public sealed record AppSettingsDto(
    LibrarySettingsDto Library,
    PlayerSettingsDto Player,
    MetadataSettingsDto Metadata,
    ThumbnailSettingsDto Thumbnails);
