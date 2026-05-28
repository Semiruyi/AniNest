namespace AniNest.Host.Modules;

internal sealed class FileSystemVideoFolderDiscovery
{
    public IReadOnlyList<string> FindDescendantVideoFolders(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(rootPath))
        {
            return [];
        }

        var discovered = new List<string>();

        foreach (var childPath in Directory.EnumerateDirectories(rootPath))
        {
            Traverse(childPath, discovered, cancellationToken);
        }

        return discovered
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void Traverse(
        string path,
        List<string> discovered,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (IsReparsePoint(path))
        {
            return;
        }

        if (ContainsSupportedVideoFile(path))
        {
            discovered.Add(path);
        }

        foreach (var childPath in Directory.EnumerateDirectories(path))
        {
            Traverse(childPath, discovered, cancellationToken);
        }
    }

    private static bool ContainsSupportedVideoFile(string path)
        => Directory.EnumerateFiles(path).Any(FileSystemLibraryMediaRules.IsSupportedVideoFile);

    private static bool IsReparsePoint(string path)
        => File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
}
