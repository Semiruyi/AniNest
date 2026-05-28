namespace AniNest.Host.Composition;

internal static class HostServiceRegistration
{
    public static IServiceCollection AddAniNestHostServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSharedServices();
        services.AddLibraryServices(configuration);
        services.AddPlaybackServices(configuration);
        services.AddThumbnailServices(configuration);
        services.AddSettingsServices(configuration);
        services.AddMetadataServices(configuration);
        services.AddResourceServices(configuration);

        return services;
    }
}
