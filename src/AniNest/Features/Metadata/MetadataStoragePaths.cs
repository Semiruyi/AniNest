using System.IO;
using System.Security.Cryptography;
using System.Text;
using AniNest.Infrastructure.Paths;

namespace AniNest.Features.Metadata;

internal static class MetadataStoragePaths
{
    public static string GetMetadataFilePath(string folderPath)
        => Path.Combine(AppPaths.MetadataDirectory, $"{ComputeHash(folderPath)}.json");

    public static string GetPosterFilePath(string folderPath)
        => Path.Combine(AppPaths.MetadataPosterDirectory, $"{ComputeHash(folderPath)}.jpg");

    private static string ComputeHash(string input)
    {
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }
}
