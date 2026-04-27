using System.Collections.Generic;

namespace LocalPlayer.Models;

public class AppSettings
{
    public List<FolderInfo> Folders { get; set; } = new();
    public Dictionary<string, VideoProgress> VideoProgress { get; set; } = new();
    public Dictionary<string, FolderProgress> FolderProgress { get; set; } = new();
}

public class FolderInfo
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public long AddedTime { get; set; }  // Unix 时间戳
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
