using AniNest.Application.Library;
using AniNest.Application.Metadata;
using AniNest.Application.Modules;
using AniNest.Application.Playback;
using AniNest.Application.Resources;
using AniNest.Contracts.Library;
using AniNest.Core.Enums;
using AniNest.Host.Events;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Modules;

internal sealed class LibraryModule : ILibraryModule
{
    private readonly ServerDirectoryBrowser _directoryBrowser = new();
    private readonly LibraryCatalogService _catalog;
    private readonly ILibraryFileScanner _scanner;
    private readonly PlaybackProgressSummaryService _playbackProgressSummary;
    private readonly IMetadataRuntimeBootstrapService _metadataBootstrap;
    private readonly IMetadataLifecycleService _metadataLifecycle;
    private readonly IMetadataRuntimeStateService _metadataState;
    private readonly IHostEventStream _events;
    private readonly ILogger<LibraryModule> _logger;

    public LibraryModule(
        ILibraryCatalogStore store,
        ILibraryFileScanner scanner,
        PlaybackProgressSummaryService playbackProgressSummary,
        IResourceUrlService resourceUrlService,
        IMetadataRuntimeBootstrapService metadataBootstrap,
        IMetadataLifecycleService metadataLifecycle,
        IMetadataRuntimeStateService metadataState,
        IHostEventStream events,
        ILogger<LibraryModule> logger)
    {
        _catalog = new LibraryCatalogService(store, scanner, resourceUrlService);
        _scanner = scanner;
        _playbackProgressSummary = playbackProgressSummary;
        _metadataBootstrap = metadataBootstrap;
        _metadataLifecycle = metadataLifecycle;
        _metadataState = metadataState;
        _events = events;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LibraryFolderDto>> GetFoldersAsync(CancellationToken cancellationToken = default)
    {
        var folders = await _catalog.GetFoldersAsync(cancellationToken);
        _logger.LogInformation("Library folders loaded. FolderCount={FolderCount}", folders.Count);
        var foldersWithPlayback = await ApplyPlaybackSummariesAsync(folders, cancellationToken);
        var snapshot = await BuildSnapshotAsync(foldersWithPlayback, cancellationToken);
        _logger.LogInformation("Library metadata snapshot built. FolderCount={FolderCount}", snapshot.Count);
        await _metadataLifecycle.SyncLibrarySnapshotAsync(snapshot, cancellationToken);
        return foldersWithPlayback.Select(ApplyMetadataSummary).ToArray();
    }

    public async Task<AddLibraryFolderResult> AddFolderAsync(AddLibraryFolderRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _catalog.AddFolderAsync(request, cancellationToken);
        _logger.LogInformation("Library add folder processed. Status={Status}, Path={Path}", result.Status, request.Path);
        if (string.Equals(result.Status, "added", StringComparison.OrdinalIgnoreCase))
        {
            var folders = await _catalog.GetFoldersAsync(cancellationToken);
            var snapshot = await BuildSnapshotAsync(folders, cancellationToken);
            await _metadataLifecycle.SyncLibrarySnapshotAsync(snapshot, cancellationToken);
            var addedFolder = folders
                .FirstOrDefault(folder => string.Equals(folder.FolderId, result.Folder?.FolderId, StringComparison.OrdinalIgnoreCase));
            var addedSnapshot = addedFolder is null ? null : ApplyMetadataSummary(addedFolder);

            _events.Publish("library.folder_added", new
            {
                folderId = result.Folder?.FolderId,
                path = request.Path,
                folder = addedSnapshot is null ? null : MapFolderEventPayload(addedSnapshot)
            });
        }

        return result;
    }

    public async Task AddFolderBatchAsync(BatchAddLibraryFoldersRequest request, CancellationToken cancellationToken = default)
    {
        var before = await _catalog.GetFoldersAsync(cancellationToken);
        await _catalog.AddFolderBatchAsync(request, cancellationToken);
        var after = await _catalog.GetFoldersAsync(cancellationToken);
        _logger.LogInformation("Library batch add processed. BeforeCount={BeforeCount}, AfterCount={AfterCount}, RootPath={RootPath}", before.Count, after.Count, request.RootPath);
        var snapshot = await BuildSnapshotAsync(after, cancellationToken);
        await _metadataLifecycle.SyncLibrarySnapshotAsync(snapshot, cancellationToken);
        var existingIds = before.Select(folder => folder.FolderId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in after.Where(folder => !existingIds.Contains(folder.FolderId)))
        {
            _events.Publish("library.folder_added", new
            {
                folderId = folder.FolderId,
                folder = MapFolderEventPayload(ApplyMetadataSummary(folder))
            });
        }
    }

    public Task<LibraryBrowserResponse> BrowseAsync(string? path, CancellationToken cancellationToken = default)
        => _directoryBrowser.BrowseAsync(path, cancellationToken);

    public Task DeleteFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        _catalog.DeleteFolder(folderId);
        _logger.LogInformation("Library folder deleted. FolderId={FolderId}", folderId);
        _events.Publish("library.folder_removed", new { folderId });
        return Task.CompletedTask;
    }

    public async Task SetFavoriteAsync(string folderId, bool isFavorite, CancellationToken cancellationToken = default)
    {
        _catalog.SetFavorite(folderId, isFavorite);
        await PublishLibraryFolderSnapshotEventAsync("library.folder_updated", folderId, cancellationToken, new
        {
            folderId,
            isFavorite
        });
    }

