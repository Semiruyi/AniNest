using System.Collections.Generic;

namespace LocalPlayer.Models;

public class AppSettings
{
    public List<FolderInfo> Folders { get; set; } = new();
}

public class FolderInfo
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public long AddedTime { get; set; }  // Unix 时间戳
}