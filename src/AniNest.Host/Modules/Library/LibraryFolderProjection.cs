using AniNest.Application.Library;
using AniNest.Application.Metadata;
using AniNest.Application.Playback;
using AniNest.Contracts.Library;

namespace AniNest.Host.Modules;

internal sealed class LibraryFolderProjection
{
    private readonly LibraryCatalogService _catalog;
    private readonly ILibraryFileScanner _scanner;
    private readonly PlaybackProgressSummaryService _playbackProgressSummary;
    private readonly IMetadataRuntimeBootstrapService _metadataBootstrap;
    private readonly IMetadataLifecycleService _metadataLifecycle;
    private readonly IMetadataRuntimeStateService _metadataState;

    public LibraryFolderProjection(
        LibraryCatalogService catalog,
        ILibraryFileScanner scanner,
        PlaybackProgressSummaryService playbackProgressSummary,
        IMetadataRuntimeBootstrapService metadataBootstrap,
        IMetadataLifecycleService metadataLifecycle,
        IMetadataRuntimeStateService metadataState)
    {
        _catalog = catalog;
        _scanner = scanner;
        _playbackProgressSummary = playbackProgressSummary;
        _metadataBootstrap = metadataBootstrap;
        _metadataLifecycle = metadataLifecycle;
        _metadataState = metadataState;
    }

    public async Task<IReadOnlyList<LibraryFolderDto>> LoadProjectedFoldersAsync(
        CancellationToken cancellationToken)
    {
        var folders = await LoadFoldersWithPlaybackAsync(cancellationToken);
        await SyncMetadataSnapshotAsync(folders, cancellationToken);
        return folders.Select(ApplyMetadataSummary).ToArray();
    }

    public async Task<LibraryFolderDto?> LoadProjectedFolderAsync(
        string folderId,
        CancellationToken cancellationToken)
    {
        var folders = await LoadFoldersWithPlaybackAsync(cancellationToken);
        var folder = folders.FirstOrDefault(folder =>
            string.Equals(folder.FolderId, folderId, StringComparison.OrdinalIgnoreCase));
        return folder is null ? null : ApplyMetadataSummary(folder);
    }

    private async Task<IReadOnlyList<LibraryFolderDto>> LoadFoldersWithPlaybackAsync(
        CancellationToken cancellationToken)
    {
        var folders = await _catalog.GetFoldersAsync(cancellationToken);
        var result = new List<LibraryFolderDto>(folders.Count);
        foreach (var folder in folders)
        {
            result.Add(await ApplyPlaybackSummaryAsync(folder, cancellationToken));
        }

        return result;
    }

    private async Task SyncMetadataSnapshotAsync(
        IReadOnlyList<LibraryFolderDto> folders,
        CancellationToken cancellationToken)
    {
        var snapshot = await BuildMetadataSnapshotAsync(folders, cancellationToken);
        await _metadataLifecycle.SyncLibrarySnapshotAsync(snapshot, cancellationToken);
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

    private async Task<IReadOnlyList<MetadataFolderRef>> BuildMetadataSnapshotAsync(
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
}
