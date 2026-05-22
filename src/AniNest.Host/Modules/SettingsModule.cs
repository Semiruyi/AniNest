using AniNest.Application.Modules;
using AniNest.Application.Settings;
using AniNest.Contracts.Settings;

namespace AniNest.Host.Modules;

internal sealed class SettingsModule : ISettingsModule
{
    private readonly SettingsService _settings;

    public SettingsModule(ISettingsStore store)
    {
        _settings = new SettingsService(store);
    }

    public Task<AppSettingsDto> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_settings.Get());

    public Task SaveAsync(AppSettingsDto settings, CancellationToken cancellationToken = default)
    {
        _settings.Save(settings);
        return Task.CompletedTask;
    }

    public Task<PlayerSettingsDto> GetPlayerAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_settings.GetPlayer());

    public Task SavePlayerAsync(PlayerSettingsDto settings, CancellationToken cancellationToken = default)
    {
        _settings.SavePlayer(settings);
        return Task.CompletedTask;
    }

    public Task<MetadataSettingsDto> GetMetadataAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_settings.GetMetadata());

    public Task SaveMetadataAsync(MetadataSettingsDto settings, CancellationToken cancellationToken = default)
    {
        _settings.SaveMetadata(settings);
        return Task.CompletedTask;
    }

    public Task<ThumbnailSettingsDto> GetThumbnailsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_settings.GetThumbnails());

    public Task SaveThumbnailsAsync(ThumbnailSettingsDto settings, CancellationToken cancellationToken = default)
    {
        _settings.SaveThumbnails(settings);
        return Task.CompletedTask;
    }
}
