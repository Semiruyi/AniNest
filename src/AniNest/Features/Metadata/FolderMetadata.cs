namespace AniNest.Features.Metadata;

public sealed class FolderMetadata
{
    public string FolderPath { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? OriginalTitle { get; set; }
    public string? Summary { get; set; }
    public string? PosterUrl { get; set; }
    public string? LocalPosterPath { get; set; }
    public string? Date { get; set; }
    public double? Rating { get; set; }
    public int? Episodes { get; set; }
    public string? Platform { get; set; }
    public List<string> Tags { get; set; } = [];
    public string? SourceId { get; set; }
    public DateTime ScrapedAt { get; set; }
}
