using AniNest.Application.Library;
using AniNest.Application.Metadata;
using AniNest.Application.Playlist;
using AniNest.Application.Resources;
using AniNest.Host.Modules.Resources;

namespace AniNest.Host.Composition;

internal static class ResourceServiceRegistration
{
    public static IServiceCollection AddResourceServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IResourceLocator>(sp => new ResourceLocator(
            sp.GetRequiredService<ILibraryCatalogStore>(),
            sp.GetRequiredService<IMetadataStore>(),
            sp.GetRequiredService<IPlaylistCatalogStore>(),
            configuration.ResolveAniNestPath("AniNest:MetadataPosterRootPath", Path.Combine("metadata", "posters"))));
        return services;
    }
}
