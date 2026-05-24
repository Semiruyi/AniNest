using System.Security.Cryptography;
using System.Text;

namespace AniNest.Host.Modules;

internal static class MetadataStoragePathCodec
{
    public static string GetPayloadPath(string folderId)
    {
        var normalizedFolderId = folderId?.Trim() ?? string.Empty;
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedFolderId)));
        return Path.Combine("records", $"{hash}.json");
    }
}
