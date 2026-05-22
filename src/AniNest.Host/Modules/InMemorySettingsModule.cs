using AniNest.Application.Modules;
using AniNest.Contracts.Settings;

namespace AniNest.Host.Modules;

internal sealed class InMemorySettingsModule : ISettingsModule
{
    private AppSettingsDto _settings = new(
        new LibrarySettingsDto(Array.Empty<string>()),
        new PlayerSettingsDto(1.0, 80, true),
        new MetadataSettingsDto(true),
        new ThumbnailSettingsDto(30, true));

    public Task<AppSettingsDto> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_settings);

    public Task SaveAsync(AppSettingsDto settings, CancellationToken cancellationToken = default)
    {
        _settings = settings;
        return Task.CompletedTask;
    }

    public Task<PlayerSettingsDto> GetPlayerAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_settings.Player);

    public Task SavePlayerAsync(PlayerSettingsDto settings, CancellationToken cancellationToken = default)
    {
        _settings = _settings with { Player = settings };
        return Task.CompletedTask;
    }

    public Task<MetadataSettingsDto> GetMetadataAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_settings.Metadata);

    public Task SaveMetadataAsync(MetadataSettingsDto settings, CancellationToken cancellationToken = default)
    {
        _settings = _settings with { Metadata = settings };
        return Task.CompletedTask;
    }

    public Task<ThumbnailSettingsDto> GetThumbnailsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_settings.Thumbnails);

    public Task SaveThumbnailsAsync(ThumbnailSettingsDto settings, CancellationToken cancellationToken = default)
    {
        _settings = _settings with { Thumbnails = settings };
        return Task.CompletedTask;
    }
}
