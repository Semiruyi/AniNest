using AniNest.Contracts.Library;
using AniNest.Core.Enums;
using AniNest.Application.Resources;
using System.Security.Cryptography;
using System.Text;

namespace AniNest.Application.Library;

public sealed class LibraryCatalogService
{
    private readonly ILibraryCatalogStore _store;
    private readonly ILibraryFileScanner _scanner;
    private readonly IResourceUrlService _resourceUrlService;
    private readonly TimeProvider _timeProvider;

    public LibraryCatalogService(
        ILibraryCatalogStore store,
        ILibraryFileScanner scanner,
        IResourceUrlService resourceUrlService,
        TimeProvider? timeProvider = null)
    {
        _store = store;
        _scanner = scanner;
        _resourceUrlService = resourceUrlService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<LibraryFolderDto>> GetFoldersAsync(CancellationToken cancellationToken = default)
    {
        var orderedFolders = _store.GetFolders()
            .OrderBy(folder => folder.Order)
            .ThenBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rescanned = new List<LibraryFolderRecord>(orderedFolders.Count);
        var changed = false;

        foreach (var folder in orderedFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(folder.Path))
            {
                changed = true;
                continue;
            }

            var scanResult = await _scanner.ScanFolderAsync(folder.Path, cancellationToken);
            var updatedFolder = folder with
            {
                VideoCount = scanResult.VideoCount,
                CoverPath = scanResult.CoverPath
            };

            if (updatedFolder != folder)
                changed = true;

            rescanned.Add(updatedFolder);
        }

        var normalized = rescanned
            .Select((folder, index) => folder with { Order = index })
            .ToArray();

        if (changed)
            _store.SaveFolders(normalized);

        return normalized.Select(MapFolder).ToArray();
    }

