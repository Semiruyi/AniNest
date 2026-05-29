namespace AniNest.Host.Modules;

internal static class MetadataSearchKeywordBuilder
{
    public static IReadOnlyList<string> Build(MetadataPreparedContext context)
    {
        var keywords = new List<string>(8);

        AddKeyword(keywords, context.SearchSeed);
        AddKeyword(keywords, context.KeywordPlan.PrimaryKeyword);
        AddKeyword(keywords, context.KeywordPlan.SeasonAwareKeyword);

        if (context.SeasonNumber is > 1)
        {
            foreach (var variant in MetadataSeasonSupport.BuildSeasonKeywordVariants(
                         context.KeywordPlan.BaseTitle,
                         context.SeasonNumber.Value))
            {
                AddKeyword(keywords, variant);
            }
        }

        AddKeyword(keywords, context.KeywordPlan.SimplifiedKeyword);
        AddKeyword(keywords, context.NormalizedTitle);

        foreach (var alias in context.Aliases.Take(3))
            AddKeyword(keywords, alias);

        return keywords;
    }

    private static void AddKeyword(ICollection<string> keywords, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (keywords.Contains(value, StringComparer.OrdinalIgnoreCase))
            return;

        keywords.Add(value);
    }
}
