using AniNest.Features.Metadata;
using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Persistence;

namespace AniNest.Infrastructure.Presentation;

public sealed class ApplicationRuntime : IApplicationRuntime
{
    private static readonly Logger Log = AppLog.For<ApplicationRuntime>();
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;
    private readonly IMediaPlayerController _mediaPlayerController;
    private readonly MetadataWorker _metadataWorker;

    public ApplicationRuntime(
        ISettingsService settingsService,
        ILocalizationService localizationService,
        IMediaPlayerController mediaPlayerController,
        MetadataWorker metadataWorker)
    {
        _settingsService = settingsService;
        _localizationService = localizationService;
        _mediaPlayerController = mediaPlayerController;
        _metadataWorker = metadataWorker;
    }

    public void Start()
    {
        Log.Info("Application runtime start begin");

        var settings = _settingsService.Load();
        Log.Info($"Settings loaded. language={settings.Language}");

        _localizationService.SetLanguage(settings.Language);
        Log.Info($"Localization applied. language={settings.Language}");

        _ = WarmupMediaAsync();
        _metadataWorker.Start();

        Log.Info("Application runtime start complete");
    }

    public void Stop()
    {
        Log.Info("Application runtime stop begin");
        _settingsService.Save();
        Log.Info("Application runtime stop complete");
    }

    private async Task WarmupMediaAsync()
    {
        Log.Info("Media warmup queued");

        try
        {
            await _mediaPlayerController.WarmupAsync();
            Log.Info("Media warmup complete");
        }
        catch (Exception ex)
        {
            Log.Error("Media warmup failed", ex);
        }
    }
}
