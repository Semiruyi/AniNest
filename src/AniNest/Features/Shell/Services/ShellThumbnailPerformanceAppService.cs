using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Shell.Services;

public sealed class ShellThumbnailPerformanceAppService : IShellThumbnailPerformanceAppService
{
    private readonly ISettingsService _settings;
    private readonly IThumbnailGenerator _thumbnailGenerator;

    public ShellThumbnailPerformanceAppService(
        ISettingsService settings,
        IThumbnailGenerator thumbnailGenerator)
    {
        _settings = settings;
        _thumbnailGenerator = thumbnailGenerator;
    }

    public Task<bool> TrySetPerformanceModeAsync(string code)
        => Task.Run(() => TrySetPerformanceModeCore(code));

    private bool TrySetPerformanceModeCore(string code)
    {
        ThumbnailPerformanceMode mode = ParseMode(code);
        ThumbnailPerformanceMode previousMode = _settings.GetThumbnailPerformanceMode();
        if (previousMode == mode)
            return true;

        if (!_thumbnailGenerator.TryApplyPerformanceMode(mode))
            return false;

        try
        {
            _settings.SetThumbnailPerformanceMode(mode);
            return true;
        }
        catch
        {
            _thumbnailGenerator.TryApplyPerformanceMode(previousMode);
            return false;
        }
    }

    private static ThumbnailPerformanceMode ParseMode(string code)
        => code.ToLowerInvariant() switch
        {
            "paused" => ThumbnailPerformanceMode.Paused,
            "quiet" => ThumbnailPerformanceMode.Quiet,
            "fast" => ThumbnailPerformanceMode.Fast,
            _ => ThumbnailPerformanceMode.Balanced
        };
}
