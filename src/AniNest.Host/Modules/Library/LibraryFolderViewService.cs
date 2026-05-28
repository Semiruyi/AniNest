using AniNest.Application.Playback;
using AniNest.Contracts.Library;

namespace AniNest.Host.Modules;

internal sealed class LibraryFolderViewService
{
    private readonly LibraryFolderProjection _folderProjection;
    private readonly LibraryMetadataProjection _metadataProjection;
    private readonly PlaybackProgressSummaryService _playbackProgressSummary;

    public LibraryFolderViewService(
        LibraryFolderProjection folderProjection,
        LibraryMetadataProjection metadataProjection,
        PlaybackProgressSummaryService playbackProgressSummary)
    {
        _folderProjection = folderProjection;
        _metadataProjection = metadataProjection;
        _playbackProgressSummary = playbackProgressSummary;
    }

    public async Task<IReadOnlyList<LibraryFolderDto>> GetFoldersAsync(CancellationToken cancellationToken = default)
    {
        var snapshots = await _folderProjection.LoadFolderSnapshotsAsync(cancellationToken);
        return snapshots
            .Select(Apply)
            .ToArray();
    }

    public async Task<LibraryFolderDto?> GetFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        var snapshots = await _folderProjection.LoadFolderSnapshotsAsync(cancellationToken);
        var snapshot = snapshots.FirstOrDefault(item =>
            string.Equals(item.Folder.FolderId, folderId, StringComparison.OrdinalIgnoreCase));
        return snapshot is null ? null : Apply(snapshot);
    }

    private LibraryFolderDto Apply(LibraryFolderSnapshot snapshot)
    {
        var playbackSummary = _playbackProgressSummary.SummarizeFolder(snapshot.VideoFiles);
        var folder = snapshot.Folder with { PlayedCount = playbackSummary.PlayedCount };
        return _metadataProjection.Apply(folder);
    }
}
