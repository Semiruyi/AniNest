using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Shell.Services;

public sealed class ShellThumbnailStatusService : IShellThumbnailStatusService
{
    private readonly IShellPreferencesService _preferencesService;
    private readonly IThumbnailGenerator _thumbnailGenerator;

    public ShellThumbnailStatusService(
        IShellPreferencesService preferencesService,
        IThumbnailGenerator thumbnailGenerator)
    {
        _preferencesService = preferencesService;
        _thumbnailGenerator = thumbnailGenerator;
    }

    public ShellThumbnailStatusSnapshot GetStatusSnapshot()
    {
        var decodeStatus = _preferencesService.CurrentThumbnailDecodeStatus;
        var generationStatus = _thumbnailGenerator.GetStatusSnapshot();
        var generationStatusCode = generationStatus.IsPaused
            ? "paused"
            : generationStatus.ActiveWorkers > 0
                ? "generating"
                : generationStatus.PendingCount > 0
                    ? "waiting"
                    : generationStatus.ReadyCount >= generationStatus.TotalCount && generationStatus.TotalCount > 0
                        ? "complete"
                        : "idle";

        return new ShellThumbnailStatusSnapshot(
            decodeStatus,
            generationStatus,
            generationStatusCode);
    }
}
