using AniNest.Application.Modules;
using AniNest.Application.Thumbnail;
using AniNest.Host.Modules;

namespace AniNest.Host.Composition;

internal static class ThumbnailServiceRegistration
{
    public static IServiceCollection AddThumbnailServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IThumbnailStore>(_ => new FileThumbnailStore(
            configuration.ResolveAniNestPath("AniNest:ThumbnailPath", "thumbnails.json"),
            ThumbnailDefaults.Create()));
        services.AddSingleton<ThumbnailService>();
        services.AddSingleton<ThumbnailFolderProjection>();
        services.AddSingleton<IThumbnailModule, ThumbnailModule>();
        return services;
    }
}
