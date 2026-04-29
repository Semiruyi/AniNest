using System;
using System.IO;

namespace LocalPlayer.Services;

public static class AppLog
{
    private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly object Lock = new();

    public static void Write(string fileName, string category, string message)
    {
        try
        {
            string path = Path.Combine(BaseDir, fileName);
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] [{category}] {message}{Environment.NewLine}";
            lock (Lock)
            {
                File.AppendAllText(path, line);
            }
        }
        catch { }
    }
}
