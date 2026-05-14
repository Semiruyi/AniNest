using System.IO;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Library.Services;

public sealed class LibraryAppService : ILibraryAppService
{
    private static readonly Logger Log = AppLog.For<LibraryAppService>();
    private readonly ISettingsService _settings;
    private readonly ILibraryThumbnailService _thumbnailService;
    private readonly IVideoScanner _videoScanner;

    public LibraryAppService(
        ISettingsService settings,
        ILibraryThumbnailService thumbnailService,
        IVideoScanner videoScanner)
    {
        _settings = settings;
        _thumbnailService = thumbnailService;
        _videoScanner = videoScanner;
    }

    public event EventHandler<ThumbnailProgressEventArgs>? ThumbnailProgressChanged
    {
        add => _thumbnailService.ThumbnailProgressChanged += value;
        remove => _thumbnailService.ThumbnailProgressChanged -= value;
    }

    public async Task<IReadOnlyList<LibraryFolderDto>> LoadLibraryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var loadedItems = new List<(LibraryFolderDto Folder, string[] VideoFiles)>();
        var folders = _settings.GetFolders();

        foreach (var folder in folders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Directory.Exists(folder.Path))
            {
                var result = await _videoScanner.ScanFolderAsync(folder.Path, cancellationToken);
                loadedItems.Add((CreateFolderDto(folder.Name, folder.Path, result.VideoCount, result.CoverPath, result.VideoFiles), result.VideoFiles));
                continue;
            }

            _settings.RemoveFolder(folder.Path);
            _thumbnailService.DeleteFolder(folder.Path);
        }

        foreach (var (folder, videoFiles) in loadedItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnqueueFolderForThumbnails(folder.Path, videoFiles);
        }

        return loadedItems.Select(static item => item.Folder).ToArray();
    }

    public async Task<OpenFolderResult> OpenFolderAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var videos = await _videoScanner.GetVideoFilesAsync(path, cancellationToken);
        if (videos.Length == 0)
            return new OpenFolderResult(false, string.Empty, OpenFolderFailure.NoVideos);

        var folderName = Path.GetFileName(path);
        return new OpenFolderResult(true, folderName);
    }

    public async Task<AddFolderResult> AddFolderAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string name = Path.GetFileName(path);
        var scanResult = await _videoScanner.ScanFolderAsync(path, cancellationToken);
        if (scanResult.VideoCount == 0)
            return new AddFolderResult(false, null, AddFolderFailure.NoVideos);

        var (success, error) = _settings.AddFolder(path, name);
        if (!success)
        {
            var failure = error == "This folder has already been added."
                ? AddFolderFailure.Duplicate
                : AddFolderFailure.Unknown;
            return new AddFolderResult(false, null, failure, error);
        }

        var folder = CreateFolderDto(name, path, scanResult.VideoCount, scanResult.CoverPath, scanResult.VideoFiles);
        EnqueueFolderForThumbnails(path, scanResult.VideoFiles);
        return new AddFolderResult(true, folder);
    }

    public async Task<BatchAddFoldersResult> AddFolderBatchAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var foundFolders = await _videoScanner.FindVideoFoldersAsync(rootPath, cancellationToken);
        var toAdd = foundFolders
            .Select(path => (Path: path, Name: Path.GetFileName(path)))
            .ToList();

        var (addedPaths, skipped) = _settings.AddFoldersBatch(toAdd);
        var addedFolders = new List<LibraryFolderDto>(addedPaths.Count);

        foreach (var path in addedPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scanResult = await _videoScanner.ScanFolderAsync(path, cancellationToken);
            if (scanResult.VideoCount == 0)
                continue;

            addedFolders.Add(CreateFolderDto(
                Path.GetFileName(path),
                path,
                scanResult.VideoCount,
                scanResult.CoverPath,
                scanResult.VideoFiles));

            EnqueueFolderForThumbnails(path, scanResult.VideoFiles);
        }

        return new BatchAddFoldersResult(addedFolders, skipped);
    }

    public Task MoveFolderToFrontAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var orderedPaths = _settings.GetFolders()
            .Select(folder => folder.Path)
            .Where(folderPath => !string.Equals(folderPath, path, StringComparison.OrdinalIgnoreCase))
            .ToList();

        orderedPaths.Insert(0, path);
        _settings.ReorderFolders(orderedPaths);
        return Task.CompletedTask;
    }

    public async Task FocusFolderThumbnailsAsync(string path, CancellationToken cancellationToken = default)
        => await _thumbnailService.FocusFolderThumbnailsAsync(path, cancellationToken);

    public async Task PrioritizeFolderThumbnailsAsync(string path, CancellationToken cancellationToken = default)
        => await _thumbnailService.PrioritizeFolderThumbnailsAsync(path, cancellationToken);

    public async Task RegenerateFolderThumbnailsAsync(string path, CancellationToken cancellationToken = default)
        => await _thumbnailService.RegenerateFolderThumbnailsAsync(path, cancellationToken);

    public async Task ClearFolderThumbnailCacheAsync(string path, CancellationToken cancellationToken = default)
        => await _thumbnailService.ClearFolderThumbnailCacheAsync(path, cancellationToken);

    public async Task<LibraryFolderDto?> ClearFolderWatchHistoryAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(path))
            return null;

        var scanResult = await _videoScanner.ScanFolderAsync(path, cancellationToken);
        _settings.ClearFolderWatchHistory(path);
        return CreateFolderDto(
            Path.GetFileName(path),
            path,
            scanResult.VideoCount,
            scanResult.CoverPath,
            scanResult.VideoFiles);
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

    public Task DeleteFolderAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _settings.RemoveFolder(path);
        _thumbnailService.DeleteFolder(path);
        return Task.CompletedTask;
    }

    public int GetThumbnailExpiryDays()
        => _settings.GetThumbnailExpiryDays();

    public ThumbnailExpirySaveResult SaveThumbnailExpiryDays(string input)
    {
        if (!int.TryParse(input, out int days) || days < 0 || days > 365)
            return new ThumbnailExpirySaveResult(false, ThumbnailExpirySaveOutcome.InvalidInput);

        _settings.SetThumbnailExpiryDays(days);
        return days == 0
            ? new ThumbnailExpirySaveResult(true, ThumbnailExpirySaveOutcome.SavedNever)
            : new ThumbnailExpirySaveResult(true, ThumbnailExpirySaveOutcome.SavedDays, days);
    }

    private void EnqueueFolderForThumbnails(string folderPath, string[] videoFiles)
    {
        _thumbnailService.RegisterFolder(folderPath, videoFiles);
    }

    private LibraryFolderDto CreateFolderDto(
        string name,
        string path,
        int videoCount,
        string? coverPath,
        string[] videoFiles)
    {
        int playedCount = _settings.GetFolderPlayedCount(path, videoFiles);
        var classification = _settings.GetFolderClassification(path);
        return new LibraryFolderDto(name, path, videoCount, coverPath, playedCount, classification.Status, classification.IsFavorite);
    }
}
