using AniNest.Application.Metadata;
using AniNest.Core.Enums;
using AniNest.Host.Modules;

namespace AniNest.Backend.Tests;

public sealed class MetadataConfidenceServiceTests
{
    private readonly MetadataConfidenceService _service = new();

    [Fact]
    public void Evaluate_PrefersExactSeasonWhenCandidateUsesChineseNumeral()
    {
        var context = CreateContext(
            folderName: "咒术回战 第三季",
            searchSeed: "咒术回战 第三季",
            normalizedTitle: "咒术回战",
            baseTitle: "咒术回战",
            seasonNumber: 3);
        var result = _service.Evaluate(context, new MetadataAcquisitionResult(
            true,
            [
                CreateCandidate(
                    "season-2",
                    matchedTitle: "咒术回战 第二季",
                    detailTitle: "咒术回战 第二季",
                    bestRank: 1,
                    hitCount: 3),
                CreateCandidate(
                    "season-3",
                    matchedTitle: "咒术回战 第三季",
                    detailTitle: "呪術廻戦 死滅回游",
                    bestRank: 2,
                    hitCount: 1)
            ],
            null));

        Assert.NotNull(result.BestCandidate);
        Assert.Equal("season-3", result.BestCandidate.Candidate.SourceId);
        Assert.Contains("season.exact-match", result.BestCandidate.Reasons);
    }

    [Fact]
    public void Evaluate_RecognizesOrdinalSeasonTitles()
    {
        var context = CreateContext(
            folderName: "Re：从零开始的异世界生活 第四季 丧失篇",
            searchSeed: "Re:从零开始的异世界生活 第四季 丧失篇",
            normalizedTitle: "Re:从零开始的异世界生活 丧失篇",
            baseTitle: "Re:从零开始的异世界生活 丧失篇",
            seasonNumber: 4);
        var result = _service.Evaluate(context, new MetadataAcquisitionResult(
            true,
            [
                CreateCandidate(
                    "season-1",
                    matchedTitle: "Re:从零开始的异世界生活",
                    detailTitle: "Re:ゼロから始める異世界生活",
                    bestRank: 1,
                    hitCount: 3),
                CreateCandidate(
                    "season-4",
                    matchedTitle: "Re:从零开始的异世界生活 第四季",
                    detailTitle: "Re:ゼロから始める異世界生活 4th season 喪失編",
                    bestRank: 2,
                    hitCount: 1)
            ],
            null));

        Assert.NotNull(result.BestCandidate);
        Assert.Equal("season-4", result.BestCandidate.Candidate.SourceId);
        Assert.Contains("season.exact-match", result.BestCandidate.Reasons);
    }

    [Fact]
    public void Evaluate_UsesDetailAliasesForSeasonMatching()
    {
        var context = CreateContext(
            folderName: "咒术回战 第三季",
            searchSeed: "咒术回战 第三季",
            normalizedTitle: "咒术回战",
            baseTitle: "咒术回战",
            seasonNumber: 3);
        var result = _service.Evaluate(context, new MetadataAcquisitionResult(
            true,
            [
                CreateCandidate(
                    "season-3",
                    matchedTitle: "呪術廻戦 死滅回游 前編",
                    detailTitle: "呪術廻戦 死滅回游 前編",
                    bestRank: 1,
                    hitCount: 5,
                    detailAliases: ["咒术回战 第三季"]),
                CreateCandidate(
                    "season-2",
                    matchedTitle: "呪術廻戦 懐玉・玉折／渋谷事変",
                    detailTitle: "呪術廻戦 懐玉・玉折／渋谷事変",
                    bestRank: 1,
                    hitCount: 5,
                    detailAliases: ["咒术回战 第二季"])
            ],
            null));

        Assert.NotNull(result.BestCandidate);
        Assert.Equal("season-3", result.BestCandidate.Candidate.SourceId);
        Assert.Contains("season.exact-match", result.BestCandidate.Reasons);
    }

    private static MetadataPreparedContext CreateContext(
        string folderName,
        string searchSeed,
        string normalizedTitle,
        string baseTitle,
        int seasonNumber)
        => new(
            new MetadataRecord(
                "sample",
                string.Empty,
                folderName,
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
                folderName,
                null,
                [],
                0),
            new MetadataKeywordPlan(
                MetadataSeasonSupport.BuildPreferredSeasonKeyword(baseTitle, seasonNumber),
                MetadataSeasonSupport.BuildAlternateSeasonKeyword(baseTitle, seasonNumber),
                null,
                baseTitle,
                seasonNumber,
                null,
                false,
                false),
            searchSeed,
            normalizedTitle,
            [folderName, baseTitle],
            seasonNumber,
            null,
            false);

    private static MetadataAcquisitionCandidate CreateCandidate(
        string sourceId,
        string? matchedTitle,
        string? detailTitle,
        int bestRank,
        int hitCount,
        IReadOnlyList<string>? detailAliases = null)
        => new(
            sourceId,
            matchedTitle,
            detailTitle,
            null,
            hitCount,
            bestRank,
            new ProviderSubjectDetail(
                sourceId,
                detailTitle,
                detailTitle,
                null,
                null,
                null,
                null,
                null,
                null,
                detailAliases ?? [],
                [],
                "bangumi"));
}
