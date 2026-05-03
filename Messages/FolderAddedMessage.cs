namespace LocalPlayer.Messages;

public record FolderAddedMessage(string Name, string Path, int VideoCount, string? CoverPath);
