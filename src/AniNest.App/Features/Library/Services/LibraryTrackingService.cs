using System.IO;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Library.Services;

public sealed class LibraryTrackingService : ILibraryTrackingService
{
    private readonly ISettingsService _settings;
    private readonly IVideoScanner _videoScanner;

    public LibraryTrackingService(
        ISettingsService settings,
        IVideoScanner videoScanner)
    {
        _settings = settings;
        _videoScanner = videoScanner;
    }

    public LibraryFolderTrackingSnapshot GetFolderTrackingSnapshot(string folderPath, string[] videoFiles)
    {
        int playedCount = _settings.GetFolderPlayedCount(folderPath, videoFiles);
        var classification = _settings.GetFolderClassification(folderPath);
        return new LibraryFolderTrackingSnapshot(playedCount, classification.Status, classification.IsFavorite);
    }

    public async Task<LibraryFolderDto?> ClearFolderWatchHistoryAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(path))
            return null;

        var scanResult = await _videoScanner.ScanFolderAsync(path, cancellationToken);
        _settings.ClearFolderWatchHistory(path);
        var snapshot = GetFolderTrackingSnapshot(path, scanResult.VideoFiles);
        return new LibraryFolderDto(
            Path.GetFileName(path),
            path,
            scanResult.VideoCount,
            scanResult.CoverPath,
            snapshot.PlayedCount,
            snapshot.Status,
            snapshot.IsFavorite);
    }

    public Task SetFolderWatchStatusAsync(string path, WatchStatus status, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _settings.SetFolderWatchStatus(path, status);
        return Task.CompletedTask;
    }

    public Task SetFolderFavoriteAsync(string path, bool isFavorite, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _settings.SetFolderFavorite(path, isFavorite);
        return Task.CompletedTask;
    }
}
