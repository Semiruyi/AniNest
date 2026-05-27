using AniNest.Application.Modules;
using AniNest.Application.Playback;
using AniNest.Application.Settings;
using AniNest.Contracts.Settings;
using AniNest.Host.Events;

namespace AniNest.Host.Modules;

internal sealed class SettingsModule : ISettingsModule
{
    private readonly SettingsService _settings;
    private readonly PlaybackSessionEngine _playback;
    private readonly IHostEventStream _events;

    public SettingsModule(
        SettingsService settings,
        PlaybackSessionEngine playback,
        IHostEventStream events)
    {
        _settings = settings;
        _playback = playback;
        _events = events;
    }

    public Task<AppSettingsDto> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_settings.Get());

    public Task SaveAsync(AppSettingsDto settings, CancellationToken cancellationToken = default)
    {
        _settings.Save(settings);
        _events.Publish("settings.changed", new { scope = "app" });
        return Task.CompletedTask;
    }

    public Task<PlayerSettingsDto> GetPlayerAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_settings.GetPlayer());

    public Task SavePlayerAsync(PlayerSettingsDto settings, CancellationToken cancellationToken = default)
    {
        _settings.SavePlayer(settings);
        _playback.UpdatePlayerSettings(_settings.GetPlayer());
        _events.Publish("settings.changed", new { scope = "player" });
        return Task.CompletedTask;
    }

    public Task<MetadataSettingsDto> GetMetadataAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_settings.GetMetadata());

    public Task SaveMetadataAsync(MetadataSettingsDto settings, CancellationToken cancellationToken = default)
    {
        _settings.SaveMetadata(settings);
        _events.Publish("settings.changed", new { scope = "metadata" });
        return Task.CompletedTask;
    }

    public Task<ThumbnailSettingsDto> GetThumbnailsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_settings.GetThumbnails());

    public Task SaveThumbnailsAsync(ThumbnailSettingsDto settings, CancellationToken cancellationToken = default)
    {
        _settings.SaveThumbnails(settings);
        _events.Publish("settings.changed", new { scope = "thumbnails" });
        return Task.CompletedTask;
    }
}
