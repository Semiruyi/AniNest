using System.Collections.ObjectModel;
using LocalPlayer.Features.Player.Models;
using LocalPlayer.Infrastructure.Localization;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Thumbnails;

namespace LocalPlayer.Features.Player.Services;

public sealed class PlayerPlaylistService : IPlayerPlaylistService
{
    private readonly PlaylistManager _playlistManager;

    public PlayerPlaylistService(
        ISettingsService settings,
        IMediaPlayerController media,
        IVideoScanner videoScanner,
        ILocalizationService localization,
        IPlayerPlaybackFacade playbackFacade)
    {
        _playlistManager = new PlaylistManager(settings, media, videoScanner, playbackFacade.GetThumbnailState);
        Playlist = new PlaylistViewModel(localization);
        Playlist.SetPlaylistManager(_playlistManager);
    }

    public PlaylistViewModel Playlist { get; }
    public PlaylistItem? CurrentItem => _playlistManager.CurrentItem;
    public ObservableCollection<PlaylistItem> Items => _playlistManager.Items;

    public Task LoadFolderSkeletonAsync(string folderPath, string folderName, CancellationToken cancellationToken)
        => Playlist.LoadFolderSkeletonAsync(folderPath, folderName, cancellationToken);

    public Task LoadFolderDataAsync(CancellationToken cancellationToken)
        => Playlist.LoadFolderDataAsync(cancellationToken);

    public void ActivateCurrentVideo()
        => Playlist.ActivateCurrentVideo();

    public bool PlayNext()
        => Playlist.PlayNext();

    public bool PlayPrevious()
        => Playlist.PlayPrevious();

    public void SaveProgress()
        => Playlist.SaveProgress();

    public void Cleanup()
        => Playlist.Cleanup();
}
