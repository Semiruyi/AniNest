namespace AniNest.Host.Modules;

internal static class FileSystemLibraryMediaRules
{
    private static readonly string[] SupportedVideoExtensions =
    [
        ".mp4",
        ".mkv",
        ".avi",
        ".mov",
        ".wmv",
        ".m4v",
        ".webm",
        ".flv",
        ".ts"
    ];

    public static readonly string[] SupportedCoverNames =
    [
        "poster.jpg",
        "poster.png",
        "folder.jpg",
        "folder.png",
        "cover.jpg",
        "cover.png"
    ];

    public static bool IsSupportedVideoFile(string path)
        => SupportedVideoExtensions.Contains(
            Path.GetExtension(path),
            StringComparer.OrdinalIgnoreCase);
}
