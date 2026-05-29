using AniNest.Contracts.Settings;

namespace AniNest.Application.Settings;

public sealed class SettingsService
{
    private readonly ISettingsStore _store;

    public SettingsService(ISettingsStore store)
    {
        _store = store;
    }

    public AppSettingsDto Get()
        => _store.Load();

    public void Save(AppSettingsDto settings)
        => _store.Save(Normalize(settings));

    public PlayerSettingsDto GetPlayer()
        => _store.Load().Player;

    public void SavePlayer(PlayerSettingsDto settings)
    {
        var current = _store.Load();
        _store.Save(Normalize(current with { Player = NormalizePlayer(settings) }));
    }

    public MetadataSettingsDto GetMetadata()
        => _store.Load().Metadata;

    public void SaveMetadata(MetadataSettingsDto settings)
    {
        var current = _store.Load();
        _store.Save(Normalize(current with { Metadata = NormalizeMetadata(settings) }));
    }

    public ThumbnailSettingsDto GetThumbnails()
        => _store.Load().Thumbnails;

    public void SaveThumbnails(ThumbnailSettingsDto settings)
    {
        var current = _store.Load();
        _store.Save(Normalize(current with { Thumbnails = NormalizeThumbnails(settings) }));
    }

    private static AppSettingsDto Normalize(AppSettingsDto settings)
        => new(
            settings.Library,
            NormalizePlayer(settings.Player),
            NormalizeMetadata(settings.Metadata),
            NormalizeThumbnails(settings.Thumbnails));

    private static PlayerSettingsDto NormalizePlayer(PlayerSettingsDto settings)
        => settings with
        {
            PreferredRate = Math.Clamp(settings.PreferredRate, 0.25, 4.0),
            PreferredVolume = Math.Clamp(settings.PreferredVolume, 0, 100)
        };

    private static ThumbnailSettingsDto NormalizeThumbnails(ThumbnailSettingsDto settings)
        => settings with
        {
            ExpiryDays = Math.Clamp(settings.ExpiryDays, 0, 365)
        };

    private static MetadataSettingsDto NormalizeMetadata(MetadataSettingsDto settings)
        => settings with
        {
            BangumiAccessToken = string.IsNullOrWhiteSpace(settings.BangumiAccessToken)
                ? null
                : settings.BangumiAccessToken.Trim(),
            MetadataProxyUrl = string.IsNullOrWhiteSpace(settings.MetadataProxyUrl)
                ? null
                : settings.MetadataProxyUrl.Trim()
        };
}
