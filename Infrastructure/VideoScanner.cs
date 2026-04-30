using System;
using System.IO;
using System.Linq;

namespace LocalPlayer.Infrastructure;

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

    private static void Log(string message) => AppLog.Info(nameof(VideoScanner), message);

    /// <summary>
    /// 一次 Directory.GetFiles 同时返回视频数量和封面路径，避免重复 IO
    /// </summary>
    public static (int VideoCount, string? CoverPath) ScanFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return (0, null);

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var files = Directory.GetFiles(folderPath);
            Log($"ScanFolder({Path.GetFileName(folderPath)}) GetFiles 耗时 {sw.ElapsedMilliseconds}ms，共 {files.Length} 个文件");

            int videoCount = 0;
            string? coverPath = null;

            string specificCover = Path.Combine(folderPath, "cover.jpg");
            if (File.Exists(specificCover))
                coverPath = specificCover;

            foreach (var file in files)
            {
                string ext = Path.GetExtension(file).ToLower();

                if (IsVideoFileExt(ext))
                    videoCount++;

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

            return (videoCount, coverPath);
        }
        catch
        {
            return (0, null);
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
            Log($"Directory.GetFiles({Path.GetFileName(folderPath)}) 耗时 {sw.ElapsedMilliseconds}ms，共 {files.Length} 个文件");
            int count = files.Count(f => IsVideoFile(f));
            return count;
        }
        catch
        {
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
        catch
        {
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
            Log($"FindCoverImage.Directory.GetFiles({Path.GetFileName(folderPath)}) 耗时 {sw.ElapsedMilliseconds}ms，共 {files.Length} 个文件");

            // 1. 先找常见命名
            foreach (var file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file).ToLower();
                string ext = Path.GetExtension(file).ToLower();

                if (IsCoverExt(ext) && CoverNames.Contains(fileName))
                {
                    return file;
                }
            }

            // 2. 找任意图片作为备选
            foreach (var file in files)
            {
                string ext = Path.GetExtension(file).ToLower();
                if (IsCoverExt(ext))
                {
                    return file;
                }
            }
        }
        catch { }

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

    private static bool IsCoverExt(string ext)
    {
        return CoverExtensions.Contains(ext);
    }
}
