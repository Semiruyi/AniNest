using AniNest.Contracts.Settings;

namespace AniNest.Application.Settings;

public interface ISettingsStore
{
    AppSettingsDto Load();
    void Save(AppSettingsDto settings);
}
