using AniNest.Application.Metadata;
using AniNest.Core.Enums;
using AniNest.Host.Modules;
using Microsoft.Extensions.Logging.Abstractions;

namespace AniNest.Backend.Tests;

public sealed class MetadataAcquisitionServiceTests
{
    [Fact]
    public async Task AcquireAsync_PreservesTopSeasonSpecificCandidateForHydration()
    {
        var provider = new FakeAnimeMetadataProvider();
        provider.SearchResults["Re:从零开始的异世界生活 第四季 丧失篇"] =
        [
            new ProviderSearchResult("547888", "Re：从零开始的异世界生活 第四季 丧失篇", "Re:ゼロから始める異世界生活 4th season 喪失編", 2026, "bangumi"),
            new ProviderSearchResult("140001", "Re：从零开始的异世界生活", "Re:ゼロから始める異世界生活", 2016, "bangumi")
        ];
        provider.SearchResults["Re:从零开始的异世界生活 丧失篇 第4季"] =
        [
            new ProviderSearchResult("547888", "Re：从零开始的异世界生活 第四季 丧失篇", "Re:ゼロから始める異世界生活 4th season 喪失編", 2026, "bangumi")
        ];
        provider.SearchResults["Re:从零开始的异世界生活 丧失篇 第四季"] =
        [
            new ProviderSearchResult("547888", "Re：从零开始的异世界生活 第四季 丧失篇", "Re:ゼロから始める異世界生活 4th season 喪失編", 2026, "bangumi")
        ];
        provider.SearchResults["Re:从零开始的异世界生活 丧失篇 第4期"] =
        [
            new ProviderSearchResult("140001", "Re：从零开始的异世界生活", "Re:ゼロから始める異世界生活", 2016, "bangumi")
        ];
        provider.SearchResults["Re:从零开始的异世界生活 丧失篇 4th season"] =
        [
            new ProviderSearchResult("547888", "Re：从零开始的异世界生活 第四季 丧失篇", "Re:ゼロから始める異世界生活 4th season 喪失編", 2026, "bangumi")
        ];
        provider.SearchResults["Re:从零开始的异世界生活"] =
        [
            new ProviderSearchResult("140001", "Re：从零开始的异世界生活", "Re:ゼロから始める异世界生活", 2016, "bangumi"),
            new ProviderSearchResult("225462", "Re：从零开始的异世界生活 Memory Snow", "Re:ゼロから始める異世界生活 Memory Snow", 2018, "bangumi"),
            new ProviderSearchResult("261805", "Re：从零开始的异世界生活 冰结之绊", "Re:ゼロから始める異世界生活 氷結の絆", 2019, "bangumi")
        ];
        provider.SearchResults["Re:从零开始的异世界生活 丧失篇"] =
        [
            new ProviderSearchResult("547888", "Re：从零开始的异世界生活 第四季 丧失篇", "Re:ゼロから始める異世界生活 4th season 喪失編", 2026, "bangumi")
        ];

        provider.Details["547888"] = new ProviderSubjectDetail(
            "547888",
            "Re:ゼロから始める異世界生活 4th season 喪失編",
            "Re:ゼロから始める異世界生活 4th season 喪失編",
            null,
            null,
            "2026-04-08",
            2026,
            null,
            11,
            [],
            [],
            "bangumi");
        provider.Details["140001"] = new ProviderSubjectDetail(
            "140001",
            "Re:ゼロから始める異世界生活",
            "Re:ゼロから始める異世界生活",
            null,
            null,
            "2016-04-04",
            2016,
            null,
            25,
            [],
            [],
            "bangumi");
        provider.Details["225462"] = new ProviderSubjectDetail(
            "225462",
            "Re:ゼロから始める異世界生活 Memory Snow",
            "Re:ゼロから始める異世界生活 Memory Snow",
            null,
            null,
            "2018-10-06",
            2018,
            null,
            1,
            [],
            [],
            "bangumi");
        provider.Details["261805"] = new ProviderSubjectDetail(
            "261805",
            "Re:ゼロから始める異世界生活 氷結の絆",
            "Re:ゼロから始める異世界生活 氷結の絆",
            null,
            null,
            "2019-11-08",
            2019,
            null,
            1,
            [],
            [],
            "bangumi");

        var service = new MetadataAcquisitionService(provider, NullLogger<MetadataAcquisitionService>.Instance);

        var result = await service.AcquireAsync(CreateContext(), CancellationToken.None);

        Assert.True(result.SearchSucceeded);
        Assert.Contains(result.Candidates, candidate => candidate.SourceId == "547888");
        Assert.Equal("547888", result.Candidates[0].SourceId);
    }

    private static MetadataPreparedContext CreateContext()
        => new(
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

    private sealed class FakeAnimeMetadataProvider : IAnimeMetadataProvider
    {
        public Dictionary<string, IReadOnlyList<ProviderSearchResult>> SearchResults { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, ProviderSubjectDetail> Details { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<ProviderSearchResult>> SearchAsync(
            MetadataKeywordPlan plan,
            string keyword,
            int maxCount,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                SearchResults.TryGetValue(keyword, out var results)
                    ? results.Take(maxCount).ToArray() as IReadOnlyList<ProviderSearchResult>
                    : []);
        }

        public Task<ProviderSubjectDetail> GetSubjectAsync(string sourceId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Details[sourceId]);
        }

        public Task<Stream> DownloadPosterAsync(string imageUrl, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
