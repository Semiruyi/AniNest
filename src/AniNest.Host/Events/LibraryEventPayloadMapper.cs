using AniNest.Contracts.Library;

namespace AniNest.Host.Events;

internal static class LibraryEventPayloadMapper
{
    public static LibraryFolderAddedEventPayload BuildFolderAdded(
        string? folderId,
        string? path,
        LibraryFolderDto? folder)
        => new(
            folderId,
            path,
            folder is null ? null : MapFolder(folder));

    public static LibraryFolderEventPayload BuildFolderChanged(
        string folderId,
        bool? isFavorite,
        string? watchStatus,
        int? position,
        LibraryFolderDto? folder)
        => new(
            folderId,
            isFavorite,
            watchStatus,
            position,
            folder is null ? null : MapFolder(folder));

    public static LibraryFolderEventFolderPayload MapFolder(LibraryFolderDto folder)
        => new(
            folder.FolderId,
            folder.Name,
            folder.VideoCount,
            folder.CoverUrl,
            folder.PlayedCount,
            folder.WatchStatus.ToString(),
            folder.IsFavorite,
            folder.AddedAtUtc,
            folder.MetadataSummary is null
                ? null
                : new LibraryFolderEventMetadataPayload(
                    folder.MetadataSummary.MatchedTitle,
                    folder.MetadataSummary.OriginalTitle,
                    folder.MetadataSummary.PosterUrl,
                    folder.MetadataSummary.State,
                    folder.MetadataSummary.HasMetadata));
}
