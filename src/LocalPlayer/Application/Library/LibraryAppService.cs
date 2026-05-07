using System.IO;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Thumbnails;

namespace LocalPlayer.Features.Library.Services;

public sealed class LibraryAppService : ILibraryAppService
{
    private readonly ISettingsService _settings;
    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly IVideoScanner _videoScanner;

    public LibraryAppService(
        ISettingsService settings,
        IThumbnailGenerator thumbnailGenerator,
        IVideoScanner videoScanner)
    {
        _settings = settings;
        _thumbnailGenerator = thumbnailGenerator;
        _videoScanner = videoScanner;
    }

    public async Task<IReadOnlyList<LibraryFolderDto>> LoadLibraryAsync(CancellationToken cancellationToken = default)
    {
        var loadedFolders = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var loadedItems = new List<(LibraryFolderDto Folder, string[] VideoFiles)>();
            var folders = _settings.GetFolders();

            foreach (var folder in folders)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Directory.Exists(folder.Path))
                {
                    var result = _videoScanner.ScanFolder(folder.Path);
                    loadedItems.Add((new LibraryFolderDto(folder.Name, folder.Path, result.VideoCount, result.CoverPath), result.VideoFiles));
                    continue;
                }

                _settings.RemoveFolder(folder.Path);
                _thumbnailGenerator.DeleteForFolder(folder.Path);
            }

            foreach (var (folder, videoFiles) in loadedItems)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnqueueFolderForThumbnails(folder.Path, videoFiles);
            }

            return (IReadOnlyList<LibraryFolderDto>)loadedItems.Select(static item => item.Folder).ToArray();
        }, cancellationToken);

        return loadedFolders;
    }

    public Task<OpenFolderResult> OpenFolderAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var videos = _videoScanner.GetVideoFiles(path);
            if (videos.Length == 0)
            {
                return new OpenFolderResult(false, string.Empty, OpenFolderFailure.NoVideos);
            }

            var folderName = Path.GetFileName(path);
            return new OpenFolderResult(true, folderName);
        }, cancellationToken);
    }

    public Task<AddFolderResult> AddFolderAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            string name = Path.GetFileName(path);
            var scanResult = _videoScanner.ScanFolder(path);
            if (scanResult.VideoCount == 0)
            {
                return new AddFolderResult(false, null, AddFolderFailure.NoVideos);
            }

            var (success, error) = _settings.AddFolder(path, name);
            if (!success)
            {
                var failure = error == "This folder has already been added."
                    ? AddFolderFailure.Duplicate
                    : AddFolderFailure.Unknown;
                return new AddFolderResult(false, null, failure, error);
            }

            var folder = new LibraryFolderDto(name, path, scanResult.VideoCount, scanResult.CoverPath);
            EnqueueFolderForThumbnails(path, scanResult.VideoFiles);
            return new AddFolderResult(true, folder);
        }, cancellationToken);
    }

    public Task<BatchAddFoldersResult> AddFolderBatchAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var foundFolders = _videoScanner.FindVideoFolders(rootPath);
            var toAdd = foundFolders
                .Select(path => (Path: path, Name: Path.GetFileName(path)))
                .ToList();

            var (addedPaths, skipped) = _settings.AddFoldersBatch(toAdd);
            var addedFolders = new List<LibraryFolderDto>(addedPaths.Count);

            foreach (var path in addedPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var scanResult = _videoScanner.ScanFolder(path);
                if (scanResult.VideoCount == 0)
                    continue;

                addedFolders.Add(new LibraryFolderDto(
                    Path.GetFileName(path),
                    path,
                    scanResult.VideoCount,
                    scanResult.CoverPath));

                EnqueueFolderForThumbnails(path, scanResult.VideoFiles);
            }

            return new BatchAddFoldersResult(addedFolders, skipped);
        }, cancellationToken);
    }

    public Task DeleteFolderAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            _settings.RemoveFolder(path);
            _thumbnailGenerator.DeleteForFolder(path);
        }, cancellationToken);
    }

    private void EnqueueFolderForThumbnails(string folderPath, string[] videoFiles)
    {
        int cardOrder = 0;
        var folders = _settings.GetFolders();
        var folderInfo = folders.FirstOrDefault(f => f.Path == folderPath);
        if (folderInfo != null)
            cardOrder = folderInfo.OrderIndex;

        var folderProgress = _settings.GetFolderProgress(folderPath);
        string? lastPlayed = folderProgress?.LastVideoPath;

        var playedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var videoFile in videoFiles)
        {
            if (_settings.IsVideoPlayed(videoFile))
                playedPaths.Add(videoFile);
        }

        _thumbnailGenerator.EnqueueFolder(folderPath, cardOrder, lastPlayed, playedPaths);
    }
}
