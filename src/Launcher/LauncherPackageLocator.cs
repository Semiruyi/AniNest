using System.IO.Compression;

namespace LocalPlayer.Launcher;

public static class LauncherPackageLocator
{
    public static string? FindPendingPackage(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        if (!Directory.Exists(root))
            return null;

        var candidates = Directory.GetFiles(root, "*.zip")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        foreach (var candidate in candidates)
        {
            try
            {
                using var archive = ZipFile.OpenRead(candidate);
                if (archive.GetEntry("manifest.json") != null)
                    return candidate;
            }
            catch
            {
            }
        }

        return null;
    }
}
