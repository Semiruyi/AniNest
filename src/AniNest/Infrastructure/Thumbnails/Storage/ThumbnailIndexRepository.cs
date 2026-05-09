using System;
using System.Collections.Generic;
using System.IO;
using AniNest.Infrastructure.Logging;

namespace AniNest.Infrastructure.Thumbnails;

internal sealed class ThumbnailIndexRepository
{
    private static readonly Logger Log = AppLog.For<ThumbnailIndexRepository>();

    private readonly string _thumbBaseDir;
    private readonly string _indexPath;
    private readonly object _indexIoLock = new();

    public ThumbnailIndexRepository(string thumbBaseDir)
    {
        _thumbBaseDir = thumbBaseDir;
        _indexPath = Path.Combine(_thumbBaseDir, "index.json");
    }

    public IReadOnlyList<ThumbnailTask> Load(HashSet<string> existingPaths)
        => ThumbnailIndex.Load(_indexPath, _thumbBaseDir, existingPaths);

    public void Save(IReadOnlyCollection<ThumbnailTask> tasks)
    {
        lock (_indexIoLock)
        {
            ThumbnailIndex.Save(_indexPath, tasks);
        }
    }

    public void CleanupTempArtifacts()
    {
        try
        {
            string[] tmpDirs = Directory.GetDirectories(_thumbBaseDir, ".tmp_*");
            foreach (string dir in tmpDirs)
            {
                try { Directory.Delete(dir, recursive: true); }
                catch { }
            }

            string[] backupDirs = Directory.GetDirectories(_thumbBaseDir, "*.bak");
            foreach (string dir in backupDirs)
            {
                try { Directory.Delete(dir, recursive: true); }
                catch { }
            }

            string[] backupFiles = Directory.GetFiles(_thumbBaseDir, "*.bak", SearchOption.AllDirectories);
            foreach (string file in backupFiles)
            {
                try { File.Delete(file); }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Cleanup temp directories failed", ex);
        }
    }

    public void DeleteThumbnailDirectory(string thumbnailDir)
    {
        try
        {
            if (Directory.Exists(thumbnailDir))
                Directory.Delete(thumbnailDir, recursive: true);
        }
        catch (Exception ex)
        {
            Log.Error($"Delete thumbnail directory failed: {thumbnailDir}", ex);
        }
    }

    public void DeleteTaskDirectory(string md5Dir)
        => DeleteThumbnailDirectory(Path.Combine(_thumbBaseDir, md5Dir));
}
