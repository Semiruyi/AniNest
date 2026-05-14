using AniNest.Features.Metadata;
using FluentAssertions;

namespace AniNest.Tests.View;

public class BangumiMetadataProviderTests
{
    [Fact]
    public void PickBestCandidate_PrefersSeasonMatchingCandidate()
    {
        var plan = new MetadataKeywordPlan(
            "Attack on Titan Season 3",
            "Attack on Titan Season 3",
            "Attack on Titan",
            "Attack on Titan",
            3,
            null,
            false,
            false);

        var season1 = CreateCandidate(1, "Attack on Titan", "进击的巨人", "2013-04-07");
        var season3 = CreateCandidate(2, "Attack on Titan Season 3", "进击的巨人 第三季", "2018-07-23");

        var winner = BangumiMetadataProvider.PickBestCandidate([season1, season3], plan);

        winner.Should().NotBeNull();
        winner!.Id.Should().Be(2);
    }

    [Fact]
    public void PickBestCandidate_PrefersMovieCandidate_ForMovieLikeQuery()
    {
        var plan = new MetadataKeywordPlan(
            "Jujutsu Kaisen Movie",
            null,
            null,
            "Jujutsu Kaisen",
            null,
            null,
            false,
            true);

        var series = CreateCandidate(1, "Jujutsu Kaisen", "咒术回战", "2020-10-03");
        var movie = CreateCandidate(2, "Jujutsu Kaisen 0 Movie", "咒术回战 剧场版", "2021-12-24");

        var winner = BangumiMetadataProvider.PickBestCandidate([series, movie], plan);

        winner.Should().NotBeNull();
        winner!.Id.Should().Be(2);
    }

    [Fact]
    public void PickBestCandidate_RejectsAmbiguousShortKeyword()
    {
        var plan = new MetadataKeywordPlan(
            "Fate",
            null,
            null,
            "Fate",
            null,
            null,
            true,
            false);

        var candidate = CreateCandidate(1, "Fate/stay night", "命运之夜", "2006-01-06");

        BangumiMetadataProvider.PickBestCandidate([candidate], plan).Should().BeNull();
    }

    [Fact]
    public void PickBestCandidate_AppliesYearPenalty()
    {
        var plan = new MetadataKeywordPlan(
            "Frieren Beyond Journey's End",
            null,
            null,
            "Frieren Beyond Journey's End",
            null,
            2023,
            false,
            false);

        var oldCandidate = CreateCandidate(1, "Frieren Beyond Journey's End", "葬送的芙莉莲", "2015-01-01");
        var newCandidate = CreateCandidate(2, "Frieren Beyond Journey's End", "葬送的芙莉莲", "2023-09-29");

        var winner = BangumiMetadataProvider.PickBestCandidate([oldCandidate, newCandidate], plan);

        winner.Should().NotBeNull();
        winner!.Id.Should().Be(2);
    }

    [Fact]
    public void PickBestCandidate_AcceptsRomanizedQuery_WhenChineseTitleMatchesWeakly()
    {
        var plan = new MetadataKeywordPlan(
            "bocchi the rock",
            null,
            null,
            "bocchi the rock",
            null,
            null,
            false,
            false);

        var target = CreateCandidate(1, "ぼっち・ざ・ろっく！", "孤独摇滚！", "2022-10-09");

        var winner = BangumiMetadataProvider.PickBestCandidate([target], plan);

        winner.Should().NotBeNull();
        winner!.Id.Should().Be(1);
    }

    [Fact]
    public void PickBestCandidate_InfersSeriesOrder_WhenSeasonNumberIsNotInCandidateTitle()
    {
        var plan = new MetadataKeywordPlan(
            "Jujutsu Kaisen Season 3",
            "Jujutsu Kaisen Season 3",
            "Jujutsu Kaisen",
            "Jujutsu Kaisen",
            3,
            null,
            false,
            false);

        var season1 = CreateCandidate(294993, "Jujutsu Kaisen", "咒术回战", "2020-10-02");
        var season2 = CreateCandidate(369304, "Jujutsu Kaisen Hidden Inventory / Shibuya Incident", "咒术回战 怀玉·玉折 / 涩谷事变", "2023-07-06");
        var season3 = CreateCandidate(472741, "Jujutsu Kaisen Culling Game", "咒术回战 死灭回游 前篇", "2026-01-08");

        var winner = BangumiMetadataProvider.PickBestCandidate([season1, season2, season3], plan);

        winner.Should().NotBeNull();
        winner!.Id.Should().Be(472741);
    }

    [Fact]
    public void PickBestCandidate_InfersSecondSeriesEntry_WhenOnlyFirstEntryHasExactTitle()
    {
        var plan = new MetadataKeywordPlan(
            "Jujutsu Kaisen Season 2",
            "Jujutsu Kaisen Season 2",
            "Jujutsu Kaisen",
            "Jujutsu Kaisen",
            2,
            null,
            false,
            false);

        var season1 = CreateCandidate(294993, "Jujutsu Kaisen", "咒术回战", "2020-10-02", "TV");
        var movie = CreateCandidate(331559, "Jujutsu Kaisen 0", "剧场版 咒术回战 0", "2021-12-24", "剧场版");
        var season2 = CreateCandidate(369304, "Jujutsu Kaisen Hidden Inventory / Shibuya Incident", "咒术回战 怀玉·玉折 / 涩谷事变", "2023-07-06", "TV");

        var winner = BangumiMetadataProvider.PickBestCandidate([season1, movie, season2], plan);

        winner.Should().NotBeNull();
        winner!.Id.Should().Be(369304);
    }

    [Fact]
    public void PickBestCandidate_PrefersMovieZeroCandidate_WhenQueryContainsZero()
    {
        var plan = new MetadataKeywordPlan(
            "Jujutsu Kaisen 0 Movie",
            null,
            null,
            "Jujutsu Kaisen 0",
            null,
            null,
            false,
            true);

        var series = CreateCandidate(294993, "Jujutsu Kaisen", "咒术回战", "2020-10-02");
        var recap = CreateCandidate(509599, "Jujutsu Kaisen Hidden Inventory Compilation", "咒术回战 怀玉·玉折 总集篇", "2025-05-30");
        var movie = CreateCandidate(331559, "Jujutsu Kaisen 0", "剧场版 咒术回战 0", "2021-12-24");

        var winner = BangumiMetadataProvider.PickBestCandidate([series, recap, movie], plan);

        winner.Should().NotBeNull();
        winner!.Id.Should().Be(331559);
    }

    private static BangumiMetadataProvider.BangumiSubjectItem CreateCandidate(int id, string name, string nameCn, string date, string platform = "TV")
        => new()
        {
            Id = id,
            Name = name,
            NameCn = nameCn,
            Date = date,
            Platform = platform
        };
}
