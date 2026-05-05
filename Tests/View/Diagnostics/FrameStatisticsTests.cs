using LocalPlayer.View.Diagnostics;

namespace LocalPlayer.Tests.View.Diagnostics;

public class FrameStatisticsTests
{
    [Fact]
    public void FromSamples_ComputesExpectedFrameMetrics()
    {
        double[] samples = [5, 6, 7, 8, 10, 12, 20, 40];

        var stats = FrameStatistics.FromSamples(samples, droppedSamples: 3);

        stats.FrameCount.Should().Be(8);
        stats.AverageFrameTimeMs.Should().BeApproximately(13.5, 0.001);
        stats.AverageFps.Should().BeApproximately(74.074, 0.01);
        stats.MinFrameTimeMs.Should().Be(5);
        stats.MedianFrameTimeMs.Should().BeApproximately(9, 0.001);
        stats.P95FrameTimeMs.Should().BeApproximately(33, 0.001);
        stats.P99FrameTimeMs.Should().BeApproximately(38.6, 0.001);
        stats.MaxFrameTimeMs.Should().Be(40);
        stats.OnePercentLowFps.Should().Be(25);
        stats.JankOver6_25MsCount.Should().Be(6);
        stats.JankOver8_33MsCount.Should().Be(4);
        stats.JankOver16_67MsCount.Should().Be(2);
        stats.JankOver33_33MsCount.Should().Be(1);
        stats.DroppedSamples.Should().Be(3);
    }

    [Fact]
    public void FromSamples_WhenEmpty_ReturnsEmptyStatistics()
    {
        var stats = FrameStatistics.FromSamples([], droppedSamples: 2);

        stats.FrameCount.Should().Be(0);
        stats.AverageFrameTimeMs.Should().Be(0);
        stats.AverageFps.Should().Be(0);
        stats.DroppedSamples.Should().Be(2);
    }
}
