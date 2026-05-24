using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal sealed class FileMetadataPosterCache : IMetadataPosterCache
{
    private readonly string _rootPath;

    public FileMetadataPosterCache(string rootPath)
    {
        _rootPath = rootPath;
    }

    public string Save(string fileName, Stream posterStream)
    {
        Directory.CreateDirectory(_rootPath);

        var sanitizedFileName = string.IsNullOrWhiteSpace(fileName) ? $"{Guid.NewGuid():N}.jpg" : fileName;
        var fullPath = Path.Combine(_rootPath, sanitizedFileName);
        using var fileStream = File.Create(fullPath);
        posterStream.CopyTo(fileStream);
        return sanitizedFileName;
    }

    public void Delete(string relativePath)
    {
        var fullPath = Path.Combine(_rootPath, relativePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }
}
