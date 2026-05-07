using CommunityToolkit.Mvvm.ComponentModel;

namespace AniNest.Features.Library.Models;

public partial class FolderListItem : ObservableObject
{
    public FolderListItem(string name, string path, int videoCount, string? coverPath)
    {
        Name = name;
        Path = path;
        VideoCount = videoCount;
        CoverPath = coverPath;
    }

    public string Name { get; }
    public string Path { get; }
    public int VideoCount { get; }
    public string? CoverPath { get; }

    [ObservableProperty]
    private string _videoCountText = "";

    [ObservableProperty]
    private bool _isPopupOpen;

    [ObservableProperty]
    private bool _canMoveToFront;
}


