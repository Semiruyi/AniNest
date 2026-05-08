using FluentAssertions;
using Moq;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Thumbnails;
using Xunit;

namespace AniNest.Tests.Model;

public class ThumbnailDecodeStrategyServiceTests
{
    [Fact]
    public void GetStrategyChain_WhenMachineChanges_ClearsStalePreferredDecoder()
    {
        var settings = new AppSettings
        {
            ThumbnailDecoderMachineId = "old-machine",
            ThumbnailPreferredDecoder = "NvidiaCuda"
        };
        var service = CreateService(settings, "new-machine", new ThumbnailHardwareProbeResult(false, true, true));

        var chain = service.GetStrategyChain();

        settings.ThumbnailDecoderMachineId.Should().Be("new-machine");
        settings.ThumbnailPreferredDecoder.Should().BeEmpty();
        chain.Should().ContainInOrder(
            ThumbnailDecodeStrategy.IntelQsv,
            ThumbnailDecodeStrategy.D3D11VA,
            ThumbnailDecodeStrategy.AutoHardware,
            ThumbnailDecodeStrategy.Software);
    }

    [Fact]
    public void GetStrategyChain_PutsPreferredDecoderFirst()
    {
        var settings = new AppSettings
        {
            ThumbnailDecoderMachineId = "same-machine",
            ThumbnailPreferredDecoder = "D3D11VA"
        };
        var service = CreateService(settings, "same-machine", new ThumbnailHardwareProbeResult(true, true, true));

        var chain = service.GetStrategyChain();

        chain[0].Should().Be(ThumbnailDecodeStrategy.D3D11VA);
        chain.Should().Contain(ThumbnailDecodeStrategy.NvidiaCuda);
        chain.Should().Contain(ThumbnailDecodeStrategy.IntelQsv);
        chain.Should().Contain(ThumbnailDecodeStrategy.AutoHardware);
        chain.Should().Contain(ThumbnailDecodeStrategy.Software);
    }

    [Fact]
    public void RecordSuccess_UpdatesPreferredDecoder()
    {
        var settings = new AppSettings
        {
            ThumbnailDecoderMachineId = "same-machine"
        };
        var service = CreateService(settings, "same-machine", new ThumbnailHardwareProbeResult(false, false, false));

        service.RecordSuccess(ThumbnailDecodeStrategy.AutoHardware);

        settings.ThumbnailPreferredDecoder.Should().Be(nameof(ThumbnailDecodeStrategy.AutoHardware));
    }

    private static ThumbnailDecodeStrategyService CreateService(
        AppSettings appSettings,
        string machineId,
        ThumbnailHardwareProbeResult probeResult)
    {
        var settingsService = new Mock<ISettingsService>();
        settingsService.Setup(x => x.Load()).Returns(appSettings);
        settingsService.Setup(x => x.Save());
        settingsService.Setup(x => x.GetThumbnailPerformanceMode()).Returns(ThumbnailPerformanceMode.Balanced);

        return new ThumbnailDecodeStrategyService(
            settingsService.Object,
            () => machineId,
            () => probeResult);
    }
}
