using System.ComponentModel;
using AniNest.Features.Library;
using AniNest.Features.Library.Models;
using AniNest.Features.Library.Services;
using AniNest.Infrastructure.Persistence;
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

    [Fact]
    public async Task PrioritizeFolderThumbnailsCommand_CallsLibraryService()
    {
        var libraryService = new Mock<ILibraryAppService>();
        var localization = CreateLocalizationService();
        libraryService
            .Setup(service => service.PrioritizeFolderThumbnailsAsync("/folder", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var viewModel = new MainPageViewModel(libraryService.Object, localization.Object);
        viewModel.AddFolderItem("Folder", "/folder", 4, null);

        await viewModel.PrioritizeFolderThumbnailsCommand.ExecuteAsync(viewModel.FolderItems[0]);

        libraryService.Verify(service => service.PrioritizeFolderThumbnailsAsync("/folder", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SelectedFilter_FiltersLoadedItems()
    {
        var libraryService = new Mock<ILibraryAppService>();
        var localization = CreateLocalizationService();
        libraryService
            .Setup(service => service.LoadLibraryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new LibraryFolderDto("Watching", "/watching", 4, null, 0, WatchStatus.Watching, false),
                new LibraryFolderDto("Favorite", "/favorite", 4, null, 0, WatchStatus.Unsorted, true),
                new LibraryFolderDto("Other", "/other", 4, null, 0, WatchStatus.Completed, false),
            });

        var viewModel = new MainPageViewModel(libraryService.Object, localization.Object);

        await viewModel.LoadDataCommand.ExecuteAsync(null);
        viewModel.FolderItems.Should().HaveCount(3);

        viewModel.SetSelectedFilterCommand.Execute(LibraryFilter.Favorites);
        viewModel.FolderItems.Should().ContainSingle(item => item.Path == "/favorite");

        viewModel.SetSelectedFilterCommand.Execute(LibraryFilter.Watching);
        viewModel.FolderItems.Should().ContainSingle(item => item.Path == "/watching");
    }

    [Fact]
    public async Task ToggleFavoriteCommand_UpdatesItemAndCallsLibraryService()
    {
        var libraryService = new Mock<ILibraryAppService>();
        var localization = CreateLocalizationService();
        libraryService
            .Setup(service => service.SetFolderFavoriteAsync("/folder", true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var viewModel = new MainPageViewModel(libraryService.Object, localization.Object);
        viewModel.AddFolderItem("Folder", "/folder", 4, null);

        await viewModel.ToggleFolderFavoriteCommand.ExecuteAsync(viewModel.FolderItems[0]);

        viewModel.FolderItems[0].IsFavorite.Should().BeTrue();
        libraryService.Verify(service => service.SetFolderFavoriteAsync("/folder", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetFolderWatchStatusCommand_UpdatesItemAndCallsLibraryService()
    {
        var libraryService = new Mock<ILibraryAppService>();
        var localization = CreateLocalizationService();
        libraryService
            .Setup(service => service.SetFolderWatchStatusAsync("/folder", WatchStatus.Completed, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var viewModel = new MainPageViewModel(libraryService.Object, localization.Object);
        viewModel.AddFolderItem("Folder", "/folder", 4, null);

        await viewModel.SetFolderWatchStatusCommand.ExecuteAsync(new FolderStatusChangeRequest(viewModel.FolderItems[0], WatchStatus.Completed));

        viewModel.FolderItems[0].Status.Should().Be(WatchStatus.Completed);
        libraryService.Verify(service => service.SetFolderWatchStatusAsync("/folder", WatchStatus.Completed, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetFolderWatchStatusAsync_RemovesItem_WhenItNoLongerMatchesSelectedFilter()
    {
        var libraryService = new Mock<ILibraryAppService>();
        var localization = CreateLocalizationService();
        libraryService
            .Setup(service => service.SetFolderWatchStatusAsync("/folder", WatchStatus.Completed, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var viewModel = new MainPageViewModel(libraryService.Object, localization.Object);
        viewModel.AddFolderItem("Folder", "/folder", 4, null);
        viewModel.SetSelectedFilterCommand.Execute(LibraryFilter.Unsorted);

        viewModel.FolderItems.Should().ContainSingle();

        await viewModel.SetFolderWatchStatusCommand.ExecuteAsync(new FolderStatusChangeRequest(viewModel.FolderItems[0], WatchStatus.Completed));

        viewModel.FolderItems.Should().BeEmpty();
        libraryService.Verify(service => service.SetFolderWatchStatusAsync("/folder", WatchStatus.Completed, It.IsAny<CancellationToken>()), Times.Once);
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
