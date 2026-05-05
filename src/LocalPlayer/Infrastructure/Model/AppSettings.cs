using System.Collections.Generic;

namespace LocalPlayer.Infrastructure.Model;

public class AppSettings
{
    public List<FolderInfo> Folders { get; set; } = new();
    public Dictionary<string, VideoProgress> VideoProgress { get; set; } = new();
    public Dictionary<string, FolderProgress> FolderProgress { get; set; } = new();
    public int ThumbnailExpiryDays { get; set; } = 30; // 0 = 姘镐笉杩囨湡
    public Dictionary<string, int> KeyBindings { get; set; } = new();
    public string Language { get; set; } = "zh-CN";
    public string FullscreenAnimation { get; set; } = "none";
}

public class KeyBindingInfo
{
    public string ActionName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int DefaultKey { get; set; }
}

public class FolderInfo
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public long AddedTime { get; set; }  // Unix 鏃堕棿鎴?
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

