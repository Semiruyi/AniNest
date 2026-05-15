using System.ComponentModel;
using AniNest.Features.Library;
using AniNest.Features.Library.Services;
using AniNest.Features.Player;
using AniNest.Features.Player.Input;
using AniNest.Features.Player.Services;
using AniNest.Features.Player.Settings;
using AniNest.Features.Shell;
using AniNest.Features.Shell.Services;
using AniNest.Infrastructure.Interop;
using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Presentation;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Tests.View;

public class ShellViewModelTests
{
    [Fact]
    public void SwitchLanguage_RefreshesLocalizedShellDisplayText()
    {
        var localization = new Mock<ILocalizationService>();
        string currentLanguage = "zh-CN";
        localization.SetupGet(service => service.CurrentLanguage).Returns(() => currentLanguage);
        localization.Setup(service => service.AvailableLanguages).Returns(Array.Empty<LanguageInfo>());
        localization.Setup(service => service[It.IsAny<string>()]).Returns((string key) => (currentLanguage, key) switch
        {
            ("zh-CN", "App.File") => "文件",
            ("en-US", "App.File") => "File",
            ("zh-CN", "KeyBinding.Back") => "返回",
            ("en-US", "KeyBinding.Back") => "Back",
            ("zh-CN", "Settings.ThumbnailPerformance.Paused") => "已暂停",
            ("en-US", "Settings.ThumbnailPerformance.Paused") => "Paused",
            ("zh-CN", "Settings.ThumbnailAcceleration.Auto") => "自动",
            ("en-US", "Settings.ThumbnailAcceleration.Auto") => "Auto",
            ("zh-CN", "Settings.FullscreenAnimation.NoAnimation") => "无动画",
            ("en-US", "Settings.FullscreenAnimation.NoAnimation") => "No animation",
            ("zh-CN", "Settings.FullscreenAnimation.ContinuousAnimation") => "连续动画",
            ("en-US", "Settings.FullscreenAnimation.ContinuousAnimation") => "Continuous",
            _ => key
        });
        localization.Setup(service => service.SetLanguage(It.IsAny<string>()))
            .Callback<string>(code => currentLanguage = code);
        localization.SetupAdd(service => service.PropertyChanged += It.IsAny<PropertyChangedEventHandler>())
            .Callback<PropertyChangedEventHandler>(_ => { });
        localization.SetupRemove(service => service.PropertyChanged -= It.IsAny<PropertyChangedEventHandler>())
            .Callback<PropertyChangedEventHandler>(_ => { });

        var viewModel = CreateViewModel(localization.Object);

        viewModel.TitleBarPrimaryActionText.Should().Be("文件");
        viewModel.SettingsPanel.ThumbnailPerformanceSummary.Should().Be("已暂停");
        viewModel.SettingsPanel.FullscreenAnimationOptions[0].DisplayName.Should().Be("无动画");

        viewModel.SettingsPanel.SwitchLanguageCommand.Execute("en-US");

        viewModel.TitleBarPrimaryActionText.Should().Be("File");
        viewModel.SettingsPanel.ThumbnailPerformanceSummary.Should().Be("Paused");
        viewModel.SettingsPanel.ThumbnailAccelerationSummary.Should().Be("Auto");
        viewModel.SettingsPanel.FullscreenAnimationOptions[0].DisplayName.Should().Be("No animation");
        viewModel.SettingsPanel.FullscreenAnimationOptions[1].DisplayName.Should().Be("Continuous");
    }

