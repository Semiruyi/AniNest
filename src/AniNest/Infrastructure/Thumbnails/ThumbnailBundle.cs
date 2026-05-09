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

    public static BundleWriter CreateWriter(string targetDirectory)
        => new(targetDirectory);

    public static void Write(string sourceDirectory, string targetDirectory, IReadOnlyList<long> framePositionsMs)
    {
        string[] frameFiles = Directory.GetFiles(sourceDirectory, "*.jpg")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (frameFiles.Length == 0)
            return;

        if (framePositionsMs.Count != frameFiles.Length)
            throw new InvalidOperationException("Frame position count must match frame file count.");

        using var bundleWriter = CreateWriter(targetDirectory);
        for (int i = 0; i < frameFiles.Length; i++)
        {
            bundleWriter.AppendFrame(framePositionsMs[i], File.ReadAllBytes(frameFiles[i]));
        }
        bundleWriter.Commit();
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

    internal sealed class BundleWriter : IDisposable
    {
        private readonly string _bundlePath;
        private readonly string _tempPath;
        private readonly string _payloadTempPath;
        private readonly FileStream _payloadStream;
        private readonly List<FrameEntry> _entries = [];
        private bool _committed;
        private bool _disposed;

        public BundleWriter(string targetDirectory)
        {
            Directory.CreateDirectory(targetDirectory);
            _bundlePath = GetBundlePath(targetDirectory);
            _tempPath = _bundlePath + ".tmp";
            _payloadTempPath = _tempPath + ".payload";
            _payloadStream = new FileStream(_payloadTempPath, FileMode.Create, FileAccess.Write, FileShare.None);
        }

        public void AppendFrame(long positionMs, ReadOnlySpan<byte> bytes)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_committed)
                throw new InvalidOperationException("Cannot append frames after commit.");

            if (bytes.Length <= 0)
                throw new InvalidOperationException("Frame payload must not be empty.");

            long offset = _payloadStream.Position;
            _payloadStream.Write(bytes);
            _entries.Add(new FrameEntry(positionMs, offset, bytes.Length));
        }

        public void UpdateFramePosition(int frameIndex, long positionMs)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_committed)
                throw new InvalidOperationException("Cannot update frame positions after commit.");

            if (frameIndex < 0 || frameIndex >= _entries.Count)
                throw new ArgumentOutOfRangeException(nameof(frameIndex));

            FrameEntry entry = _entries[frameIndex];
            _entries[frameIndex] = entry with { PositionMs = positionMs };
        }

        public int FrameCount => _entries.Count;

        public void Commit()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_committed)
                return;

            _payloadStream.Flush(flushToDisk: true);
            _payloadStream.Dispose();

            long tableLength = _entries.Count * (sizeof(long) + sizeof(long) + sizeof(int));
            long payloadStartOffset = Magic.Length + sizeof(int) + sizeof(int) + tableLength;

            using (var bundleStream = new FileStream(_tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(bundleStream, Encoding.ASCII, leaveOpen: false))
            {
                writer.Write(Encoding.ASCII.GetBytes(Magic));
                writer.Write(Version);
                writer.Write(_entries.Count);

                foreach (FrameEntry entry in _entries)
                {
                    writer.Write(entry.PositionMs);
                    writer.Write(payloadStartOffset + entry.Offset);
                    writer.Write(entry.Length);
                }

                using var payloadStream = new FileStream(_payloadTempPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                payloadStream.CopyTo(bundleStream);
                writer.Flush();
                bundleStream.Flush(flushToDisk: true);
            }

            if (File.Exists(_bundlePath))
                File.Delete(_bundlePath);

            File.Move(_tempPath, _bundlePath);
            File.Delete(_payloadTempPath);
            _committed = true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (!_committed)
            {
                _payloadStream.Dispose();
                try
                {
                    if (File.Exists(_tempPath))
                        File.Delete(_tempPath);
                    if (File.Exists(_payloadTempPath))
                        File.Delete(_payloadTempPath);
                }
                catch
                {
                }
            }

            _disposed = true;
        }
    }
}
