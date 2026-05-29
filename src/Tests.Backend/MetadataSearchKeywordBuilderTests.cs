using AniNest.Application.Metadata;
using AniNest.Core.Enums;
using AniNest.Host.Modules;

namespace AniNest.Backend.Tests;

public sealed class MetadataSearchKeywordBuilderTests
{
    [Fact]
    public void Build_IncludesSeasonAwareCjkVariantsAndOriginalSearchSeed()
    {
        var context = new MetadataPreparedContext(
            new MetadataRecord(
                "sample",
                string.Empty,
                "Re：从零开始的异世界生活 第四季 丧失篇",
                string.Empty,
                MetadataState.NeedsMetadata,
                MetadataFailureKind.None,
                null,
                null,
                null,
                null,
                null,
                null),
            new MetadataFolderRef(
                "sample",
                string.Empty,
                "Re：从零开始的异世界生活 第四季 丧失篇",
                null,
                [],
                0),
            new MetadataKeywordPlan(
                "Re:从零开始的异世界生活 丧失篇 第4季",
                "Re:从零开始的异世界生活 丧失篇 第四季",
                "Re:从零开始的异世界生活",
                "Re:从零开始的异世界生活 丧失篇",
                4,
                null,
                false,
                false),
            "Re:从零开始的异世界生活 第四季 丧失篇",
            "Re:从零开始的异世界生活 丧失篇",
            ["Re:从零开始的异世界生活 丧失篇"],
            4,
            null,
            false);

        var keywords = MetadataSearchKeywordBuilder.Build(context);

        Assert.Contains("Re:从零开始的异世界生活 第四季 丧失篇", keywords);
        Assert.Contains("Re:从零开始的异世界生活 丧失篇 第4季", keywords);
        Assert.Contains("Re:从零开始的异世界生活 丧失篇 第四季", keywords);
        Assert.Contains("Re:从零开始的异世界生活 丧失篇 第4期", keywords);
    }
}
