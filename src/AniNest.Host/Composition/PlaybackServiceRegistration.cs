using AniNest.Application.Modules;
using AniNest.Application.Playback;
using AniNest.Application.Playlist;
using AniNest.Application.Resources;
using AniNest.Application.Settings;
using AniNest.Host.Modules;

namespace AniNest.Host.Composition;

internal static class PlaybackServiceRegistration
{
    public static IServiceCollection AddPlaybackServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IPlaybackProgressStore>(_ => new FilePlaybackProgressStore(
            configuration.ResolveAniNestPath("AniNest:PlaybackProgressPath", "playback-progress.json"),
            PlaybackProgressDefaults.CreateVideoProgress(),
            PlaybackProgressDefaults.CreateFolderProgress()));
        services.AddSingleton<IPlaylistCatalogStore, FileSystemPlaylistCatalogStore>();
        services.AddSingleton<PlaylistCatalogService>();
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsStore>().Load().Player;
            return new PlaybackSessionEngine(
                sp.GetRequiredService<PlaylistCatalogService>(),
                settings,
                sp.GetRequiredService<IPlaybackProgressStore>(),
                sp.GetRequiredService<IResourceUrlService>());
        });
        services.AddHostedService<PlaybackStartupService>();
        services.AddSingleton<PlaybackModule>();
        services.AddSingleton<IPlaylistModule>(sp => sp.GetRequiredService<PlaybackModule>());
        services.AddSingleton<ISessionModule>(sp => sp.GetRequiredService<PlaybackModule>());
        return services;
    }
}
