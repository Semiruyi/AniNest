using System.Collections.ObjectModel;
using LocalPlayer.Features.Player.Models;

namespace LocalPlayer.Features.Player.Services;

public interface IPlayerPlaylistService
{
    PlaylistViewModel Playlist { get; }
    PlaylistItem? CurrentItem { get; }
    ObservableCollection<PlaylistItem> Items { get; }

    Task LoadFolderSkeletonAsync(string folderPath, string folderName, CancellationToken cancellationToken);
    Task LoadFolderDataAsync(CancellationToken cancellationToken);
    void ActivateCurrentVideo();
    bool PlayNext();
    bool PlayPrevious();
    void SaveProgress();
    void Cleanup();
}
