using AniNest.Application.Library;
using AniNest.Application.Modules;
using AniNest.Application.Playback;
using AniNest.Host.Modules;

namespace AniNest.Host.Composition;

internal static class LibraryServiceRegistration
{
    public static IServiceCollection AddLibraryServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<FileSystemVideoFolderDiscovery>();
        services.AddSingleton<ILibraryFileScanner, FileSystemLibraryFileScanner>();
        services.AddSingleton(_ => new ServerDirectoryBrowser(configuration["AniNest:LibraryBrowserRootPath"]));
        services.AddSingleton<ILibraryCatalogStore>(_ => new FileLibraryCatalogStore(
            configuration.ResolveAniNestPath("AniNest:LibraryCatalogPath", "library-catalog.json"),
            LibraryCatalogDefaults.CreateFolders(),
            LibraryCatalogDefaults.CreateWatchStatuses(),
            LibraryCatalogDefaults.CreateFavorites()));
        services.AddSingleton<LibraryCatalogService>();
        services.AddSingleton<PlaybackProgressSummaryService>();
        services.AddSingleton<LibraryMetadataSyncService>();
        services.AddSingleton<LibraryFolderProjection>();
        services.AddSingleton<LibraryMetadataProjection>();
        services.AddSingleton<LibraryFolderViewService>();
        services.AddHostedService<LibraryMetadataSyncStartupService>();
        services.AddSingleton<ILibraryModule, LibraryModule>();
        return services;
    }
}
