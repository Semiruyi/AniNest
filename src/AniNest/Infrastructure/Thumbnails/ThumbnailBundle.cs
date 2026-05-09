using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AniNest.Infrastructure.Thumbnails;

internal static class ThumbnailBundle
{
    internal readonly record struct FrameEntry(long PositionMs, long Offset, int Length);

    private const string FileName = "bundle.bin";
    private const string Magic = "ANITHMB1";
    private const int Version = 2;

    public static string GetBundlePath(string thumbnailDirectory)
        => Path.Combine(thumbnailDirectory, FileName);

    public static bool Exists(string thumbnailDirectory)
        => File.Exists(GetBundlePath(thumbnailDirectory));

    public static void Write(string sourceDirectory, string targetDirectory, IReadOnlyList<long> framePositionsMs)
    {
        string[] frameFiles = Directory.GetFiles(sourceDirectory, "*.jpg")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (frameFiles.Length == 0)
            return;

        if (framePositionsMs.Count != frameFiles.Length)
            throw new InvalidOperationException("Frame position count must match frame file count.");

        Directory.CreateDirectory(targetDirectory);
        string bundlePath = GetBundlePath(targetDirectory);
        string tempPath = bundlePath + ".tmp";

        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false))
        {
            writer.Write(Encoding.ASCII.GetBytes(Magic));
            writer.Write(Version);
            writer.Write(frameFiles.Length);

            long tableStart = stream.Position;
            long tableLength = frameFiles.Length * (sizeof(long) + sizeof(long) + sizeof(int));
            stream.Position += tableLength;

            var offsets = new long[frameFiles.Length];
            var lengths = new int[frameFiles.Length];

            for (int i = 0; i < frameFiles.Length; i++)
            {
                byte[] bytes = File.ReadAllBytes(frameFiles[i]);
                offsets[i] = stream.Position;
                lengths[i] = bytes.Length;
                writer.Write(bytes);
            }

            stream.Position = tableStart;
            for (int i = 0; i < frameFiles.Length; i++)
            {
                writer.Write(framePositionsMs[i]);
                writer.Write(offsets[i]);
                writer.Write(lengths[i]);
            }
        }

        if (File.Exists(bundlePath))
            File.Delete(bundlePath);

        File.Move(tempPath, bundlePath);
    }

    public static IReadOnlyList<long>? ReadFramePositions(string thumbnailDirectory)
    {
        FrameEntry[]? entries = ReadFrameEntries(thumbnailDirectory);
        return entries?.Select(static entry => entry.PositionMs).ToArray();
    }

    public static int GetFrameCount(string thumbnailDirectory)
    {
        FrameEntry[]? entries = ReadFrameEntries(thumbnailDirectory);
        return entries?.Length ?? 0;
    }

    public static byte[]? ReadFrameBytes(string thumbnailDirectory, int frameIndex)
    {
        if (frameIndex < 0)
            return null;

        FrameEntry[]? entries = ReadFrameEntries(thumbnailDirectory);
        if (entries == null || frameIndex >= entries.Length)
            return null;

        string bundlePath = GetBundlePath(thumbnailDirectory);
        using var stream = new FileStream(bundlePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);
        FrameEntry entry = entries[frameIndex];
        if (entry.Length <= 0)
            return null;

        stream.Position = entry.Offset;
        return reader.ReadBytes(entry.Length);
    }

    private static FrameEntry[]? ReadFrameEntries(string thumbnailDirectory)
    {
        string bundlePath = GetBundlePath(thumbnailDirectory);
        if (!File.Exists(bundlePath))
            return null;

        using var stream = new FileStream(bundlePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);

        string magic = Encoding.ASCII.GetString(reader.ReadBytes(Magic.Length));
        if (!string.Equals(magic, Magic, StringComparison.Ordinal))
            return null;

        int version = reader.ReadInt32();
        int frameCount = reader.ReadInt32();
        if (frameCount < 0)
            return null;

        var entries = new FrameEntry[frameCount];
        if (version == 1)
        {
            for (int i = 0; i < frameCount; i++)
            {
                long offset = reader.ReadInt64();
                int length = reader.ReadInt32();
                entries[i] = new FrameEntry(i * 1000L, offset, length);
            }

            return entries;
        }

        if (version != Version)
            return null;

        for (int i = 0; i < frameCount; i++)
        {
            long positionMs = reader.ReadInt64();
            long offset = reader.ReadInt64();
            int length = reader.ReadInt32();
            entries[i] = new FrameEntry(positionMs, offset, length);
        }

        return entries;
    }
}
