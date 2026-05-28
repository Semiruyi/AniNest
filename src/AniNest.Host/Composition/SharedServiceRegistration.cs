using AniNest.Application.Resources;
using AniNest.Host.Events;
using AniNest.Host.Modules.Resources;

namespace AniNest.Host.Composition;

internal static class SharedServiceRegistration
{
    public static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        services.AddSingleton<IHostEventStream, InMemoryHostEventStream>();
        services.AddSingleton<IResourceUrlService, ResourceUrlService>();
        return services;
    }
}
