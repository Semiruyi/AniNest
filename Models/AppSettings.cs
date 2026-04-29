using System.Collections.Generic;
using System.Windows.Input;

namespace LocalPlayer.Models;

public class AppSettings
{
    public List<FolderInfo> Folders { get; set; } = new();
    public Dictionary<string, VideoProgress> VideoProgress { get; set; } = new();
    public Dictionary<string, FolderProgress> FolderProgress { get; set; } = new();
    public int ThumbnailExpiryDays { get; set; } = 30; // 0 = 永不过期
    public Dictionary<string, Key> KeyBindings { get; set; } = new();
}

public class KeyBindingInfo
{
    public string ActionName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public Key DefaultKey { get; set; }
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
