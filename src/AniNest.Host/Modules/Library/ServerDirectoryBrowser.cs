using AniNest.Contracts.Library;

namespace AniNest.Host.Modules;

internal sealed class ServerDirectoryBrowser
{
    internal const string AllowedRootPath = "/Volumes/WD1T/y-s/anime";

    private readonly string _allowedRootPath;

    public ServerDirectoryBrowser(string? allowedRootPath = null)
    {
        _allowedRootPath = NormalizePath(allowedRootPath ?? AllowedRootPath);
    }

    public Task<LibraryBrowserResponse> BrowseAsync(string? path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(_allowedRootPath))
        {
            throw new DirectoryNotFoundException(
                $"Configured library browser root '{_allowedRootPath}' does not exist.");
        }

        var targetPath = string.IsNullOrWhiteSpace(path)
            ? _allowedRootPath
            : NormalizePath(path);

        EnsurePathAllowed(targetPath);

        if (!Directory.Exists(targetPath))
        {
            throw new DirectoryNotFoundException(
                $"Library browser path '{targetPath}' does not exist.");
        }

        var parentPath = string.Equals(targetPath, _allowedRootPath, StringComparison.Ordinal)
            ? null
            : GetParentPath(targetPath);

        var directories = Directory.EnumerateDirectories(targetPath)
            .Select(NormalizePath)
            .Where(IsPathAllowed)
            .OrderBy(static item => item, StringComparer.OrdinalIgnoreCase)
            .Select(static item => new LibraryBrowserDirectoryDto(
                Path.GetFileName(item.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                item))
            .ToArray();

        var response = new LibraryBrowserResponse(
            _allowedRootPath,
            targetPath,
            parentPath,
            true,
            directories);

        return Task.FromResult(response);
    }

    private string? GetParentPath(string path)
    {
        var parent = Directory.GetParent(path)?.FullName;
        if (string.IsNullOrWhiteSpace(parent))
        {
            return null;
        }

        var normalizedParent = NormalizePath(parent);
        return IsPathAllowed(normalizedParent)
            ? normalizedParent
            : _allowedRootPath;
    }

    private void EnsurePathAllowed(string path)
    {
        if (!IsPathAllowed(path))
        {
            throw new ArgumentException(
                $"Library browser path '{path}' is outside the allowed root '{_allowedRootPath}'.",
                nameof(path));
        }
    }

    private bool IsPathAllowed(string path)
        => string.Equals(path, _allowedRootPath, StringComparison.Ordinal) ||
           path.StartsWith(_allowedRootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static string NormalizePath(string path)
        => Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
