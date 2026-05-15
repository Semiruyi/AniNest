using AniNest.Features.Shell.Services;
using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Tests.View;

public class ShellThumbnailStatusServiceTests
{
    [Fact]
    public void GetStatusSnapshot_WhenPaused_ReturnsPausedCode()
    {
        var service = CreateService(new ThumbnailGenerationStatusSnapshot(
            true, false, 0, 5, 10, 2, 0, null, null, Array.Empty<ThumbnailActiveTaskSnapshot>()));

        var snapshot = service.GetStatusSnapshot();

        snapshot.GenerationStatusCode.Should().Be("paused");
    }

    [Fact]
    public void GetStatusSnapshot_WhenGenerating_ReturnsGeneratingCode()
    {
        var service = CreateService(new ThumbnailGenerationStatusSnapshot(
            false, false, 1, 5, 10, 2, 0, null, null, Array.Empty<ThumbnailActiveTaskSnapshot>()));

        var snapshot = service.GetStatusSnapshot();

        snapshot.GenerationStatusCode.Should().Be("generating");
    }

    [Fact]
    public void GetStatusSnapshot_WhenWaiting_ReturnsWaitingCode()
    {
        var service = CreateService(new ThumbnailGenerationStatusSnapshot(
            false, false, 0, 5, 10, 2, 3, null, null, Array.Empty<ThumbnailActiveTaskSnapshot>()));

        var snapshot = service.GetStatusSnapshot();

        snapshot.GenerationStatusCode.Should().Be("waiting");
    }

    [Fact]
    public void GetStatusSnapshot_WhenComplete_ReturnsCompleteCode()
    {
        var service = CreateService(new ThumbnailGenerationStatusSnapshot(
            false, false, 0, 10, 10, 0, 0, null, null, Array.Empty<ThumbnailActiveTaskSnapshot>()));

        var snapshot = service.GetStatusSnapshot();

        snapshot.GenerationStatusCode.Should().Be("complete");
    }

    [Fact]
    public void GetStatusSnapshot_WhenIdle_ReturnsIdleCode()
    {
        var service = CreateService(new ThumbnailGenerationStatusSnapshot(
            false, false, 0, 0, 0, 0, 0, null, null, Array.Empty<ThumbnailActiveTaskSnapshot>()));

        var snapshot = service.GetStatusSnapshot();

        snapshot.GenerationStatusCode.Should().Be("idle");
    }

    [Fact]
    public void GetPanelSnapshot_FormatsPanelDisplayState()
    {
        var service = CreateService(new ThumbnailGenerationStatusSnapshot(
            false,
            true,
            1,
            4,
            10,
            6,
            2,
            "Episode 03",
            "PlaybackCurrent",
            new[]
            {
                new ThumbnailActiveTaskSnapshot(
                    "/ep03.mp4",
                    "ep03.mp4",
                    ThumbnailWorkIntent.PlaybackCurrent,
                    ThumbnailState.Generating,
                    65,
                    true,
                    false)
            }));

        var localization = new Mock<ILocalizationService>();
        localization.Setup(service => service[It.IsAny<string>()]).Returns((string key) => key switch
        {
            "Settings.ThumbnailAcceleration.Hardware.None" => "Software only",
            "Settings.ThumbnailGeneration.Status.Generating" => "Generating",
            "Settings.ThumbnailGeneration.Status.Paused" => "Paused",
            "Settings.ThumbnailGeneration.Summary" => "{0} ready / {1} total",
            "TitleBar.ThumbnailBackgroundTasks" => "Current Tasks",
            "Settings.ThumbnailGeneration.Intent.Now" => "Now",
            "Settings.ThumbnailGeneration.Intent.Current" => "Current",
            "Settings.ThumbnailGeneration.Intent.Nearby" => "Nearby",
            "Settings.ThumbnailGeneration.Intent.Manual" => "Manual",
            "Settings.ThumbnailGeneration.Intent.Focused" => "Focused",
            "Settings.ThumbnailGeneration.Intent.Background" => "Background",
            _ => key
        });

        var panel = service.GetPanelSnapshot(localization.Object);

        panel.HardwareSummary.Should().Be("CUDA, QSV");
        panel.CurrentDecoderSummary.Should().Be("NVIDIA CUDA");
        panel.FallbackChainSummary.Should().Be("NVIDIA CUDA -> Software");
        panel.GenerationStatusCode.Should().Be("generating");
        panel.GenerationStatusText.Should().Be("Generating");
        panel.GenerationStatusColor.Should().Be("#007AFF");
        panel.GenerationProgressPercent.Should().Be(40);
        panel.GenerationSummaryText.Should().Be("4 ready / 10 total");
        panel.BackgroundTasksHeaderText.Should().Be("Current Tasks");
        panel.BackgroundTasks.Should().ContainSingle();
        panel.BackgroundTasks[0].IntentText.Should().Be("Now");
        panel.BackgroundTasks[0].StatusCode.Should().Be("generating");
        panel.BackgroundTasks[0].StatusText.Should().Be("Generating");
        panel.StatusLog.Should().Contain("code=generating");
    }

    private static ShellThumbnailStatusService CreateService(ThumbnailGenerationStatusSnapshot generationStatus)
    {
        var preferences = new Mock<IShellPreferencesService>();
        preferences.Setup(service => service.GetSnapshot()).Returns(
            new ShellPreferencesSnapshot(
                "zh-CN",
                "none",
                "balanced",
                "auto",
                new ThumbnailDecodeStatusSnapshot(
                    ThumbnailAccelerationMode.Auto,
                    new[] { ThumbnailDecodeStrategy.NvidiaCuda, ThumbnailDecodeStrategy.Software },
                    ThumbnailDecodeStrategy.NvidiaCuda,
                    true,
                    true,
                    false)));

        var generator = new Mock<IThumbnailGenerator>();
        generator.Setup(service => service.GetStatusSnapshot()).Returns(generationStatus);

        return new ShellThumbnailStatusService(preferences.Object, generator.Object);
    }
}
