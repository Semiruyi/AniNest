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
    private readonly ServerDirectoryBrowser _directoryBrowser;
    private readonly LibraryFolderProjection _projection;
    private readonly LibraryMetadataSyncService _metadataSync;
    private readonly PlaybackProgressSummaryService _playbackProgressSummary;
    private readonly LibraryCatalogService _catalog;
    private readonly IHostEventStream _events;
    private readonly ILogger<LibraryModule> _logger;

    public LibraryModule(
        LibraryCatalogService catalog,
        ServerDirectoryBrowser directoryBrowser,
        LibraryFolderProjection projection,
        LibraryMetadataSyncService metadataSync,
        PlaybackProgressSummaryService playbackProgressSummary,
        IHostEventStream events,
        ILogger<LibraryModule> logger)
    {
        _catalog = catalog;
        _directoryBrowser = directoryBrowser;
        _projection = projection;
        _metadataSync = metadataSync;
        _playbackProgressSummary = playbackProgressSummary;
        _events = events;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LibraryFolderDto>> GetFoldersAsync(CancellationToken cancellationToken = default)
    {
        var folders = await LoadProjectedFoldersAsync(cancellationToken);
        _logger.LogInformation("Library folders loaded. FolderCount={FolderCount}", folders.Count);
        return folders;
    }

    public async Task<AddLibraryFolderResult> AddFolderAsync(AddLibraryFolderRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _catalog.AddFolderAsync(request, cancellationToken);
        _logger.LogInformation("Library add folder processed. Status={Status}, Path={Path}", result.Status, request.Path);
        if (string.Equals(result.Status, "added", StringComparison.OrdinalIgnoreCase))
        {
            await _metadataSync.SyncAsync(cancellationToken);
            var folders = await LoadProjectedFoldersAsync(cancellationToken);
            var addedFolder = folders
                .FirstOrDefault(folder => string.Equals(folder.FolderId, result.Folder?.FolderId, StringComparison.OrdinalIgnoreCase));

            _events.Publish(
                "library.folder_added",
                LibraryEventPayloadMapper.BuildFolderAdded(
                    result.Folder?.FolderId,
                    request.Path,
                    addedFolder));
        }

        return result;
    }

    public async Task AddFolderBatchAsync(BatchAddLibraryFoldersRequest request, CancellationToken cancellationToken = default)
    {
        var before = await _catalog.GetFoldersAsync(cancellationToken);
        await _catalog.AddFolderBatchAsync(request, cancellationToken);
        await _metadataSync.SyncAsync(cancellationToken);
        var after = await LoadProjectedFoldersAsync(cancellationToken);
        _logger.LogInformation("Library batch add processed. BeforeCount={BeforeCount}, AfterCount={AfterCount}, RootPath={RootPath}", before.Count, after.Count, request.RootPath);
        var existingIds = before.Select(folder => folder.FolderId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in after.Where(folder => !existingIds.Contains(folder.FolderId)))
        {
            _events.Publish(
                "library.folder_added",
                LibraryEventPayloadMapper.BuildFolderAdded(
                    folder.FolderId,
                    null,
                    folder));
        }
    }

    public Task<LibraryBrowserResponse> BrowseAsync(string? path, CancellationToken cancellationToken = default)
        => _directoryBrowser.BrowseAsync(path, cancellationToken);

    public async Task DeleteFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        _catalog.DeleteFolder(folderId);
        await _metadataSync.SyncAsync(cancellationToken);
        _logger.LogInformation("Library folder deleted. FolderId={FolderId}", folderId);
        _events.Publish("library.folder_removed", new { folderId });
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
        var folder = await LoadProjectedFolderAsync(folderId, cancellationToken);
        _events.Publish(
            type,
            LibraryEventPayloadMapper.BuildFolderChanged(
                folderId,
                isFavorite,
                watchStatus,
                position,
                folder));
    }

    private async Task<IReadOnlyList<LibraryFolderDto>> LoadProjectedFoldersAsync(CancellationToken cancellationToken)
    {
        var snapshots = await _projection.LoadFolderSnapshotsAsync(cancellationToken);
        return snapshots
            .Select(ApplyPlaybackSummary)
            .Select(_projection.ApplyMetadataSummaryForModule)
            .ToArray();
    }

    private async Task<LibraryFolderDto?> LoadProjectedFolderAsync(string folderId, CancellationToken cancellationToken)
    {
        var snapshots = await _projection.LoadFolderSnapshotsAsync(cancellationToken);
        var snapshot = snapshots.FirstOrDefault(item =>
            string.Equals(item.Folder.FolderId, folderId, StringComparison.OrdinalIgnoreCase));
        return snapshot is null ? null : _projection.ApplyMetadataSummaryForModule(ApplyPlaybackSummary(snapshot));
    }

    private LibraryFolderDto ApplyPlaybackSummary(LibraryFolderSnapshot snapshot)
    {
        var summary = _playbackProgressSummary.SummarizeFolder(snapshot.VideoFiles);
        return snapshot.Folder with { PlayedCount = summary.PlayedCount };
    }

}
