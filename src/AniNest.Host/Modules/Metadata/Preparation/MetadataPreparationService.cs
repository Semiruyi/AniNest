using AniNest.Application.Library;
using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal sealed class MetadataPreparationService : IMetadataPreparationService
{
    private readonly ILibraryFileScanner _scanner;

    public MetadataPreparationService(ILibraryFileScanner scanner)
    {
        _scanner = scanner;
    }

    public async Task<MetadataPreparedContext> PrepareAsync(
        MetadataRecord record,
        CancellationToken cancellationToken)
    {
        var videoFiles = await _scanner.GetVideoFilesAsync(record.FolderPath, cancellationToken);
        var folderName = string.IsNullOrWhiteSpace(record.FolderName)
            ? record.FolderId
            : record.FolderName;
        var parentName = Path.GetDirectoryName(record.FolderPath) is { } parentPath
            ? Path.GetFileName(parentPath)
            : null;
        var analysis = MetadataPreparationAnalyzer.Analyze(folderName, parentName, videoFiles);
        var folder = new MetadataFolderRef(
            record.FolderId,
            record.FolderPath,
            folderName,
            parentName,
            videoFiles,
            videoFiles.Count);

        return new MetadataPreparedContext(
            record,
            folder,
            analysis.KeywordPlan,
            analysis.SearchSeed,
            analysis.BaseTitle,
            analysis.Aliases,
            analysis.SeasonNumber,
            analysis.YearHint,
            analysis.IsMovieLike);
    }
}
