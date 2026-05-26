namespace AniNest.Host.Modules.Resources;

internal static class ResourceContentTypes
{
    private static readonly IReadOnlyDictionary<string, string> KnownTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp",
            [".gif"] = "image/gif",
            [".bmp"] = "image/bmp",
            [".mp4"] = "video/mp4",
            [".m4v"] = "video/x-m4v",
            [".mkv"] = "video/x-matroska",
            [".webm"] = "video/webm",
            [".avi"] = "video/x-msvideo",
            [".mov"] = "video/quicktime",
            [".wmv"] = "video/x-ms-wmv",
            [".srt"] = "application/x-subrip",
            [".ass"] = "text/x-ssa",
            [".ssa"] = "text/x-ssa",
            [".vtt"] = "text/vtt",
        };

    public static string FromPath(string path)
    {
        var extension = Path.GetExtension(path);
        return KnownTypes.TryGetValue(extension, out var contentType)
            ? contentType
            : "application/octet-stream";
    }
}
