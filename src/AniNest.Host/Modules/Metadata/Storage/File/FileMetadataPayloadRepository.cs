using System.Text.Json;
using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal sealed class FileMetadataPayloadRepository : IMetadataPayloadRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _rootPath;

    public FileMetadataPayloadRepository(string rootPath)
    {
        _rootPath = rootPath;
    }

    public FolderMetadataPayload? Load(string relativePath)
    {
        var fullPath = ResolvePath(relativePath);
        if (!File.Exists(fullPath))
            return null;

        using var stream = File.OpenRead(fullPath);
        return JsonSerializer.Deserialize<FolderMetadataPayload>(stream, SerializerOptions);
    }

    public void Save(string relativePath, FolderMetadataPayload payload)
    {
        var fullPath = ResolvePath(relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var tempPath = fullPath + ".tmp";
        using (var stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, payload, SerializerOptions);
        }

        File.Move(tempPath, fullPath, true);
    }

    public void Delete(string relativePath)
    {
        var fullPath = ResolvePath(relativePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    private string ResolvePath(string relativePath)
        => Path.Combine(_rootPath, relativePath);
}
