namespace AniNest.Host.Modules;

internal sealed class MetadataConfidenceService : IMetadataConfidenceService
{
    private static readonly string[] SeasonPatterns =
    [
        @"season\s*(\d+)",
        @"s(\d+)",
        @"\u7b2c\s*(\d+)\s*[\u5b63\u671f\u90e8]",
        @"(\d+)\s*[\u5b63\u671f\u90e8]"
    ];

    public MetadataConfidenceResult Evaluate(
        MetadataPreparedContext context,
        MetadataAcquisitionResult acquisition)
    {
        var candidates = acquisition.Candidates
            .Select(candidate => ScoreCandidate(context, candidate))
            .OrderByDescending(candidate => candidate.Score)
            .ToArray();

        return new MetadataConfidenceResult(candidates.FirstOrDefault(), candidates);
    }

    private static MetadataConfidenceCandidate ScoreCandidate(
        MetadataPreparedContext context,
        MetadataAcquisitionCandidate candidate)
    {
        var reasons = new List<string>();
        var score = 0d;
        var candidateTitles = new[]
        {
            candidate.MatchedTitle,
            candidate.OriginalTitle,
            candidate.Detail?.Title,
            candidate.Detail?.OriginalTitle
        }
        .Where(title => !string.IsNullOrWhiteSpace(title))
        .Cast<string>()
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        if (!string.IsNullOrWhiteSpace(candidate.Detail?.Title))
        {
            score += 0.25;
            reasons.Add("provider.detail.title");
        }

        if (!string.IsNullOrWhiteSpace(candidate.Detail?.OriginalTitle))
        {
            score += 0.15;
            reasons.Add("provider.detail.original-title");
        }

        if (candidate.BestRank == 1)
        {
            score += 0.15;
            reasons.Add("search.top-rank");
        }
        else if (candidate.BestRank == 2)
        {
            score += 0.05;
            reasons.Add("search.high-rank");
        }

        if (candidate.HitCount >= 3)
        {
            score += 0.15;
            reasons.Add("search.multi-hit");
        }
        else if (candidate.HitCount == 2)
        {
            score += 0.08;
            reasons.Add("search.repeat-hit");
        }

        if (context.Aliases.Any(alias => candidateTitles.Any(title => TitleEquals(alias, title))))
        {
            score += 0.3;
            reasons.Add("alias.exact-match");
        }

        if (context.Aliases.Any(alias => candidateTitles.Any(title => TitleContains(alias, title))))
        {
            score += 0.15;
            reasons.Add("alias.contains-match");
        }

        if (candidateTitles.Any(title => TitleEquals(context.NormalizedTitle, title)))
        {
            score += 0.2;
            reasons.Add("title.exact-match");
        }

        if (candidateTitles.Any(title => TitleContains(context.KeywordPlan.BaseTitle, title)))
        {
            score += 0.1;
            reasons.Add("title.contains-base");
        }

        var subtitleSignals = ExtractSubtitleSignals(context);
        var subtitleMatchCount = subtitleSignals.Count(signal =>
            candidateTitles.Any(title => TitleContains(signal, title)));
        if (subtitleMatchCount > 0)
        {
            score += Math.Min(0.2, subtitleMatchCount * 0.1);
            reasons.Add("subtitle.match");
        }

        var detailYear = candidate.Detail?.Year ?? candidate.Year;
        if (context.YearHint.HasValue && detailYear.HasValue)
        {
            var delta = Math.Abs(context.YearHint.Value - detailYear.Value);
            if (delta == 0)
            {
                score += 0.1;
                reasons.Add("year.exact-match");
            }
            else if (delta >= 3)
            {
                score -= 0.15;
                reasons.Add("year.conflict");
            }
        }

        var candidateSeason = DetectSeason(candidateTitles);
        if (context.SeasonNumber.HasValue && candidateSeason.HasValue)
        {
            if (context.SeasonNumber.Value == candidateSeason.Value)
            {
                score += 0.25;
                reasons.Add("season.exact-match");
            }
            else
            {
                score -= 0.2;
                reasons.Add("season.conflict");
            }
        }

        var candidateMovieLike = candidateTitles.Any(IsMovieLikeTitle);
        if (context.IsMovieLike && candidateMovieLike)
        {
            score += 0.2;
            reasons.Add("format.movie-match");
        }
        else if (context.IsMovieLike)
        {
            score += 0.05;
            reasons.Add("format.movie-like-input");
        }
        else if (candidateMovieLike)
        {
            score -= 0.1;
            reasons.Add("format.movie-conflict");
        }

        var candidateSpecialLike = candidateTitles.Any(IsSpecialLikeTitle);
        var contextSpecialLike = IsSpecialLikeInput(context);
        if (contextSpecialLike && candidateSpecialLike)
        {
            score += 0.15;
            reasons.Add("format.special-match");
        }
        else if (!contextSpecialLike && candidateSpecialLike)
        {
            score -= 0.15;
            reasons.Add("format.special-conflict");
        }

        var level = score switch
        {
            >= 0.8 => MetadataConfidenceLevel.High,
            >= 0.5 => MetadataConfidenceLevel.Medium,
            > 0 => MetadataConfidenceLevel.Low,
            _ => MetadataConfidenceLevel.None
        };

        return new MetadataConfidenceCandidate(candidate, score, level, reasons);
    }

