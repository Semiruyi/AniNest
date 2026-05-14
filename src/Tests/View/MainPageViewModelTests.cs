using System.ComponentModel;
using AniNest.Features.Library;
using AniNest.Features.Library.Models;
using AniNest.Features.Library.Services;
using AniNest.Features.Metadata;
using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Presentation;

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

        var viewModel = CreateViewModel(libraryService.Object, localization.Object);

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
        var viewModel = CreateViewModel(libraryService.Object, localization.Object);
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

        var viewModel = CreateViewModel(libraryService.Object, localization.Object);

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
        var viewModel = CreateViewModel(libraryService.Object, localization.Object);
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
        var viewModel = CreateViewModel(libraryService.Object, localization.Object);
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
        var viewModel = CreateViewModel(libraryService.Object, localization.Object);
        viewModel.AddFolderItem("Folder", "/folder", 4, null);
        viewModel.SetSelectedFilterCommand.Execute(LibraryFilter.Unsorted);

        viewModel.FolderItems.Should().ContainSingle();

        await viewModel.SetFolderWatchStatusCommand.ExecuteAsync(new FolderStatusChangeRequest(viewModel.FolderItems[0], WatchStatus.Completed));

        viewModel.FolderItems.Should().BeEmpty();
        libraryService.Verify(service => service.SetFolderWatchStatusAsync("/folder", WatchStatus.Completed, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetFolderWatchStatusCommand_DoesNothing_WhenStatusIsUnchanged()
    {
        var libraryService = new Mock<ILibraryAppService>();
        var localization = CreateLocalizationService();
        var viewModel = CreateViewModel(libraryService.Object, localization.Object);
        viewModel.AddFolderItem("Folder", "/folder", 4, null);

        await viewModel.SetFolderWatchStatusCommand.ExecuteAsync(
            new FolderStatusChangeRequest(viewModel.FolderItems[0], WatchStatus.Unsorted));

        viewModel.FolderItems[0].Status.Should().Be(WatchStatus.Unsorted);
        libraryService.Verify(
            service => service.SetFolderWatchStatusAsync("/folder", It.IsAny<WatchStatus>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void MetadataRefresh_UpdatesExistingFolderItem()
    {
        var libraryService = new Mock<ILibraryAppService>();
        var metadataQueryService = new Mock<IMetadataQueryService>();
        var localization = CreateLocalizationService();
        EventHandler<FolderMetadataRefreshedEventArgs>? handler = null;

        metadataQueryService.SetupAdd(service => service.FolderMetadataRefreshed += It.IsAny<EventHandler<FolderMetadataRefreshedEventArgs>>())
            .Callback<EventHandler<FolderMetadataRefreshedEventArgs>>(value => handler += value);
        metadataQueryService.SetupRemove(service => service.FolderMetadataRefreshed -= It.IsAny<EventHandler<FolderMetadataRefreshedEventArgs>>())
            .Callback<EventHandler<FolderMetadataRefreshedEventArgs>>(value => handler -= value);

        var viewModel = CreateViewModel(libraryService.Object, localization.Object, metadataQueryService.Object);
        viewModel.AddFolderItem("Folder", "/folder", 4, null);

        handler.Should().NotBeNull();
        handler!.Invoke(viewModel, new FolderMetadataRefreshedEventArgs("/folder", new FolderMetadata
        {
            FolderPath = "/folder",
            Title = "Title",
            LocalPosterPath = "/cache/poster.jpg"
        }));

        viewModel.FolderItems[0].Metadata.Should().NotBeNull();
        viewModel.FolderItems[0].Metadata!.Title.Should().Be("Title");
        viewModel.FolderItems[0].EffectiveCoverPath.Should().Be("/cache/poster.jpg");
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

    private static MainPageViewModel CreateViewModel(
        ILibraryAppService libraryService,
        ILocalizationService localization,
        IMetadataQueryService? metadataQueryService = null)
    {
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(service => service.ShowInput(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(string.Empty);

        metadataQueryService ??= Mock.Of<IMetadataQueryService>();

        var dispatcher = new Mock<IUiDispatcher>();
        dispatcher.Setup(service => service.CheckAccess()).Returns(true);
        dispatcher.Setup(service => service.Invoke(It.IsAny<Action>()))
            .Callback<Action>(action => action());
        dispatcher.Setup(service => service.BeginInvoke(It.IsAny<Action>()))
            .Callback<Action>(action => action());

        return new MainPageViewModel(libraryService, metadataQueryService, localization, dialogs.Object, dispatcher.Object);
    }
}
