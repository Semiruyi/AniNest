using AniNest.Infrastructure.Diagnostics;

namespace AniNest.Tests.View.Diagnostics;

public class FrameSampleBufferTests
{
    [Fact]
    public void SnapshotOrdered_WhenBelowCapacity_PreservesInsertionOrder()
    {
        var buffer = new FrameSampleBuffer(4);

        buffer.Add(10);
        buffer.Add(11);
        buffer.Add(12);

        buffer.SnapshotOrdered().Should().Equal(10, 11, 12);
        buffer.DroppedSamples.Should().Be(0);
    }

    [Fact]
    public void SnapshotOrdered_WhenOverCapacity_ReturnsLatestSamplesInOrder()
    {
        var buffer = new FrameSampleBuffer(3);

        buffer.Add(10);
        buffer.Add(11);
        buffer.Add(12);
        buffer.Add(13);
        buffer.Add(14);

        buffer.SnapshotOrdered().Should().Equal(12, 13, 14);
        buffer.DroppedSamples.Should().Be(2);
    }
}
