using AniNest.Application.Library;
using AniNest.Application.Metadata;
using AniNest.Contracts.Library;

namespace AniNest.Host.Modules;

internal sealed class LibraryMetadataProjection
{
    private readonly LibraryCatalogService _catalog;
    private readonly IMetadataRuntimeStateService _metadataState;

    public LibraryMetadataProjection(
        LibraryCatalogService catalog,
        IMetadataRuntimeStateService metadataState)
    {
        _catalog = catalog;
        _metadataState = metadataState;
    }

    public LibraryFolderDto Apply(LibraryFolderDto folder)
    {
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
}
