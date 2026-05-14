using AniNest.Features.Metadata;

namespace AniNest.Tests.View;

public class MetadataRepositoryTests
{
    [Fact]
    public void SaveAndGet_RoundTripsMetadata()
    {
        var folderPath = "/library/folder";
        var metadata = new FolderMetadata
        {
            FolderPath = folderPath,
            Title = "Test Title",
            OriginalTitle = "Original Title",
            LocalPosterPath = "/cache/poster.jpg",
            Rating = 8.7,
            ScrapedAt = DateTime.UtcNow
        };

        var repository = new MetadataRepository();

        repository.Delete(folderPath);
        repository.Save(metadata);

        var loaded = repository.Get(folderPath);

        loaded.Should().NotBeNull();
        loaded!.FolderPath.Should().Be(folderPath);
        loaded.Title.Should().Be("Test Title");
        loaded.OriginalTitle.Should().Be("Original Title");
        loaded.LocalPosterPath.Should().Be("/cache/poster.jpg");
        loaded.Rating.Should().Be(8.7);

        repository.Delete(folderPath);
    }
}
