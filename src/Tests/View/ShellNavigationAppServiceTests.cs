using AniNest.Features.Player.Services;
using AniNest.Features.Shell.Services;

namespace AniNest.Tests.View;

public class ShellNavigationAppServiceTests
{
    [Fact]
    public void CanEnterPlayerPage_ReturnsTrue_OnlyWhenOnMainPageWithoutPendingTransition()
    {
        var service = new ShellNavigationAppService(Mock.Of<IPlayerAppService>());

        service.CanEnterPlayerPage(false, true).Should().BeTrue();
        service.CanEnterPlayerPage(true, true).Should().BeFalse();
        service.CanEnterPlayerPage(false, false).Should().BeFalse();
    }

    [Fact]
    public void CanLeavePlayerPage_ReturnsTrue_OnlyWhenOnPlayerPageWithoutPendingTransition()
    {
        var service = new ShellNavigationAppService(Mock.Of<IPlayerAppService>());

        service.CanLeavePlayerPage(false, true).Should().BeTrue();
        service.CanLeavePlayerPage(true, true).Should().BeFalse();
        service.CanLeavePlayerPage(false, false).Should().BeFalse();
    }

    [Fact]
    public async Task BeginEnterPlayerPageAsync_DelegatesToPlayerAppService()
    {
        var playerAppService = new Mock<IPlayerAppService>();
        var service = new ShellNavigationAppService(playerAppService.Object);

        await service.BeginEnterPlayerPageAsync("continuous", "/anime", "Anime");

        playerAppService.Verify(
            app => app.EnterPlayerAsync("continuous", "/anime", "Anime"),
            Times.Once);
    }

    [Fact]
    public async Task BeginLeavePlayerPageAsync_DelegatesToPlayerAppService()
    {
        var playerAppService = new Mock<IPlayerAppService>();
        var service = new ShellNavigationAppService(playerAppService.Object);

        await service.BeginLeavePlayerPageAsync();

        playerAppService.Verify(app => app.BeginLeavePlayerAsync(), Times.Once);
    }

    [Fact]
    public void CompletePlayerPageTransition_OnPlayerPage_ActivatesPlayerPageTransition()
    {
        var playerAppService = new Mock<IPlayerAppService>();
        var service = new ShellNavigationAppService(playerAppService.Object);

        service.CompletePlayerPageTransition(true);

        playerAppService.Verify(app => app.OnPlayerPageTransitionCompleted(), Times.Once);
        playerAppService.Verify(app => app.CompleteLeavePlayerTransition(), Times.Never);
    }

    [Fact]
    public void CompletePlayerPageTransition_OnMainPage_CompletesLeaveTransition()
    {
        var playerAppService = new Mock<IPlayerAppService>();
        var service = new ShellNavigationAppService(playerAppService.Object);

        service.CompletePlayerPageTransition(false);

        playerAppService.Verify(app => app.CompleteLeavePlayerTransition(), Times.Once);
        playerAppService.Verify(app => app.OnPlayerPageTransitionCompleted(), Times.Never);
    }
}
