using System.IO;
using System.Text.Json;
using AniNest.Infrastructure.Paths;
namespace AniNest.Features.Metadata;

public sealed class MetadataRepository : IMetadataRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly object _ioLock = new();

    public FolderMetadata? Get(string folderPath)
    {
        var path = GetMetadataFilePath(folderPath);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<FolderMetadata>(json);
        }
        catch
        {
            return null;
        }
    }

    public void Save(FolderMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var path = GetMetadataFilePath(metadata.FolderPath);
        var tempPath = $"{path}.tmp";
        var json = JsonSerializer.Serialize(metadata, JsonOptions);

        lock (_ioLock)
        {
            Directory.CreateDirectory(AppPaths.MetadataDirectory);
            File.WriteAllText(tempPath, json);

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
    }

    public void Delete(string folderPath)
    {
        var path = GetMetadataFilePath(folderPath);
        lock (_ioLock)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static string GetMetadataFilePath(string folderPath)
        => MetadataStoragePaths.GetMetadataFilePath(folderPath);
}
