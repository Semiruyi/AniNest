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
}
