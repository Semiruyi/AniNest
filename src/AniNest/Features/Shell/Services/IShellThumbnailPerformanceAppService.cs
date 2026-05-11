namespace AniNest.Features.Shell.Services;

public interface IShellThumbnailPerformanceAppService
{
    Task<bool> TrySetPerformanceModeAsync(string code);
}
