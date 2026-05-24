using System.Text.RegularExpressions;

namespace AniNest.Host.Modules;

internal static partial class MetadataPreparationAnalyzer
{
    private static readonly string[] GenericFolderNames =
    [
        "library",
        "collection",
        "anime",
        "animation",
        "video",
        "videos"
    ];

    private static readonly string[] ReleaseNoiseTokens =
    [
        "nc-raws",
        "lolihouse",
        "orion origin",
        "gm-team",
        "sweetsub",
        "raws",
        "feibanyama",
        "kitaujisub",
        "kitauji",
        "nekomoe",
        "kissaten",
        "jpsc",
        "varyg",
        "iqiyi",
        "youku",
        "bilibili",
        "cr",
        "multi-audio",
        "multi-subs"
    ];

    public static MetadataPreparationAnalysis Analyze(
        string folderName,
        string? parentName,
        IReadOnlyList<string> videoFiles)
    {
        var candidates = BuildInputCandidates(folderName, parentName, videoFiles);
        var bestCandidate = candidates
            .Select(candidate => AnalyzeCandidate(candidate.value, candidate.priority))
            .OrderByDescending(result => result.Score)
            .FirstOrDefault()
            ?? AnalyzeCandidate(folderName, priority: 100);

        var aliases = candidates
            .Select(candidate => AnalyzeCandidate(candidate.value, candidate.priority))
            .Select(result => result.BaseTitle)
            .Where(IsUsefulAlias)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var baseTitle = CleanupTitle(bestCandidate.BaseTitle);
        var simplifiedKeyword = BuildSimplifiedKeyword(baseTitle);
        var primaryKeyword = BuildPrimaryKeyword(bestCandidate with { BaseTitle = baseTitle });
        var seasonAwareKeyword = bestCandidate.SeasonNumber is > 1
            ? $"{baseTitle} Season {bestCandidate.SeasonNumber}"
            : null;

        return new MetadataPreparationAnalysis(
            bestCandidate.RawInput,
            baseTitle,
            aliases,
            new AniNest.Application.Metadata.MetadataKeywordPlan(
                primaryKeyword,
                seasonAwareKeyword,
                simplifiedKeyword,
                baseTitle,
                bestCandidate.SeasonNumber,
                bestCandidate.YearHint,
                IsAmbiguousShortKeyword(baseTitle),
                bestCandidate.IsMovieLike),
            bestCandidate.SeasonNumber,
            bestCandidate.YearHint,
            bestCandidate.IsMovieLike);
    }

    private static string BuildPrimaryKeyword(MetadataPreparationCandidate candidate)
    {
        if (candidate.IsMovieLike)
            return candidate.BaseTitle.Contains("movie", StringComparison.OrdinalIgnoreCase)
                ? candidate.BaseTitle
                : $"{candidate.BaseTitle} Movie";

        if (candidate.SeasonNumber is > 1)
            return $"{candidate.BaseTitle} Season {candidate.SeasonNumber}";

        return candidate.BaseTitle;
    }

    private static string? BuildSimplifiedKeyword(string baseTitle)
    {
        var simplified = baseTitle;
        simplified = ArcSuffixRegex().Replace(simplified, " ");
        simplified = CleanupTitle(simplified);

        if (string.IsNullOrWhiteSpace(simplified) ||
            string.Equals(simplified, baseTitle, StringComparison.OrdinalIgnoreCase))
            return null;

        return simplified;
    }

    private static IReadOnlyList<(string value, int priority)> BuildInputCandidates(
        string folderName,
        string? parentName,
        IReadOnlyList<string> videoFiles)
    {
        var values = new List<(string value, int priority)>
        {
            (folderName, 100)
        };

        if (!string.IsNullOrWhiteSpace(parentName))
            values.Add((parentName, 60));

        values.AddRange(videoFiles
            .Take(3)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => (item!, 20)));

