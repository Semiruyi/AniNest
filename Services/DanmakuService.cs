using System.Text.Json;
using LocalPlayer.Models;

namespace LocalPlayer.Services;

public class DanmakuService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string dataDir;

    public DanmakuService()
    {
        dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Danmaku");
        Directory.CreateDirectory(dataDir);
    }

    public List<DanmakuItem> Load(string videoFilePath)
    {
        string path = GetDanmakuPath(videoFilePath);
        if (!File.Exists(path)) return new List<DanmakuItem>();
        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<DanmakuItem>>(json, JsonOptions) ?? new();
        }
        catch
        {
            return new List<DanmakuItem>();
        }
    }

    public void Save(string videoFilePath, List<DanmakuItem> danmakuList)
    {
        string path = GetDanmakuPath(videoFilePath);
        string json = JsonSerializer.Serialize(danmakuList, JsonOptions);
        File.WriteAllText(path, json);
    }

    public void Add(string videoFilePath, DanmakuItem item)
    {
        var list = Load(videoFilePath);
        list.Add(item);
        list.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));
        Save(videoFilePath, list);
    }

    private string GetDanmakuPath(string videoFilePath)
    {
        string hash = Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(videoFilePath)))
            .Replace('/', '_').Replace('+', '-').Replace("=", "");
        return Path.Combine(dataDir, $"{hash}.json");
    }
}
