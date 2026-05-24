using AniNest.Application.Metadata;
using AniNest.Core.Enums;

namespace AniNest.Host.Modules;

internal static class MetadataStorageDefaults
{
    public static IReadOnlyList<MetadataRecord> CreateRecords()
        => [];

    public static MetadataRecord CreatePlaceholder(string folderId, string folderPath, string folderName)
        => new(
            folderId,
            folderPath,
            folderName,
            string.Empty,
            MetadataState.NeedsMetadata,
            MetadataFailureKind.None,
            null,
            null,
            null,
            null,
            null,
            null);
}
