using System;
using System.Diagnostics;

namespace LocalPlayer.Infrastructure.Diagnostics;

public static class MemorySnapshot
{
    public static string Capture(string stage, params (string Key, object? Value)[] details)
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();

        long managed = GC.GetTotalMemory(forceFullCollection: false);
        var gcInfo = GC.GetGCMemoryInfo();

        var message = $"stage={stage}" +
                      $" managed={FormatBytes(managed)}" +
                      $" heap={FormatBytes(gcInfo.HeapSizeBytes)}" +
                      $" fragmented={FormatBytes(gcInfo.FragmentedBytes)}" +
                      $" workingSet={FormatBytes(process.WorkingSet64)}" +
                      $" private={FormatBytes(process.PrivateMemorySize64)}" +
                      $" paged={FormatBytes(process.PagedMemorySize64)}" +
                      $" handles={process.HandleCount}" +
                      $" gc0={GC.CollectionCount(0)}" +
                      $" gc1={GC.CollectionCount(1)}" +
                      $" gc2={GC.CollectionCount(2)}";

        foreach (var (key, value) in details)
        {
            message += $" {key}={FormatValue(value)}";
        }

        return message;
    }

    private static string FormatBytes(long bytes)
    {
        const double scale = 1024d * 1024d;
        return $"{bytes / scale:F1}MB";
    }

    private static string FormatValue(object? value)
    {
        if (value is null)
            return "null";

        return value switch
        {
            bool boolValue => boolValue ? "true" : "false",
            string stringValue when string.IsNullOrWhiteSpace(stringValue) => "\"\"",
            _ => value.ToString() ?? "null"
        };
    }
}
