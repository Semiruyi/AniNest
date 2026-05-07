using System.Collections.ObjectModel;
using AniNest.Features.Player.Models;

namespace AniNest.Features.Player.Services;

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
    void ResetSession();
    void Cleanup();
}
