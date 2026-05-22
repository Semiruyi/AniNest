using AniNest.Contracts.Settings;

namespace AniNest.Application.Modules;

public interface ISettingsModule
{
    Task<AppSettingsDto> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettingsDto settings, CancellationToken cancellationToken = default);
    Task<PlayerSettingsDto> GetPlayerAsync(CancellationToken cancellationToken = default);
    Task SavePlayerAsync(PlayerSettingsDto settings, CancellationToken cancellationToken = default);
    Task<MetadataSettingsDto> GetMetadataAsync(CancellationToken cancellationToken = default);
    Task SaveMetadataAsync(MetadataSettingsDto settings, CancellationToken cancellationToken = default);
    Task<ThumbnailSettingsDto> GetThumbnailsAsync(CancellationToken cancellationToken = default);
    Task SaveThumbnailsAsync(ThumbnailSettingsDto settings, CancellationToken cancellationToken = default);
}