    public async Task<AddLibraryFolderResult> AddFolderAsync(AddLibraryFolderRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Path))
            return Failed("Library folder path is required.", "path_required");
        if (!Directory.Exists(request.Path))
            return Failed($"Library folder '{request.Path}' does not exist.", "path_not_found");

        var folders = _store.GetFolders().ToList();
        var normalizedPath = NormalizePath(request.Path);
        var existingByPath = folders.FirstOrDefault(folder =>
            string.Equals(NormalizePath(folder.Path), normalizedPath, StringComparison.OrdinalIgnoreCase));
        if (existingByPath is not null)
        {
            return new AddLibraryFolderResult(
                "alreadyExists",
                $"Library folder '{request.Path}' has already been added.",
                "already_exists",
                MapFolder(existingByPath));
        }

        var folderId = CreateFolderId(request.Path, folders.Select(folder => folder.FolderId));

        var scanResult = await _scanner.ScanFolderAsync(request.Path, cancellationToken);
        if (scanResult.VideoCount == 0)
            return Failed($"Library folder '{request.Path}' does not contain any supported video files.", "no_supported_videos");

        var order = folders.Count == 0 ? 0 : folders.Max(folder => folder.Order) + 1;
        var name = Path.GetFileName(request.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        var addedRecord = new LibraryFolderRecord(
            folderId,
            string.IsNullOrWhiteSpace(name) ? request.Path : name,
            request.Path,
            scanResult.VideoCount,
            scanResult.CoverPath,
            null,
            order,
            _timeProvider.GetUtcNow());
        folders.Add(addedRecord);

        _store.SaveFolders(folders);
        return new AddLibraryFolderResult(
            "added",
            $"Library folder '{request.Path}' was added successfully.",
            null,
            MapFolder(addedRecord));
    }

    public async Task AddFolderBatchAsync(BatchAddLibraryFoldersRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.RootPath))
            throw new ArgumentException("Library root path is required.", nameof(request));
        if (!Directory.Exists(request.RootPath))
            throw new ArgumentException($"Library root path '{request.RootPath}' does not exist.", nameof(request));

        var folders = _store.GetFolders().ToList();
        var knownFolderIds = folders
            .Select(folder => folder.FolderId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var discoveredFolders = await _scanner.FindVideoFoldersAsync(request.RootPath, cancellationToken);
        foreach (var path in discoveredFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedPath = NormalizePath(path);
            if (folders.Any(folder =>
                string.Equals(NormalizePath(folder.Path), normalizedPath, StringComparison.OrdinalIgnoreCase)))
                continue;

            var folderId = CreateFolderId(path, knownFolderIds);

            var scanResult = await _scanner.ScanFolderAsync(path, cancellationToken);
            if (scanResult.VideoCount == 0)
                continue;

            var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var order = folders.Count == 0 ? 0 : folders.Max(folder => folder.Order) + 1;

            folders.Add(new LibraryFolderRecord(
                folderId,
                string.IsNullOrWhiteSpace(name) ? path : name,
                path,
                scanResult.VideoCount,
                scanResult.CoverPath,
                null,
                order,
                _timeProvider.GetUtcNow()));
            knownFolderIds.Add(folderId);
        }

        _store.SaveFolders(folders);
    }

    public void DeleteFolder(string folderId)
    {
        var updated = _store.GetFolders()
            .Where(folder => !string.Equals(folder.FolderId, folderId, StringComparison.OrdinalIgnoreCase))
            .Select((folder, index) => folder with { Order = index })
            .ToArray();

        _store.SaveFolders(updated);
    }

    public void SetFavorite(string folderId, bool isFavorite)
    {
        EnsureFolderExists(folderId);
        _store.SetIsFavorite(folderId, isFavorite);
    }

    public void SetWatchStatus(string folderId, WatchStatus status)
    {
        EnsureFolderExists(folderId);
        _store.SetWatchStatus(folderId, status);
    }

    public void MoveFolderToFront(string folderId)
    {
        var folders = _store.GetFolders().ToList();
        var target = folders.FirstOrDefault(folder => string.Equals(folder.FolderId, folderId, StringComparison.OrdinalIgnoreCase));
        if (target is null)
            throw new KeyNotFoundException($"Library folder '{folderId}' was not found.");

        folders.Remove(target);
        folders.Insert(0, target);
        _store.SaveFolders(folders.Select((folder, index) => folder with { Order = index }).ToArray());
    }

    public LibraryFolderRecord? GetFolderRecord(string folderId)
        => _store.GetFolders().FirstOrDefault(folder => string.Equals(folder.FolderId, folderId, StringComparison.OrdinalIgnoreCase));

    public LibraryFolderDto ApplyMetadataSummary(
        LibraryFolderDto folder,
        string? matchedTitle,
        string? originalTitle,
        string? posterPath,
        string state,
        bool hasMetadata)
    {
        var coverUrl = folder.CoverUrl;
        if (string.IsNullOrWhiteSpace(coverUrl) && !string.IsNullOrWhiteSpace(posterPath))
        {
            coverUrl = _resourceUrlService.GetUrl(
                new ResourceKey(ResourceKind.LibraryCover, folder.FolderId));
        }

        return folder with
        {
            CoverUrl = coverUrl,
            MetadataSummary = new LibraryMetadataSummaryDto(
                matchedTitle,
                originalTitle,
                string.IsNullOrWhiteSpace(posterPath)
                    ? null
                    : _resourceUrlService.GetUrl(
                        new ResourceKey(ResourceKind.LibraryPoster, folder.FolderId)),
                state,
                hasMetadata)
        };
    }

    private LibraryFolderDto MapFolder(LibraryFolderRecord folder)
    {
        var watchStatus = _store.GetWatchStatus(folder.FolderId);
        var isFavorite = _store.GetIsFavorite(folder.FolderId);

        return new LibraryFolderDto(
            folder.FolderId,
            folder.Name,
            folder.VideoCount,
            string.IsNullOrWhiteSpace(folder.CoverPath)
                ? null
                : _resourceUrlService.GetUrl(
                    new ResourceKey(ResourceKind.LibraryCover, folder.FolderId)),
            0,
            watchStatus,
            isFavorite,
            ResolveAddedAtUtc(folder),
            MapMetadataSummary(folder));
    }

    private static DateTimeOffset ResolveAddedAtUtc(LibraryFolderRecord folder)
        => folder.AddedAtUtc == default
            ? DateTimeOffset.UnixEpoch.AddTicks(Math.Max(0, folder.Order))
            : folder.AddedAtUtc;

    private LibraryMetadataSummaryDto? MapMetadataSummary(
        LibraryFolderRecord folder)
    {
        if (folder.MetadataSummary is null)
        {
            return null;
        }

        return new LibraryMetadataSummaryDto(
            folder.MetadataSummary.Title,
            null,
            string.IsNullOrWhiteSpace(folder.MetadataSummary.PosterPath)
                ? null
                : _resourceUrlService.GetUrl(
                    new ResourceKey(ResourceKind.LibraryPoster, folder.FolderId)),
            folder.MetadataSummary.State,
            folder.MetadataSummary.HasMetadata);
    }

    private void EnsureFolderExists(string folderId)
    {
        if (_store.GetFolders().All(folder => !string.Equals(folder.FolderId, folderId, StringComparison.OrdinalIgnoreCase)))
            throw new KeyNotFoundException($"Library folder '{folderId}' was not found.");
    }

    private static string CreateFolderId(string path, IEnumerable<string> existingFolderIds)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(name))
            name = path;

        var baseId = SlugifyFolderName(name);
        if (string.IsNullOrWhiteSpace(baseId))
            baseId = "folder";

        var comparer = StringComparer.OrdinalIgnoreCase;
        if (!existingFolderIds.Contains(baseId, comparer))
            return baseId;

        var suffix = ComputeShortPathHash(path);
        return $"{baseId}-{suffix}";
    }

    private static string SlugifyFolderName(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasSeparator = false;

        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                previousWasSeparator = false;
                continue;
            }

            if (previousWasSeparator)
                continue;

            builder.Append('-');
            previousWasSeparator = true;
        }

        return builder.ToString().Trim('-');
    }

    private static string ComputeShortPathHash(string path)
    {
        var normalized = NormalizePath(path);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexStringLower(bytes)[..8];
    }

    private static string NormalizePath(string path)
        => Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static AddLibraryFolderResult Failed(string message, string reasonCode)
        => new("failed", message, reasonCode, null);
}
