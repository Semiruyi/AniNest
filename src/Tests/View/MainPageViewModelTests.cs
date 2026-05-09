using System.ComponentModel;
using AniNest.Features.Library;
using AniNest.Features.Library.Services;
using AniNest.Infrastructure.Localization;

namespace AniNest.Tests.View;

public class MainPageViewModelTests
{
    [Fact]
    public async Task OnAppearing_AfterInitialLoad_ReloadsLibraryData()
    {
        var libraryService = new Mock<ILibraryAppService>();
        var localization = CreateLocalizationService();

        var firstLoad = new[]
        {
            new LibraryFolderDto("Folder", "/folder", 4, null, 0)
        };
        var refreshedLoad = new[]
        {
            new LibraryFolderDto("Folder", "/folder", 4, null, 2)
        };

        libraryService
            .SetupSequence(service => service.LoadLibraryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstLoad)
            .ReturnsAsync(refreshedLoad);

        var viewModel = new MainPageViewModel(libraryService.Object, localization.Object);

        await viewModel.LoadDataCommand.ExecuteAsync(null);
        viewModel.FolderItems.Should().ContainSingle();
        viewModel.FolderItems[0].PlayedCount.Should().Be(0);

        viewModel.OnAppearing();

        libraryService.Verify(service => service.LoadLibraryAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        viewModel.FolderItems.Should().ContainSingle();
        viewModel.FolderItems[0].PlayedCount.Should().Be(2);
        viewModel.FolderItems[0].PlayedPercent.Should().Be(50);
    }

    private static Mock<ILocalizationService> CreateLocalizationService()
    {
        var localization = new Mock<ILocalizationService>();
        localization.Setup(service => service.CurrentLanguage).Returns("zh-CN");
        localization.Setup(service => service.AvailableLanguages).Returns(Array.Empty<LanguageInfo>());
        localization.Setup(service => service[It.IsAny<string>()]).Returns((string key) => key);
        localization.SetupAdd(service => service.PropertyChanged += It.IsAny<PropertyChangedEventHandler>())
            .Callback<PropertyChangedEventHandler>(_ => { });
        localization.SetupRemove(service => service.PropertyChanged -= It.IsAny<PropertyChangedEventHandler>())
            .Callback<PropertyChangedEventHandler>(_ => { });
        return localization;
    }
}
