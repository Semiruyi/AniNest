using AniNest.Application.Library;

namespace AniNest.Backend.Tests;

internal sealed class FakeLibraryFileScanner : ILibraryFileScanner
{
    public Dictionary<string, LibraryFolderScanResult> ScanResults { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, IReadOnlyList<string>> BatchResults { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, IReadOnlyList<string>> VideoFilesResults { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<LibraryFolderScanResult> ScanFolderAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ScanResults.TryGetValue(path, out var result))
            return Task.FromResult(result);

        return Task.FromResult(new LibraryFolderScanResult(0, null));
    }

    public Task<IReadOnlyList<string>> FindVideoFoldersAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (BatchResults.TryGetValue(rootPath, out var result))
            return Task.FromResult(result);

        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    public Task<IReadOnlyList<string>> GetVideoFilesAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (VideoFilesResults.TryGetValue(path, out var result))
            return Task.FromResult(result);

        return Task.FromResult<IReadOnlyList<string>>([]);
    }
}
