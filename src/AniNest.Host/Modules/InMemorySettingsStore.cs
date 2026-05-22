using AniNest.Application.Settings;
using AniNest.Contracts.Settings;

namespace AniNest.Host.Modules;

internal sealed class InMemorySettingsStore : ISettingsStore
{
    private AppSettingsDto _settings = new(
        new LibrarySettingsDto(Array.Empty<string>()),
        new PlayerSettingsDto(1.0, 80, true),
        new MetadataSettingsDto(true),
        new ThumbnailSettingsDto(30, true));

    public AppSettingsDto Load()
        => _settings;

    public void Save(AppSettingsDto settings)
    {
        _settings = settings;
    }
}
