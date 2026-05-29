using AniNest.Application.Modules;
using AniNest.Application.Metadata;
using AniNest.Application.Settings;
using AniNest.Host.Modules;
using System.Net;

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
                client.Timeout = TimeSpan.FromSeconds(
                    Math.Max(5, configuration.GetValue("AniNest:MetadataHttpTimeoutSeconds", 30)));
            })
            .ConfigurePrimaryHttpMessageHandler(sp => BuildMetadataHttpHandler(
                sp.GetRequiredService<SettingsService>()));
        return services;
    }

    private static HttpMessageHandler BuildMetadataHttpHandler(SettingsService settings)
    {
        var handler = new HttpClientHandler();
        handler.Proxy = new MetadataProxy(settings);
        handler.UseProxy = true;
        return handler;
    }

    private sealed class MetadataProxy : IWebProxy
    {
        private readonly SettingsService _settings;

        public MetadataProxy(SettingsService settings)
        {
            _settings = settings;
        }

        public ICredentials? Credentials { get; set; }

        public Uri GetProxy(Uri destination)
        {
            var proxyUri = ResolveProxyUri();
            return proxyUri ?? destination;
        }

        public bool IsBypassed(Uri host)
            => ResolveProxyUri() is null;

        private Uri? ResolveProxyUri()
        {
            var proxyUrl = _settings.GetMetadata().MetadataProxyUrl;
            if (string.IsNullOrWhiteSpace(proxyUrl))
                return null;

            return Uri.TryCreate(proxyUrl, UriKind.Absolute, out var proxyUri)
                ? proxyUri
                : null;
        }
    }
}
