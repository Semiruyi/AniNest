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
    private readonly LibraryFolderProjection _projection;
    private readonly IHostEventStream _events;
    private readonly ILogger<LibraryModule> _logger;

    public LibraryModule(
        LibraryCatalogService catalog,
        LibraryFolderProjection projection,
        IHostEventStream events,
        ILogger<LibraryModule> logger)
    {
        _catalog = catalog;
        _projection = projection;
        _events = events;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LibraryFolderDto>> GetFoldersAsync(CancellationToken cancellationToken = default)
    {
        var folders = await _projection.LoadProjectedFoldersAsync(cancellationToken);
        _logger.LogInformation("Library folders loaded. FolderCount={FolderCount}", folders.Count);
        return folders;
    }

    public async Task<AddLibraryFolderResult> AddFolderAsync(AddLibraryFolderRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _catalog.AddFolderAsync(request, cancellationToken);
        _logger.LogInformation("Library add folder processed. Status={Status}, Path={Path}", result.Status, request.Path);
        if (string.Equals(result.Status, "added", StringComparison.OrdinalIgnoreCase))
        {
            var folders = await _projection.LoadProjectedFoldersAsync(cancellationToken);
            var addedFolder = folders
                .FirstOrDefault(folder => string.Equals(folder.FolderId, result.Folder?.FolderId, StringComparison.OrdinalIgnoreCase));

            _events.Publish("library.folder_added", new
            {
                folderId = result.Folder?.FolderId,
                path = request.Path,
                folder = addedFolder is null ? null : MapFolderEventPayload(addedFolder)
            });
        }

        return result;
    }

    public async Task AddFolderBatchAsync(BatchAddLibraryFoldersRequest request, CancellationToken cancellationToken = default)
    {
        var before = await _catalog.GetFoldersAsync(cancellationToken);
        await _catalog.AddFolderBatchAsync(request, cancellationToken);
        var after = await _projection.LoadProjectedFoldersAsync(cancellationToken);
        _logger.LogInformation("Library batch add processed. BeforeCount={BeforeCount}, AfterCount={AfterCount}, RootPath={RootPath}", before.Count, after.Count, request.RootPath);
        var existingIds = before.Select(folder => folder.FolderId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in after.Where(folder => !existingIds.Contains(folder.FolderId)))
        {
            _events.Publish("library.folder_added", new
            {
                folderId = folder.FolderId,
                folder = MapFolderEventPayload(folder)
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
        await PublishLibraryFolderSnapshotEventAsync(
            "library.folder_updated",
            folderId,
            cancellationToken,
            isFavorite: isFavorite);
    }

    public async Task SetWatchStatusAsync(string folderId, WatchStatus status, CancellationToken cancellationToken = default)
    {
        _catalog.SetWatchStatus(folderId, status);
        await PublishLibraryFolderSnapshotEventAsync(
            "library.folder_updated",
            folderId,
            cancellationToken,
            watchStatus: status.ToString());
    }

    public async Task MoveFolderToFrontAsync(string folderId, CancellationToken cancellationToken = default)
    {
        _catalog.MoveFolderToFront(folderId);
        await PublishLibraryFolderSnapshotEventAsync(
            "library.folder_reordered",
            folderId,
            cancellationToken,
            position: 0);
    }

    private async Task PublishLibraryFolderSnapshotEventAsync(
        string type,
        string folderId,
        CancellationToken cancellationToken,
        bool? isFavorite = null,
        string? watchStatus = null,
        int? position = null)
    {
        var folder = await _projection.LoadProjectedFolderAsync(folderId, cancellationToken);
        _events.Publish(
            type,
            BuildFolderEventPayload(
                folderId,
                isFavorite,
                watchStatus,
                position,
                folder is null ? null : MapFolderEventPayload(folder)));
    }

    private static object BuildFolderEventPayload(
        string folderId,
        bool? isFavorite,
        string? watchStatus,
        int? position,
        object? folder)
        => new
        {
            folderId,
            isFavorite,
            watchStatus,
            position,
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
