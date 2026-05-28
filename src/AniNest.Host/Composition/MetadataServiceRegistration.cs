using AniNest.Application.Modules;
using AniNest.Application.Metadata;
using AniNest.Host.Modules;

namespace AniNest.Host.Composition;

internal static class MetadataServiceRegistration
{
    public static IServiceCollection AddMetadataServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IMetadataRecordStore>(_ => new FileMetadataRecordStore(
            configuration.ResolveAniNestPath("AniNest:MetadataIndexPath", Path.Combine("metadata", "index.json")),
            MetadataStorageDefaults.CreateRecords()));
        services.AddSingleton<IMetadataReviewStore>(_ => new FileMetadataReviewStore(
            configuration.ResolveAniNestPath("AniNest:MetadataReviewPath", Path.Combine("metadata", "review.json"))));
        services.AddSingleton<IMetadataPayloadRepository>(_ => new FileMetadataPayloadRepository(
            configuration.ResolveAniNestPath("AniNest:MetadataPayloadRootPath", Path.Combine("metadata", "payload"))));
        services.AddSingleton<IMetadataPosterCache>(_ => new FileMetadataPosterCache(
            configuration.ResolveAniNestPath("AniNest:MetadataPosterRootPath", Path.Combine("metadata", "posters"))));
        services.AddSingleton<IMetadataReadyStateService, MetadataReadyStateService>();
        services.AddSingleton<IMetadataPendingStateService, MetadataPendingStateService>();
        services.AddSingleton<IMetadataAssetService, MetadataAssetService>();
        services.AddSingleton<IMetadataProjectionService, MetadataProjectionService>();
        services.AddSingleton<IMetadataReviewService, MetadataReviewService>();
        services.AddSingleton<IMetadataOrchestrationService, MetadataOrchestrationService>();
        services.AddSingleton<IMetadataPreparationService, MetadataPreparationService>();
        services.AddSingleton<IMetadataAcquisitionService, MetadataAcquisitionService>();
        services.AddSingleton<IMetadataConfidenceService, MetadataConfidenceService>();
        services.AddSingleton<IMetadataResolutionService, MetadataResolutionService>();
        services.AddSingleton<IMetadataFetchPipeline, MetadataFetchPipeline>();
        services.AddSingleton<IMetadataTaskPlanner, MetadataTaskPlanner>();
        services.AddSingleton<IMetadataTaskQueue, MetadataTaskQueue>();
        services.AddSingleton<IMetadataTaskScheduler, MetadataTaskScheduler>();
        services.AddSingleton<IMetadataRuntimeStateService, MetadataRuntimeStateService>();
        services.AddSingleton<IMetadataModule, MetadataModule>();
        if (configuration.GetValue("AniNest:MetadataWorkerEnabled", true))
            services.AddHostedService<MetadataBackgroundService>();
        services.AddHttpClient<IAnimeMetadataProvider, BangumiMetadataProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.bgm.tv/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        return services;
    }
}
