using System.IO;
using System.Text.Json;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Paths;

namespace AniNest.Features.Metadata;

public sealed class MetadataIndexStore
{
    private static readonly Logger Log = AppLog.For<MetadataIndexStore>();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _indexPath;
    private readonly object _ioLock = new();

    public MetadataIndexStore(string? indexPath = null)
    {
        _indexPath = indexPath ?? Path.Combine(AppPaths.MetadataDirectory, "index.json");
    }

    public Dictionary<string, MetadataRecord> Load()
    {
        if (!File.Exists(_indexPath))
            return new Dictionary<string, MetadataRecord>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = File.ReadAllText(_indexPath);
            return JsonSerializer.Deserialize<Dictionary<string, MetadataRecord>>(json)
                ?? new Dictionary<string, MetadataRecord>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, MetadataRecord>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void Save(IReadOnlyDictionary<string, MetadataRecord> records)
    {
        var directory = Path.GetDirectoryName(_indexPath) ?? AppPaths.MetadataDirectory;
        var tempPath = $"{_indexPath}.tmp";
        var backupPath = $"{_indexPath}.bak";
        var normalized = new Dictionary<string, MetadataRecord>(records, StringComparer.OrdinalIgnoreCase);
        var json = JsonSerializer.Serialize(normalized, JsonOptions);

        lock (_ioLock)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(tempPath, json);
            PromoteIndexFile(tempPath, _indexPath, backupPath);
        }

        Log.Info($"Metadata index saved: path={_indexPath}, records={normalized.Count}");
    }

    private static void PromoteIndexFile(string stagedPath, string finalPath, string backupPath)
    {
        if (!File.Exists(finalPath))
        {
            File.Move(stagedPath, finalPath);
            return;
        }

        if (File.Exists(backupPath))
            File.Delete(backupPath);

        File.Move(finalPath, backupPath);

        try
        {
            File.Move(stagedPath, finalPath);
            File.Delete(backupPath);
        }
        catch
        {
            if (!File.Exists(finalPath) && File.Exists(backupPath))
                File.Move(backupPath, finalPath);

            throw;
        }
    }
}
