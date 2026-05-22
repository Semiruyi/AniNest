using AniNest.Application.Settings;
using AniNest.Contracts.Settings;

namespace AniNest.Backend.Tests;

internal sealed class InMemorySettingsStore : ISettingsStore
{
    private AppSettingsDto _settings;

    public InMemorySettingsStore(AppSettingsDto settings)
    {
        _settings = settings;
    }

    public AppSettingsDto Load()
        => _settings;

    public void Save(AppSettingsDto settings)
    {
        _settings = settings;
    }
}
