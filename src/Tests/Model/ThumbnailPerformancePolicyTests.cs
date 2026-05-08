using FluentAssertions;
using AniNest.Infrastructure.Thumbnails;
using Xunit;

namespace AniNest.Tests.Model;

public class ThumbnailPerformancePolicyTests
{
    [Theory]
    [InlineData(ThumbnailPerformanceMode.Quiet, false, 1, true)]
    [InlineData(ThumbnailPerformanceMode.Quiet, true, 0, false)]
    [InlineData(ThumbnailPerformanceMode.Balanced, false, 1, true)]
    [InlineData(ThumbnailPerformanceMode.Balanced, true, 1, false)]
    [InlineData(ThumbnailPerformanceMode.Fast, false, 2, true)]
    [InlineData(ThumbnailPerformanceMode.Fast, true, 1, true)]
    public void Create_ReturnsExpectedPolicy(
        ThumbnailPerformanceMode mode,
        bool isPlayerActive,
        int expectedMaxConcurrency,
        bool expectedAllowStartNewJobs)
    {
        ThumbnailExecutionPolicy policy = ThumbnailPerformancePolicy.Create(mode, isPlayerActive);

        policy.MaxConcurrency.Should().Be(expectedMaxConcurrency);
        policy.AllowStartNewJobs.Should().Be(expectedAllowStartNewJobs);
    }
}
