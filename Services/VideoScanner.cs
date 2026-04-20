using System;
using System.IO;
using System.Linq;

namespace LocalPlayer.Services;

public class VideoScanner
{
    // 支持的视频格式
    private static readonly string[] VideoExtensions = 
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", 
        ".m4v", ".mpg", ".mpeg", ".ts", ".m2ts", ".rmvb"
    };

    public static int CountVideosInFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return 0;

        try
        {
            // 只扫描第一级文件
            var files = Directory.GetFiles(folderPath);
            return files.Count(f => IsVideoFile(f));
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

    private static bool IsVideoFile(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLower();
        return VideoExtensions.Contains(ext);
    }
}