    private static ShellViewModel CreateViewModel(ILocalizationService localization)
    {
        var settingsStateService = new Mock<IShellSettingsStateService>();
        settingsStateService.Setup(service => service.GetStateSnapshot()).Returns(() =>
            new ShellSettingsStateSnapshot(
                new ShellPreferencesSnapshot(
                    localization.CurrentLanguage,
                    "none",
                    "paused",
                    "auto",
                    new ThumbnailDecodeStatusSnapshot(
                        ThumbnailAccelerationMode.Auto,
                        new[] { ThumbnailDecodeStrategy.Software },
                        ThumbnailDecodeStrategy.Software,
                        false,
                        false,
                        false)),
                localization.CurrentLanguage.Equals("en-US", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                0,
                0,
                0,
                !localization.CurrentLanguage.Equals("en-US", StringComparison.OrdinalIgnoreCase),
                localization.CurrentLanguage.Equals("en-US", StringComparison.OrdinalIgnoreCase),
                true,
                false,
                true,
                false,
                false,
                false,
                true,
                true,
                false));
        settingsStateService.Setup(service => service.GetOptionsPanelSnapshot(localization)).Returns(() =>
            new ShellOptionsPanelSnapshot(
                new[]
                {
                    new ShellSelectableOptionSnapshot("zh-CN", "简体中文", !localization.CurrentLanguage.Equals("en-US", StringComparison.OrdinalIgnoreCase)),
                    new ShellSelectableOptionSnapshot("en-US", "English", localization.CurrentLanguage.Equals("en-US", StringComparison.OrdinalIgnoreCase))
                },
                new[]
                {
                    new ShellSelectableOptionSnapshot("none", localization["Settings.FullscreenAnimation.NoAnimation"], true),
                    new ShellSelectableOptionSnapshot("continuous", localization["Settings.FullscreenAnimation.ContinuousAnimation"], false)
                }));

        var shellSettingsAppService = new Mock<IShellSettingsAppService>();
        shellSettingsAppService.Setup(service => service.SetLanguage(It.IsAny<string>()))
            .Callback<string>(code => localization.SetLanguage(code));

        var thumbnailStatusService = new Mock<IShellThumbnailStatusService>();
        thumbnailStatusService.Setup(service => service.GetPanelSnapshot(localization)).Returns(() =>
            new ShellThumbnailPanelSnapshot(
                "Software only",
                "Software",
                "Software",
                "paused",
                localization["Settings.ThumbnailGeneration.Status.Paused"],
                "#C62828",
                0,
                "0 / 0",
                "Tasks",
                Array.Empty<ShellThumbnailPanelTaskItemSnapshot>(),
                "code=paused"));

        var thumbnailGenerator = new Mock<IThumbnailGenerator>();
        thumbnailGenerator.SetupAdd(service => service.StatusChanged += It.IsAny<Action>())
            .Callback<Action>(_ => { });
        thumbnailGenerator.SetupRemove(service => service.StatusChanged -= It.IsAny<Action>())
            .Callback<Action>(_ => { });

        var mainPage = new MainPageViewModel(
            Mock.Of<ILibraryAppService>(),
            Mock.Of<AniNest.Features.Metadata.IMetadataQueryService>(),
            localization,
            Mock.Of<IDialogService>(),
            Mock.Of<IUiDispatcher>());

        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetPlayerVolume()).Returns(70);
        settings.Setup(service => service.GetPlayerMuted()).Returns(false);

        var playbackFacade = new Mock<IPlayerPlaybackFacade>();
        playbackFacade.SetupGet(service => service.Rate).Returns(1.0f);
        playbackFacade.SetupProperty(service => service.Volume, 70);
        playbackFacade.SetupProperty(service => service.IsMuted, false);

        var playerViewModel = new PlayerViewModel(
            new PlayerSessionController(
                Mock.Of<IPlayerThumbnailSyncService>(),
                new PlayerPlaylistService(
                    settings.Object,
                    Mock.Of<AniNest.Features.Player.Playback.IPlaybackEngine>(),
                    new VideoScanner(),
                    localization,
                    playbackFacade.Object)),
            new PlayerPlaybackStateController(
                Mock.Of<AniNest.Features.Player.Playback.IPlaybackEngine>(),
                Mock.Of<IPlayerPlaybackStateSyncService>()),
            playbackFacade.Object,
            localization,
            settings.Object,
            Mock.Of<IUiDispatcher>(),
            Mock.Of<IDialogService>(),
            Mock.Of<IPlayerInputService>());

        var playerInputSettings = new PlayerInputSettingsViewModel(
            Mock.Of<IPlayerInputService>(service => service.CurrentProfile == PlayerInputDefaults.Create()),
            localization,
            Mock.Of<IDialogService>());

        var settingsPanel = new ShellSettingsPanelViewModel(
            localization,
            settingsStateService.Object,
            shellSettingsAppService.Object,
            Mock.Of<IShellThumbnailPerformanceAppService>());

        var thumbnailStatusPanel = new ShellThumbnailStatusPanelViewModel(
            localization,
            thumbnailStatusService.Object,
            thumbnailGenerator.Object,
            Mock.Of<IUiDispatcher>());

        return new ShellViewModel(
            localization,
            Mock.Of<ILibraryAppService>(),
            Mock.Of<ITaskbarAutoHideCoordinator>(),
            Mock.Of<IShellNavigationAppService>(),
            Mock.Of<IDialogService>(),
            Mock.Of<IFolderPickerService>(),
            Mock.Of<IApplicationLifecycle>(),
            mainPage,
            playerViewModel,
            playerInputSettings,
            settingsPanel,
            thumbnailStatusPanel);
    }
}
