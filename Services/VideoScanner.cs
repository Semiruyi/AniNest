using System;
using System.IO;
using System.Linq;

namespace LocalPlayer.Services;

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

    public static int CountVideosInFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return 0;

        try
        {
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

    public static string? FindCoverImage(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return null;

        try
        {
            var files = Directory.GetFiles(folderPath);
            
            // 1. 先找常见命名
            foreach (var file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file).ToLower();
                string ext = Path.GetExtension(file).ToLower();
                
                if (CoverExtensions.Contains(ext) && CoverNames.Contains(fileName))
                {
                    return file;
                }
            }
            
            // 2. 找任意图片作为备选
            foreach (var file in files)
            {
                string ext = Path.GetExtension(file).ToLower();
                if (CoverExtensions.Contains(ext))
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
        string ext = Path.GetExtension(filePath).ToLower();
        return VideoExtensions.Contains(ext);
    }
}