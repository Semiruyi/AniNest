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
        var normalizedTitle = NormalizeTitle(folderName);
        var aliases = BuildAliases(folderName, parentName, videoFiles);
        var isMovieLike = IsMovieLike(folderName);
        var folder = new MetadataFolderRef(
            record.FolderId,
            record.FolderPath,
            folderName,
            parentName,
            videoFiles,
            videoFiles.Count);
        var keywordPlan = new MetadataKeywordPlan(
            normalizedTitle,
            null,
            null,
            normalizedTitle,
            null,
            null,
            normalizedTitle.Length <= 2,
            isMovieLike);

        return new MetadataPreparedContext(
            record,
            folder,
            keywordPlan,
            normalizedTitle,
            aliases,
            null,
            null,
            isMovieLike);
    }

    private static IReadOnlyList<string> BuildAliases(
        string folderName,
        string? parentName,
        IReadOnlyList<string> videoFiles)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NormalizeTitle(folderName)
        };

        if (!string.IsNullOrWhiteSpace(parentName))
            values.Add(NormalizeTitle(parentName));

        foreach (var videoFile in videoFiles.Take(3))
            values.Add(NormalizeTitle(Path.GetFileNameWithoutExtension(videoFile)));

        return values.Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
    }

    private static string NormalizeTitle(string value)
    {
        var normalized = value.Trim();
        normalized = normalized.Replace('_', ' ');
        normalized = normalized.Replace('.', ' ');
        return string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static bool IsMovieLike(string value)
        => value.Contains("剧场", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("movie", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("the movie", StringComparison.OrdinalIgnoreCase);
}
