using Xunit;

namespace AniNest.Tests.Model;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ThumbnailStorageTestCollection
{
    public const string Name = "ThumbnailStorageTests";
}
