using AniNest.Infrastructure.Diagnostics;

namespace AniNest.Tests.View.Diagnostics;

public class PerfSceneSessionTests
{
    [Fact]
    public void Begin_WhenPerfLoggingDisabled_StopAndDisposeDoNotThrow()
    {
        bool originalEnabled = PerfLogger.Enabled;
        string originalPath = PerfLogger.LogPath;
        PerfLogger.Enabled = false;
        PerfLogger.LogPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.perf.log");

        try
        {
            var session = PerfScenes.Begin("Library.InitialLoad");

            var report = session.Stop();
            session.Dispose();

            report.SceneName.Should().BeEmpty();
            report.DurationMs.Should().Be(0);
            report.Statistics.FrameCount.Should().Be(0);
        }
        finally
        {
            PerfLogger.Enabled = originalEnabled;
            PerfLogger.LogPath = originalPath;
        }
    }
}
