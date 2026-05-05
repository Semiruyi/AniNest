namespace LocalPlayer.Core.Messaging;

public record FolderAddedMessage(string Name, string Path, int VideoCount, string? CoverPath);

