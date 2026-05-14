using AniNest.Features.Metadata;
using FluentAssertions;

namespace AniNest.Tests.View;

public class MetadataMatcherTests
{
    private readonly MetadataMatcher _matcher = new();

    [Fact]
    public void BuildKeywordPlan_CleansFansubNoise_AndPreservesTitle()
    {
        var plan = _matcher.BuildKeywordPlan(
            new MetadataFolderRef("/library/Attack", "[GM-Team][Attack on Titan][01-25][1080p]", []));

        plan.BaseTitle.Should().Be("Attack on Titan");
        plan.PrimaryKeyword.Should().Be("Attack on Titan");
    }

    [Fact]
    public void BuildKeywordPlan_DetectsSeason_AndProducesSeasonAwareKeyword()
    {
        var plan = _matcher.BuildKeywordPlan(
            new MetadataFolderRef("/library/Bocchi", "Bocchi the Rock! Season 2 [1080p][AAC]", []));

        plan.SeasonNumber.Should().Be(2);
        plan.SeasonAwareKeyword.Should().Be("Bocchi the Rock! Season 2");
        plan.PrimaryKeyword.Should().Be("Bocchi the Rock! Season 2");
    }

    [Fact]
    public void BuildKeywordPlan_DetectsChineseSeason()
    {
        var plan = _matcher.BuildKeywordPlan(
            new MetadataFolderRef("/library/JJK3", "咒术回战 第三季", []));

        plan.BaseTitle.Should().Be("咒术回战");
        plan.SeasonNumber.Should().Be(3);
        plan.SeasonAwareKeyword.Should().Be("咒术回战 Season 3");
    }

    [Fact]
    public void BuildKeywordPlan_DetectsMovieLikeTitle()
    {
        var plan = _matcher.BuildKeywordPlan(
            new MetadataFolderRef("/library/JJKMovie", "咒术回战 剧场版", []));

        plan.BaseTitle.Should().Be("咒术回战");
        plan.IsMovieLike.Should().BeTrue();
        plan.PrimaryKeyword.Should().Be("咒术回战 Movie");
    }

    [Fact]
    public void BuildKeywordPlans_CleansReleaseNoise_FromMovieFileName()
    {
        var plans = _matcher.BuildKeywordPlans(
            new MetadataFolderRef(
                "/library/JJKMovie",
                "咒术回战 剧场版",
                ["/library/JJKMovie/[orion origin] Jujutsu Kaisen 0 [The Movie] [BDRip 1080p] [H265 AAC] [CHS].mp4"]));

        plans.Should().Contain(plan => plan.BaseTitle == "Jujutsu Kaisen 0" && plan.PrimaryKeyword == "Jujutsu Kaisen 0 Movie");
    }

    [Fact]
    public void BuildKeywordPlan_ExtractsYearHint_AndSimplifiedKeyword()
    {
        var plan = _matcher.BuildKeywordPlan(
            new MetadataFolderRef("/library/Frieren", "Frieren: Beyond Journey's End 2023 BDRip", []));

        plan.YearHint.Should().Be(2023);
        plan.BaseTitle.Should().Be("Frieren: Beyond Journey's End");
    }

    [Fact]
    public void BuildKeywordPlan_UsesParentFallback_WhenFolderNameIsMeaningless()
    {
        var plan = _matcher.BuildKeywordPlan(
            new MetadataFolderRef("/library/Jujutsu Kaisen/Season 1", "Season 1", []));

        plan.BaseTitle.Should().Be("Jujutsu Kaisen");
    }

    [Fact]
    public void BuildKeywordPlan_UsesFileFallback_WhenFolderAndParentAreMeaningless()
    {
        var plan = _matcher.BuildKeywordPlan(
            new MetadataFolderRef("/library/Anime/01-12", "01-12", ["/library/Anime/01-12/CLANNAD 01.mkv"]));

        plan.BaseTitle.Should().Be("CLANNAD");
    }

    [Fact]
    public void BuildKeywordPlans_IncludesRomanizedFileFallback_ForChineseFolder()
    {
        var plans = _matcher.BuildKeywordPlans(
            new MetadataFolderRef(
                "/library/【我推的孩子】 第三季",
                "【我推的孩子】 第三季",
                ["/library/【我推的孩子】 第三季/Oshi no Ko S3 - 01.mkv"]));

        plans.Should().Contain(plan => plan.BaseTitle == "我推的孩子" && plan.SeasonNumber == 3);
        plans.Should().Contain(plan => plan.BaseTitle == "Oshi no Ko" && plan.SeasonNumber == 3);
    }

    [Fact]
    public void BuildKeywordPlans_CleansRomanizedSeasonFileFallback()
    {
        var plans = _matcher.BuildKeywordPlans(
            new MetadataFolderRef(
                "/library/咒术回战 第二季",
                "咒术回战 第二季",
                ["/library/咒术回战 第二季/[orion origin] Jujutsu Kaisen S2 [25v2] [1080p] [H265 AAC] [CHS＆JPN].mp4"]));

        plans.Should().Contain(plan => plan.BaseTitle == "Jujutsu Kaisen" && plan.SeasonNumber == 2);
    }

    [Fact]
    public void BuildKeywordPlan_FlagsAmbiguousShortKeyword()
    {
        var plan = _matcher.BuildKeywordPlan(
            new MetadataFolderRef("/library/Fate", "Fate", []));

        plan.IsAmbiguousShortKeyword.Should().BeTrue();
    }
}
