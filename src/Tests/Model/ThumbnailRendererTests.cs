using FluentAssertions;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Tests.Model;

public class ThumbnailRendererTests
{
    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(300, 0.5)]
    [InlineData(600, 0.5)]
    [InlineData(1200, 1.0)]
    [InlineData(1421, 1421.0 / 1200.0)]
    [InlineData(2400, 2.0)]
    [InlineData(3600, 3.0)]
    [InlineData(7200, 5.0)]
    public void CalculateSamplingIntervalSeconds_UsesDurationPrecisionFormula(double durationSeconds, double expectedInterval)
    {
        double interval = ThumbnailRenderer.CalculateSamplingIntervalSeconds(durationSeconds);

        interval.Should().BeApproximately(expectedInterval, 0.000001);
    }

    [Theory]
    [InlineData(600, "2")]
    [InlineData(1200, "1")]
    [InlineData(2400, "0.5")]
    [InlineData(3600, "0.333333")]
    [InlineData(1421, "0.844476")]
    public void BuildSamplingFpsExpression_FormatsInvariantFps(double durationSeconds, string expectedFps)
    {
        string fps = ThumbnailRenderer.BuildSamplingFpsExpression(durationSeconds);

        fps.Should().Be(expectedFps);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1200, 0, 0)]
    [InlineData(1200, 1, 1000)]
    [InlineData(1421, 1, 1184)]
    [InlineData(2400, 2, 4000)]
    public void GetSampledFramePositionMs_UsesSamplingInterval(double durationSeconds, int frameIndex, long expectedMs)
    {
        long positionMs = ThumbnailRenderer.GetSampledFramePositionMs(durationSeconds, frameIndex);

        positionMs.Should().Be(expectedMs);
    }

    [Fact]
    public void TryExtractJpegFrame_ExtractsFrameAndAdvancesCursor()
    {
        byte[] frame1 = [0xFF, 0xD8, 0x01, 0x02, 0xFF, 0xD9];
        byte[] frame2 = [0xFF, 0xD8, 0x03, 0x04, 0x05, 0xFF, 0xD9];
        byte[] buffer = frame1.Concat(frame2).ToArray();
        int searchStart = 0;

        var found1 = ThumbnailRenderer.TryExtractJpegFrame(buffer, buffer.Length, ref searchStart, out byte[]? extracted1);
        var found2 = ThumbnailRenderer.TryExtractJpegFrame(buffer, buffer.Length, ref searchStart, out byte[]? extracted2);

        found1.Should().BeTrue();
        extracted1.Should().Equal(frame1);
        found2.Should().BeTrue();
        extracted2.Should().Equal(frame2);
    }

    [Fact]
    public async Task ReadJpegFramesAsync_ParsesMultipleFramesAcrossChunkBoundaries()
    {
        byte[] frame1 = [0xFF, 0xD8, 0x11, 0x22, 0xFF, 0xD9];
        byte[] frame2 = [0xFF, 0xD8, 0x33, 0x44, 0x55, 0xFF, 0xD9];
        byte[] payload = frame1.Concat(frame2).ToArray();
        using var stream = new ChunkedReadStream(payload, 3);
        var frames = new List<byte[]>();

        await ThumbnailRenderer.ReadJpegFramesAsync(stream, frame => frames.Add(frame), CancellationToken.None);

        frames.Should().HaveCount(2);
        frames[0].Should().Equal(frame1);
        frames[1].Should().Equal(frame2);
    }

    private sealed class ChunkedReadStream(byte[] data, int chunkSize) : Stream
    {
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => data.Length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int remaining = data.Length - _position;
            if (remaining <= 0)
                return 0;

            int toRead = Math.Min(Math.Min(count, chunkSize), remaining);
            Buffer.BlockCopy(data, _position, buffer, offset, toRead);
            _position += toRead;
            return toRead;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int remaining = data.Length - _position;
            if (remaining <= 0)
                return ValueTask.FromResult(0);

            int toRead = Math.Min(Math.Min(buffer.Length, chunkSize), remaining);
            data.AsMemory(_position, toRead).CopyTo(buffer);
            _position += toRead;
            return ValueTask.FromResult(toRead);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
