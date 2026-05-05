namespace LocalPlayer.Infrastructure.Model;

public record FolderListItem(string Name, string Path, int VideoCount, string? CoverPath)
{
    public string VideoCountText { get; set; } = "";
}

