using System.Collections.Generic;
using AniNest.Features.Player.Input;
using AniNest.Infrastructure.Thumbnails;
namespace AniNest.Infrastructure.Persistence;

public class AppSettings
{
    public List<FolderInfo> Folders { get; set; } = new();
    public Dictionary<string, VideoProgress> VideoProgress { get; set; } = new();
    public Dictionary<string, FolderProgress> FolderProgress { get; set; } = new();
    public int ThumbnailExpiryDays { get; set; } = 30;
    public ThumbnailPerformanceMode ThumbnailPerformanceMode { get; set; } = ThumbnailPerformanceMode.Balanced;
    public string Language { get; set; } = "zh-CN";
    public string FullscreenAnimation { get; set; } = "none";
    public PlayerInputProfile PlayerInput { get; set; } = new();
    public WindowGeometry Window { get; set; } = new();
}

public class WindowGeometry
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Maximized { get; set; }
}

public class FolderInfo
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public long AddedTime { get; set; }
    public int OrderIndex { get; set; }
}

public class VideoProgress
{
    public string FilePath { get; set; } = "";
    public long Position { get; set; }
    public long Duration { get; set; }
    public bool IsPlayed { get; set; }
    public long LastPlayed { get; set; }
}

public class FolderProgress
{
    public string FolderPath { get; set; } = "";
    public string LastVideoPath { get; set; } = "";
    public long LastPlayed { get; set; }
}