    private static bool TitleEquals(string? left, string? right)
        => NormalizeTitle(left) == NormalizeTitle(right);

    private static bool TitleContains(string? left, string? right)
    {
        var normalizedLeft = NormalizeTitle(left);
        var normalizedRight = NormalizeTitle(right);
        return !string.IsNullOrWhiteSpace(normalizedLeft) &&
               !string.IsNullOrWhiteSpace(normalizedRight) &&
               (normalizedLeft.Contains(normalizedRight, StringComparison.OrdinalIgnoreCase) ||
                normalizedRight.Contains(normalizedLeft, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeTitle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = value
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ')
            .ToArray();

        return string.Join(
            ' ',
            new string(chars)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static int? DetectSeason(IEnumerable<string> titles)
    {
        foreach (var title in titles)
        {
            var normalized = title.ToLowerInvariant();
            foreach (var pattern in SeasonPatterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    normalized,
                    pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var season))
                    return season;
            }
        }

        return null;
    }

    private static bool IsMovieLikeTitle(string title)
    {
        var normalized = title.ToLowerInvariant();
        return normalized.Contains("movie", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("\u5267\u573a\u7248", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("\u5287\u5834\u7248", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("\u603b\u96c6\u7bc7", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("\u7dcf\u96c6\u7de8", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSpecialLikeTitle(string title)
    {
        var normalized = title.ToLowerInvariant();
        return normalized.Contains("ova", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("oad", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("special", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("sp", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("\u7279\u522b\u7bc7", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("\u7279\u5178", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("\u756a\u5916", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("\u603b\u96c6\u7bc7", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("\u7dcf\u96c6\u7de8", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSpecialLikeInput(MetadataPreparedContext context)
    {
        return context.Aliases.Any(IsSpecialLikeTitle) ||
               IsSpecialLikeTitle(context.NormalizedTitle) ||
               IsSpecialLikeTitle(context.KeywordPlan.PrimaryKeyword);
    }

    private static IReadOnlyList<string> ExtractSubtitleSignals(MetadataPreparedContext context)
    {
        var signals = new List<string>();

        AddSignal(signals, context.KeywordPlan.SimplifiedKeyword);

        foreach (var alias in context.Aliases)
        {
            var normalizedAlias = NormalizeTitle(alias);
            var normalizedBase = NormalizeTitle(context.KeywordPlan.BaseTitle);
            if (string.IsNullOrWhiteSpace(normalizedAlias) || string.IsNullOrWhiteSpace(normalizedBase))
                continue;

            var remainder = normalizedAlias.Replace(normalizedBase, string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            AddSignal(signals, remainder);
        }

        var baseTokens = NormalizeTitle(context.KeywordPlan.BaseTitle)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in NormalizeTitle(context.NormalizedTitle)
                     .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (baseTokens.Contains(token, StringComparer.OrdinalIgnoreCase))
                continue;

            AddSignal(signals, token);
        }

        return signals;
    }

    private static void AddSignal(ICollection<string> signals, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (value.Length < 3)
            return;

        if (signals.Contains(value, StringComparer.OrdinalIgnoreCase))
            return;

        signals.Add(value);
    }
}
