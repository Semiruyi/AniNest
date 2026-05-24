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
        };

    public static string FromPath(string path)
    {
        var extension = Path.GetExtension(path);
        return KnownTypes.TryGetValue(extension, out var contentType)
            ? contentType
            : "application/octet-stream";
    }
}