    public async Task SetWatchStatusAsync(string folderId, WatchStatus status, CancellationToken cancellationToken = default)
    {
        _catalog.SetWatchStatus(folderId, status);
        await PublishLibraryFolderSnapshotEventAsync("library.folder_updated", folderId, cancellationToken, new
        {
            folderId,
            watchStatus = status.ToString()
        });
    }

    public async Task MoveFolderToFrontAsync(string folderId, CancellationToken cancellationToken = default)
    {
        _catalog.MoveFolderToFront(folderId);
        await PublishLibraryFolderSnapshotEventAsync("library.folder_reordered", folderId, cancellationToken, new
        {
            folderId,
            position = 0
        });
    }

    private LibraryFolderDto ApplyMetadataSummary(LibraryFolderDto folder)
    {
        _metadataBootstrap.EnsureInitialized();
        var metadata = _metadataState.GetMetadata(folder.FolderId);
        var summary = _metadataState.GetFolderStateSummary(folder.FolderId);
        return _catalog.ApplyMetadataSummary(
            folder,
            metadata?.Title ?? summary.Title,
            metadata?.OriginalTitle,
            summary.PosterPath,
            summary.State.ToString(),
            summary.HasMetadata);
    }

    private async Task<IReadOnlyList<MetadataFolderRef>> BuildSnapshotAsync(
        IReadOnlyList<LibraryFolderDto> folders,
        CancellationToken cancellationToken)
    {
        var result = new List<MetadataFolderRef>(folders.Count);
        foreach (var folder in folders)
        {
            var record = _catalog.GetFolderRecord(folder.FolderId);
            if (record is null)
                continue;

            var videoFiles = await _scanner.GetVideoFilesAsync(record.Path, cancellationToken);
            var parentName = Path.GetDirectoryName(record.Path) is { } parentPath
                ? Path.GetFileName(parentPath)
                : null;

            result.Add(new MetadataFolderRef(
                folder.FolderId,
                record.Path,
                record.Name,
                parentName,
                videoFiles,
                record.VideoCount));
        }

        return result;
    }

    private async Task PublishLibraryFolderSnapshotEventAsync(
        string type,
        string folderId,
        CancellationToken cancellationToken,
        object payload)
    {
        var folder = await GetFolderSnapshotAsync(folderId, cancellationToken);
        _events.Publish(type, MergePayload(payload, folder is null ? null : MapFolderEventPayload(folder)));
    }

    private async Task<LibraryFolderDto?> GetFolderSnapshotAsync(
        string folderId,
        CancellationToken cancellationToken)
    {
        var folders = await _catalog.GetFoldersAsync(cancellationToken);
        var folder = folders.FirstOrDefault(item =>
            string.Equals(item.FolderId, folderId, StringComparison.OrdinalIgnoreCase));
        if (folder is null)
            return null;

        return ApplyMetadataSummary(await ApplyPlaybackSummaryAsync(folder, cancellationToken));
    }

    private async Task<IReadOnlyList<LibraryFolderDto>> ApplyPlaybackSummariesAsync(
        IReadOnlyList<LibraryFolderDto> folders,
        CancellationToken cancellationToken)
    {
        var result = new List<LibraryFolderDto>(folders.Count);
        foreach (var folder in folders)
        {
            result.Add(await ApplyPlaybackSummaryAsync(folder, cancellationToken));
        }

        return result;
    }

    private async Task<LibraryFolderDto> ApplyPlaybackSummaryAsync(
        LibraryFolderDto folder,
        CancellationToken cancellationToken)
    {
        var record = _catalog.GetFolderRecord(folder.FolderId);
        if (record is null)
            return folder;

        var videoFiles = await _scanner.GetVideoFilesAsync(record.Path, cancellationToken);
        var summary = _playbackProgressSummary.SummarizeFolder(videoFiles);
        return folder with { PlayedCount = summary.PlayedCount };
    }

    private static object MergePayload(object payload, object? folder)
        => new
        {
            folderId = payload.GetType().GetProperty("folderId")?.GetValue(payload) as string,
            isFavorite = payload.GetType().GetProperty("isFavorite")?.GetValue(payload) as bool?,
            watchStatus = payload.GetType().GetProperty("watchStatus")?.GetValue(payload) as string,
            position = payload.GetType().GetProperty("position")?.GetValue(payload) as int?,
            folder
        };

    private static object MapFolderEventPayload(LibraryFolderDto folder)
        => new
        {
            folderId = folder.FolderId,
            name = folder.Name,
            videoCount = folder.VideoCount,
            coverUrl = folder.CoverUrl,
            playedCount = folder.PlayedCount,
            watchStatus = folder.WatchStatus.ToString(),
            isFavorite = folder.IsFavorite,
            addedAtUtc = folder.AddedAtUtc,
            metadataSummary = folder.MetadataSummary is null
                ? null
                : new
                {
                    matchedTitle = folder.MetadataSummary.MatchedTitle,
                    originalTitle = folder.MetadataSummary.OriginalTitle,
                    posterUrl = folder.MetadataSummary.PosterUrl,
                    state = folder.MetadataSummary.State,
                    hasMetadata = folder.MetadataSummary.HasMetadata
                }
        };
}