        return values;
    }

    private static MetadataPreparationCandidate AnalyzeCandidate(string rawInput, int priority)
    {
        var normalized = NormalizeWrappersAndSeparators(rawInput);
        normalized = StripTechnicalNoise(normalized);
        normalized = StripReleaseNoise(normalized);
        var yearHint = ExtractYearHint(normalized, out normalized);
        var seasonNumber = ExtractSeasonNumber(normalized, out normalized);
        var isMovieLike = ExtractMovieHint(normalized, out normalized);
        normalized = CleanupTitle(normalized);

        if (IsMeaningless(normalized))
            normalized = CleanupTitle(NormalizeWrappersAndSeparators(rawInput));

        var cleaned = string.IsNullOrWhiteSpace(normalized) ? rawInput.Trim() : normalized;
        cleaned = CleanupTitle(cleaned);

        return new MetadataPreparationCandidate(
            rawInput,
            cleaned,
            seasonNumber,
            yearHint,
            isMovieLike,
            Score(cleaned, rawInput, priority, seasonNumber, isMovieLike));
    }

    private static string NormalizeWrappersAndSeparators(string value)
    {
        var normalized = value.Normalize();
        normalized = normalized
            .Replace('\u3010', '[')
            .Replace('\u3011', ']')
            .Replace('\uFF08', '(')
            .Replace('\uFF09', ')')
            .Replace('\u3000', ' ')
            .Replace('_', ' ')
            .Replace('.', ' ')
            .Replace('\uFF0B', '+')
            .Replace('\uFF1A', ':')
            .Replace('\u2014', '-')
            .Replace('\u2013', '-')
            .Replace('\uFF06', ' ');

        normalized = Regex.Replace(normalized, @"[\[\]\(\)<>]", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        return normalized;
    }

    private static string StripTechnicalNoise(string value)
    {
        var result = ResolutionRegex().Replace(value, " ");
        result = CodecRegex().Replace(result, " ");
        result = SourceRegex().Replace(result, " ");
        result = AudioRegex().Replace(result, " ");
        result = LanguageRegex().Replace(result, " ");
        result = SubtitleNoiseRegex().Replace(result, " ");
        result = VersionRegex().Replace(result, " ");
        result = EpisodePatternRegex().Replace(result, " ");
        result = EpisodeBatchRegex().Replace(result, " ");
        result = PlatformNoiseRegex().Replace(result, " ");
        return CleanupTitle(result);
    }

    private static string StripReleaseNoise(string value)
    {
        var result = value;
        foreach (var token in ReleaseNoiseTokens)
            result = Regex.Replace(result, $@"\b{Regex.Escape(token)}\b", " ", RegexOptions.IgnoreCase);

        result = TrailingRecruitmentRegex().Replace(result, " ");
        return CleanupTitle(result);
    }

    private static int? ExtractYearHint(string value, out string cleaned)
    {
        var match = YearRegex().Match(value);
        cleaned = value;
        if (!match.Success || !int.TryParse(match.Value, out var year))
            return null;

        cleaned = CleanupTitle(YearRegex().Replace(value, " "));
        return year;
    }

    private static int? ExtractSeasonNumber(string value, out string cleaned)
    {
        cleaned = value;

        var episodeSeason = EpisodeSeasonRegex().Match(value);
        if (episodeSeason.Success)
        {
            var token = episodeSeason.Groups["season"].Value;
            cleaned = CleanupTitle(EpisodeSeasonRegex().Replace(value, " "));
            return ParseSeasonToken(token);
        }

        var match = SeasonRegex().Match(value);
        if (match.Success)
        {
            var token = match.Groups["season"].Value;
            cleaned = CleanupTitle(SeasonRegex().Replace(value, " "));
            return ParseSeasonToken(token);
        }

        var chinese = ChineseSeasonRegex().Match(value);
        if (chinese.Success)
        {
            var token = chinese.Groups["season"].Value;
            cleaned = CleanupTitle(ChineseSeasonRegex().Replace(value, " "));
            return ParseChineseSeason(token);
        }

        return null;
    }

    private static bool ExtractMovieHint(string value, out string cleaned)
    {
        var isMovieLike = MovieRegex().IsMatch(value);
        cleaned = isMovieLike ? CleanupTitle(MovieRegex().Replace(value, " ")) : value;
        return isMovieLike;
    }

    private static string CleanupTitle(string value)
    {
        var result = value;
        result = Regex.Replace(result, @"\s+", " ").Trim();
        result = Regex.Replace(result, @"(^[-: ]+|[-: ]+$)", string.Empty);
        result = Regex.Replace(result, @"\s*-\s*", " - ");
        result = Regex.Replace(result, @"\s+", " ").Trim();
        result = Regex.Replace(result, @"(?:\s-\s){2,}", " - ");
        result = DanglingDashRegex().Replace(result, " ");
        result = Regex.Replace(result, @"\s+", " ").Trim();
        return result.Trim();
    }

    private static bool IsMeaningless(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var folded = FoldForComparison(value);
        if (folded.Length == 0)
            return true;
        if (folded.All(char.IsDigit))
            return true;
        if (GenericFolderNames.Contains(folded, StringComparer.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool IsUsefulAlias(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        if (IsMeaningless(value))
            return false;

        var folded = FoldForComparison(value);
        if (folded.Length <= 2)
            return false;
        if (folded.All(char.IsDigit))
            return false;
        if (EpisodeTitleLikeRegex().IsMatch(value))
            return false;
        if (SuspiciousNoiseLeftRegex().IsMatch(value))
            return false;

        return true;
    }

    private static bool IsAmbiguousShortKeyword(string value)
    {
        var folded = FoldForComparison(value);
        var cjkCount = value.Count(IsCjk);
        if (cjkCount >= 2)
            return cjkCount < 3;

        return folded.Length < 6;
    }

    private static int Score(string cleaned, string raw, int priority, int? seasonNumber, bool isMovieLike)
    {
        var score = string.IsNullOrWhiteSpace(cleaned) ? 0 : cleaned.Length;
        score += cleaned.Count(IsCjk) * 2;
        score += priority;
        if (seasonNumber is not null)
            score += 8;
        if (isMovieLike)
            score += 6;
        if (raw.Length != cleaned.Length)
            score += 2;
        if (SuspiciousNoiseLeftRegex().IsMatch(cleaned))
            score -= 40;
        if (EpisodePatternRegex().IsMatch(cleaned))
            score -= 25;
        if (foldedTokenCount(cleaned) <= 1)
            score -= 5;
        return score;

        static int foldedTokenCount(string value)
            => value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    private static string FoldForComparison(string value)
        => new(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private static bool IsCjk(char ch)
        => ch is >= '\u4e00' and <= '\u9fff';

    private static int? ParseSeasonToken(string token)
    {
        if (int.TryParse(token, out var numeric))
            return numeric;

        return token.ToUpperInvariant() switch
        {
            "II" => 2,
            "III" => 3,
            "IV" => 4,
            "V" => 5,
            "VI" => 6,
            _ => null
        };
    }

    private static int? ParseChineseSeason(string token)
        => token switch
        {
            "\u4e00" => 1,
            "\u4e8c" => 2,
            "\u4e09" => 3,
            "\u56db" => 4,
            "\u4e94" => 5,
            "\u516d" => 6,
            _ => null
        };

    [GeneratedRegex(@"\b(2160p|1080p|720p|4k)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ResolutionRegex();

    [GeneratedRegex(@"\b(x264|x265|h264|h265|hevc|avc|10bit)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CodecRegex();

    [GeneratedRegex(@"\b(bdrip|bluray|web-?dl|webrip|hdtv|remux)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SourceRegex();

    [GeneratedRegex(@"\b(aac|aac2|flac|ac3|dts)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AudioRegex();

    [GeneratedRegex(@"\b(chs|cht|jpn|eng|gb|big5)\b|[\u7e41\u7b80][\u4e2d\u65e5\u82f1][\u53cc]?[\u8bed\u5b57\u5e55]*|\u5b57\u5e55|\u5185\u5c01|\u5916\u6302", RegexOptions.IgnoreCase)]
    private static partial Regex LanguageRegex();

    [GeneratedRegex(@"\b(srtx?\d*|assx?\d*|multi-?audio|multi-?subs?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SubtitleNoiseRegex();

    [GeneratedRegex(@"\b(v\d+|\d{1,2}v\d+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex VersionRegex();

    [GeneratedRegex(@"\b(s(?<season>\d{1,2})e\d{1,3}|e\d{1,3}|ep\d{1,3}|episode\s*\d{1,3})\b", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodePatternRegex();

    [GeneratedRegex(@"\b(\d{1,2}\s*-\s*\d{1,2}|\d{1,3})\b", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodeBatchRegex();

    [GeneratedRegex(@"\b(iqiyi|youku|cr)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PlatformNoiseRegex();

    [GeneratedRegex(@"\b(19|20)\d{2}\b", RegexOptions.IgnoreCase)]
    private static partial Regex YearRegex();

    [GeneratedRegex(@"\bs(?<season>\d{1,2})e\d{1,3}\b", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodeSeasonRegex();

    [GeneratedRegex(@"\b(?:season|s|part)\s*(?<season>\d+|ii|iii|iv|v|vi)\b|\b(?<season>\d+)(?:st|nd|rd|th)\s+season\b", RegexOptions.IgnoreCase)]
    private static partial Regex SeasonRegex();

    [GeneratedRegex(@"\u7b2c\s*(?<season>[\u4e00\u4e8c\u4e09\u56db\u4e94\u516d])\s*\u5b63", RegexOptions.IgnoreCase)]
    private static partial Regex ChineseSeasonRegex();

    [GeneratedRegex(@"\b(movie|the movie)\b|\u5267\u573a\u7248|\u96fb\u5f71|\u7535\u5f71", RegexOptions.IgnoreCase)]
    private static partial Regex MovieRegex();

    [GeneratedRegex(@"\u4e27\u5931\u7bc7|\u79d1\u5b66\u4e0e\u672a\u6765|\u7bc7$", RegexOptions.IgnoreCase)]
    private static partial Regex ArcSuffixRegex();

    [GeneratedRegex(@"\u62db\u52df\u7ffb\u8bd1|\u62db\u52df", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingRecruitmentRegex();

    [GeneratedRegex(@"\b(feibanyama|kitaujisub|nekomoe|kissaten|jpsc|varyg|iqiyi|multi-audio|multi-subs|srtx?\d*|aac2|cr)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SuspiciousNoiseLeftRegex();

    [GeneratedRegex(@"\b(execution|one more time|about the culling game|episode|ep|part\s*\d+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodeTitleLikeRegex();

    [GeneratedRegex(@"\s-\s(?=-|$)|(?<=^|-)\s-\s", RegexOptions.IgnoreCase)]
    private static partial Regex DanglingDashRegex();
}
