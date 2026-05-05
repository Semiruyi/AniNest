using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LocalPlayer.Features.Library.Models;
using LocalPlayer.Infrastructure.Localization;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Thumbnails;

namespace LocalPlayer.Features.Library;

public class LibraryPageCoordinator : ILibraryPageCoordinator
{
    private readonly ISettingsService _settings;
    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly ILocalizationService _loc;

    public LibraryPageCoordinator(
        ISettingsService settings,
        IThumbnailGenerator thumbnailGenerator,
        ILocalizationService loc)
    {
        _settings = settings;
        _thumbnailGenerator = thumbnailGenerator;
        _loc = loc;
    }

    public List<(FolderListItem Item, string[] VideoFiles)> LoadFoldersData()
    {
        var items = new List<(FolderListItem Item, string[] VideoFiles)>();
        var folders = _settings.GetFolders();

        foreach (var folder in folders)
        {
            if (Directory.Exists(folder.Path))
            {
                var result = VideoScanner.ScanFolder(folder.Path);
                items.Add((CreateFolderItem(folder.Name, folder.Path, result.VideoCount, result.CoverPath), result.VideoFiles));
            }
            else
            {
                _settings.RemoveFolder(folder.Path);
                _thumbnailGenerator.DeleteForFolder(folder.Path);
            }
        }

        return items;
    }

    public void EnqueueAllFolders(IEnumerable<(FolderListItem Item, string[] VideoFiles)> items)
    {
        foreach (var (item, videoFiles) in items)
            EnqueueFolderForThumbnails(item, videoFiles);
    }

    public void EnqueueFolderForThumbnails(FolderListItem item, string[] videoFiles)
    {
        int cardOrder = 0;
        var folders = _settings.GetFolders();
        var folderInfo = folders.FirstOrDefault(f => f.Path == item.Path);
        if (folderInfo != null)
            cardOrder = folderInfo.OrderIndex;

        var folderProgress = _settings.GetFolderProgress(item.Path);
        string? lastPlayed = folderProgress?.LastVideoPath;

        var playedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var vf in videoFiles)
        {
            if (_settings.IsVideoPlayed(vf))
                playedPaths.Add(vf);
        }

        _thumbnailGenerator.EnqueueFolder(item.Path, cardOrder, lastPlayed, playedPaths);
    }

    public void ReorderFolders(List<string> orderedPaths)
        => _settings.ReorderFolders(orderedPaths);

    public void DeleteFolder(FolderListItem item)
    {
        _settings.RemoveFolder(item.Path);
        _thumbnailGenerator.DeleteForFolder(item.Path);
    }

    public FolderListItem CreateFolderItem(string name, string path, int videoCount, string? coverPath)
        => new(name, path, videoCount, coverPath)
        {
            VideoCountText = string.Format(_loc["Library.VideoCount"], videoCount)
        };
}
