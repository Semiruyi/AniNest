using AniNest.Application.Library;
using AniNest.Application.Playlist;
using AniNest.Application.Resources;
using AniNest.Host.Modules.Resources;
using AniNest.Host.Modules;

namespace AniNest.Host.Composition;

internal static class ResourceServiceRegistration
{
    public static IServiceCollection AddResourceServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IResourceLocator>(sp => new ResourceLocator(
            sp.GetRequiredService<ILibraryCatalogStore>(),
            sp.GetRequiredService<IMetadataRuntimeStateService>(),
            sp.GetRequiredService<IPlaylistCatalogStore>(),
            configuration.ResolveAniNestPath("AniNest:MetadataPosterRootPath", Path.Combine("metadata", "posters"))));
        return services;
    }
}
