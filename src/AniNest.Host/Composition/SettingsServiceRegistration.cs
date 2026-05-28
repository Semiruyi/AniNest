using AniNest.Application.Modules;
using AniNest.Application.Settings;
using AniNest.Host.Modules;

namespace AniNest.Host.Composition;

internal static class SettingsServiceRegistration
{
    public static IServiceCollection AddSettingsServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<ISettingsStore>(sp => new FileSettingsStore(
            configuration.ResolveAniNestPath("AniNest:SettingsPath", "host-settings.json"),
            SettingsDefaults.Create()));
        services.AddSingleton<SettingsService>();
        services.AddSingleton<ISettingsModule, SettingsModule>();
        return services;
    }
}
