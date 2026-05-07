using System;
using System.IO;
using System.Linq;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Paths;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Thumbnails;
using AniNest.Infrastructure.Interop;
namespace AniNest.Infrastructure.Thumbnails;

public class VideoScanner : IVideoScanner
{
    private static readonly string[] VideoExtensions =
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm",
        ".m4v", ".mpg", ".mpeg", ".ts", ".m2ts", ".rmvb"
    };

    private static readonly string[] CoverNames =
    {
        "folder", "cover", "poster", "front", "thumbnail", "thumb"
    };

    private static readonly string[] CoverExtensions =
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif"
    };

    private static readonly Logger Log = AppLog.For<VideoScanner>();

    public FolderScanResult ScanFolder(string folderPath)
        => ScanFolderCore(folderPath, CancellationToken.None);

    public Task<FolderScanResult> ScanFolderAsync(string folderPath, CancellationToken cancellationToken = default)
        => Task.Run(() => ScanFolderCore(folderPath, cancellationToken), cancellationToken);

    public int CountVideosInFolder(string folderPath)
        => CountVideosInFolderCore(folderPath, CancellationToken.None);

    public Task<int> CountVideosInFolderAsync(string folderPath, CancellationToken cancellationToken = default)
        => Task.Run(() => CountVideosInFolderCore(folderPath, cancellationToken), cancellationToken);

    public string[] GetVideoFiles(string folderPath)
        => GetVideoFilesCore(folderPath, CancellationToken.None);

    public Task<string[]> GetVideoFilesAsync(string folderPath, CancellationToken cancellationToken = default)
        => Task.Run(() => GetVideoFilesCore(folderPath, cancellationToken), cancellationToken);

    public string? FindCoverImage(string folderPath)
        => FindCoverImageCore(folderPath, CancellationToken.None);

    public Task<string?> FindCoverImageAsync(string folderPath, CancellationToken cancellationToken = default)
        => Task.Run(() => FindCoverImageCore(folderPath, cancellationToken), cancellationToken);

    public List<string> FindVideoFolders(string rootPath)
        => FindVideoFoldersCore(rootPath, CancellationToken.None);

    public Task<List<string>> FindVideoFoldersAsync(string rootPath, CancellationToken cancellationToken = default)
        => Task.Run(() => FindVideoFoldersCore(rootPath, cancellationToken), cancellationToken);

    private FolderScanResult ScanFolderCore(string folderPath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folderPath))
            return new FolderScanResult(0, null, Array.Empty<string>());

        try
        {
            var files = Directory.GetFiles(folderPath);
            var videoFiles = new System.Collections.Generic.List<string>();
            string? coverPath = null;

            string specificCover = Path.Combine(folderPath, "cover.jpg");
            if (File.Exists(specificCover))
                coverPath = specificCover;

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string ext = Path.GetExtension(file).ToLower();

                if (IsVideoFileExt(ext))
                    videoFiles.Add(file);

                if (coverPath == null && IsCoverExt(ext))
                {
                    string fileName = Path.GetFileNameWithoutExtension(file).ToLower();
                    if (CoverNames.Contains(fileName))
                        coverPath = file;
                }
            }

            if (coverPath == null)
            {
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (IsCoverExt(Path.GetExtension(file).ToLower()))
                    {
                        coverPath = file;
                        break;
                    }
                }
            }

            var ordered = videoFiles.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
            return new FolderScanResult(ordered.Length, coverPath, ordered);
        }
        catch (Exception ex)
        {
            Log.Error($"ScanFolder failed: {folderPath}", ex);
            return new FolderScanResult(0, null, Array.Empty<string>());
        }
    }

    private int CountVideosInFolderCore(string folderPath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folderPath))
            return 0;

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var files = Directory.GetFiles(folderPath);
            Log.Info($"Directory.GetFiles({Path.GetFileName(folderPath)}) took {sw.ElapsedMilliseconds}ms, {files.Length} files");
            int count = 0;
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsVideoFile(file))
                    count++;
            }
            return count;
        }
        catch (Exception ex)
        {
            Log.Error($"CountVideosInFolder failed: {folderPath}", ex);
            return 0;
        }
    }

    private string[] GetVideoFilesCore(string folderPath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folderPath))
            return Array.Empty<string>();

        try
        {
            var videoFiles = new List<string>();
            foreach (var file in Directory.GetFiles(folderPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsVideoFile(file))
                    videoFiles.Add(file);
            }

            return videoFiles
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            Log.Error($"GetVideoFiles failed: {folderPath}", ex);
            return Array.Empty<string>();
        }
    }

    private string? FindCoverImageCore(string folderPath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folderPath))
            return null;

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var files = Directory.GetFiles(folderPath);
            Log.Info($"FindCoverImage.Directory.GetFiles({Path.GetFileName(folderPath)}) took {sw.ElapsedMilliseconds}ms, {files.Length} files");

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string fileName = Path.GetFileNameWithoutExtension(file).ToLower();
                string ext = Path.GetExtension(file).ToLower();

                if (IsCoverExt(ext) && CoverNames.Contains(fileName))
                {
                    return file;
                }
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string ext = Path.GetExtension(file).ToLower();
                if (IsCoverExt(ext))
                {
                    return file;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"FindCoverImage failed: {folderPath}", ex);
        }

        return null;
    }

    private static bool IsVideoFile(string filePath)
    {
        return IsVideoFileExt(Path.GetExtension(filePath).ToLower());
    }

    private static bool IsVideoFileExt(string ext)
    {
        return VideoExtensions.Contains(ext);
    }

    private List<string> FindVideoFoldersCore(string rootPath, CancellationToken cancellationToken)
    {
        var result = new List<string>();
        if (!Directory.Exists(rootPath))
        {
            Log.Error($"FindVideoFolders: rootPath does not exist: {rootPath}");
            return result;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Log.Info($"FindVideoFolders start: {rootPath}");

        cancellationToken.ThrowIfCancellationRequested();

        if (CountVideosInFolderCore(rootPath, cancellationToken) > 0)
            result.Add(rootPath);

        FindVideoFoldersRecursive(rootPath, result, cancellationToken);

        Log.Info($"FindVideoFolders done: found {result.Count} folders, elapsed {sw.ElapsedMilliseconds}ms");
        return result;
    }

    private void FindVideoFoldersRecursive(string folderPath, List<string> result, CancellationToken cancellationToken)
    {
        string[] subDirs;
        try
        {
            subDirs = Directory.GetDirectories(folderPath);
        }
        catch (Exception ex)
        {
            Log.Error($"FindVideoFoldersRecursive: failed to enumerate subdirectories of {folderPath}", ex);
            return;
        }

        foreach (var subDir in subDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (CountVideosInFolderCore(subDir, cancellationToken) > 0)
                result.Add(subDir);
            else
                FindVideoFoldersRecursive(subDir, result, cancellationToken);
        }
    }

    private static bool IsCoverExt(string ext)
    {
        return CoverExtensions.Contains(ext);
    }
}

public sealed record FolderScanResult(
    int VideoCount,
    string? CoverPath,
    string[] VideoFiles);



