using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AniNest.Infrastructure.Thumbnails;

internal static class ThumbnailBundle
{
    private const string FileName = "bundle.bin";
    private const string Magic = "ANITHMB1";
    private const int Version = 1;

    public static string GetBundlePath(string thumbnailDirectory)
        => Path.Combine(thumbnailDirectory, FileName);

    public static bool Exists(string thumbnailDirectory)
        => File.Exists(GetBundlePath(thumbnailDirectory));

    public static void Write(string sourceDirectory, string targetDirectory)
    {
        string[] frameFiles = Directory.GetFiles(sourceDirectory, "*.jpg")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (frameFiles.Length == 0)
            return;

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
            long tableLength = frameFiles.Length * (sizeof(long) + sizeof(int));
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
                writer.Write(offsets[i]);
                writer.Write(lengths[i]);
            }
        }

        if (File.Exists(bundlePath))
            File.Delete(bundlePath);

        File.Move(tempPath, bundlePath);
    }

    public static byte[]? ReadFrameBytes(string thumbnailDirectory, int frameIndex)
    {
        if (frameIndex < 0)
            return null;

        string bundlePath = GetBundlePath(thumbnailDirectory);
        if (!File.Exists(bundlePath))
            return null;

        using var stream = new FileStream(bundlePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);

        string magic = Encoding.ASCII.GetString(reader.ReadBytes(Magic.Length));
        if (!string.Equals(magic, Magic, StringComparison.Ordinal))
            return null;

        int version = reader.ReadInt32();
        if (version != Version)
            return null;

        int frameCount = reader.ReadInt32();
        if (frameIndex >= frameCount)
            return null;

        long entryOffset = stream.Position + (frameIndex * (sizeof(long) + sizeof(int)));
        stream.Position = entryOffset;
        long payloadOffset = reader.ReadInt64();
        int payloadLength = reader.ReadInt32();
        if (payloadLength <= 0)
            return null;

        stream.Position = payloadOffset;
        return reader.ReadBytes(payloadLength);
    }
}
