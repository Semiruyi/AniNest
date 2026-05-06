using System;
using System.IO;
using System.Linq;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Paths;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Thumbnails;
using LocalPlayer.Infrastructure.Interop;
namespace LocalPlayer.Infrastructure.Thumbnails;

public class VideoScanner
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

    public static FolderScanResult ScanFolder(string folderPath)
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

    public static int CountVideosInFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return 0;

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var files = Directory.GetFiles(folderPath);
            Log.Info($"Directory.GetFiles({Path.GetFileName(folderPath)}) took {sw.ElapsedMilliseconds}ms, {files.Length} files");
            int count = files.Count(f => IsVideoFile(f));
            return count;
        }
        catch (Exception ex)
        {
            Log.Error($"CountVideosInFolder failed: {folderPath}", ex);
            return 0;
        }
    }

    public static string[] GetVideoFiles(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return Array.Empty<string>();

        try
        {
            return Directory.GetFiles(folderPath)
                .Where(f => IsVideoFile(f))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            Log.Error($"GetVideoFiles failed: {folderPath}", ex);
            return Array.Empty<string>();
        }
    }

    public static string? FindCoverImage(string folderPath)
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
                string fileName = Path.GetFileNameWithoutExtension(file).ToLower();
                string ext = Path.GetExtension(file).ToLower();

                if (IsCoverExt(ext) && CoverNames.Contains(fileName))
                {
                    return file;
                }
            }

            foreach (var file in files)
            {
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

    public static List<string> FindVideoFolders(string rootPath)
    {
        var result = new List<string>();
        if (!Directory.Exists(rootPath))
        {
            Log.Error($"FindVideoFolders: rootPath does not exist: {rootPath}");
            return result;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Log.Info($"FindVideoFolders start: {rootPath}");

        if (CountVideosInFolder(rootPath) > 0)
            result.Add(rootPath);

        FindVideoFoldersRecursive(rootPath, result);

        Log.Info($"FindVideoFolders done: found {result.Count} folders, elapsed {sw.ElapsedMilliseconds}ms");
        return result;
    }

    private static void FindVideoFoldersRecursive(string folderPath, List<string> result)
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
            if (CountVideosInFolder(subDir) > 0)
                result.Add(subDir);
            else
                FindVideoFoldersRecursive(subDir, result);
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



