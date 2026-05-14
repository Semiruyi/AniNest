using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AniNest.Features.Metadata;

public sealed record MetadataKeywordPlan(
    string PrimaryKeyword,
    string? SeasonAwareKeyword,
    string? SimplifiedKeyword,
    string BaseTitle,
    int? SeasonNumber,
    int? YearHint,
    bool IsAmbiguousShortKeyword,
    bool IsMovieLike);

public sealed class MetadataMatcher
{
    private static readonly Regex MultiSpaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex ReleaseGroupRegex = new(@"(?i)\b(?:gm[\s-]?team|nc[\s-]?raws|lilihouse|orion\s+origin|nekomoe\s+kissaten|kitaujisub|feibanyama)\b", RegexOptions.Compiled);
    private static readonly Regex EpisodeRangeRegex = new(@"(?i)\b(?:e?p?)?\d{1,3}\s*[-~]\s*(?:e?p?)?\d{1,3}\b", RegexOptions.Compiled);
    private static readonly Regex ResolutionRegex = new(@"(?i)\b(?:480|720|1080|2160)p\b|\b4k\b", RegexOptions.Compiled);
    private static readonly Regex CodecRegex = new(@"(?i)\b(?:x264|x265|h264|h265|hevc|avc|10bit|aac|flac|ac3|dts|webrip|web-dl|bdrip|bluray|hdtv|remux)\b", RegexOptions.Compiled);
    private static readonly Regex VersionTokenRegex = new(@"(?i)\b(?:\d{1,3}v\d+|v\d+)\b", RegexOptions.Compiled);
    private static readonly Regex LanguageTokenRegex = new(@"(?i)\b(?:chs|cht|jpn|jap|eng|gb|big5|chs&jpn|cht&jpn|srtx?\d*)\b", RegexOptions.Compiled);
    private static readonly Regex ExtensionRegex = new(@"(?i)\.(mkv|mp4|avi|wmv|mov)$", RegexOptions.Compiled);
    private static readonly Regex EpisodeTokenRegex = new(@"(?i)\b(?:s\d{1,2}e\d{1,3}|ep?\s*\d{1,3})\b.*$", RegexOptions.Compiled);
    private static readonly Regex TrailingNumericTokenRegex = new(@"\s+(\d{1,3})$", RegexOptions.Compiled);
    private static readonly Regex TrailingStopWordRegex = new(@"(?i)\bthe\b$", RegexOptions.Compiled);
    private static readonly Regex YearRegex = new(@"\b(19|20)\d{2}\b", RegexOptions.Compiled);
    private static readonly Regex SeasonWordRegex = new(@"(?i)\b(?:season|s)\s*0*(\d{1,2})\b", RegexOptions.Compiled);
    private static readonly Regex OrdinalSeasonRegex = new(@"(?i)\b(\d{1,2})(?:st|nd|rd|th)\s+season\b", RegexOptions.Compiled);
    private static readonly Regex ChineseSeasonRegex = new(@"第\s*([0-9一二三四五六七八九十两]{1,3})\s*[季期篇]", RegexOptions.Compiled);
    private static readonly Regex RomanSuffixRegex = new(@"\b(II|III|IV|V)\b", RegexOptions.Compiled);
    private static readonly Regex MovieRegex = new(@"(?i)\bmovie\b|剧场版|劇場版|电影|電影", RegexOptions.Compiled);
    private static readonly Regex ArcSuffixRegex = new(@"(?i)\b(?:final\s+chapters?|final\s+season|entertainment\s+district\s+arc|arc)\b.*$", RegexOptions.Compiled);

    public MetadataKeywordPlan BuildKeywordPlan(MetadataFolderRef folder)
        => BuildKeywordPlans(folder).FirstOrDefault()
            ?? new MetadataKeywordPlan(string.Empty, null, null, string.Empty, null, null, true, false);

    public IReadOnlyList<MetadataKeywordPlan> BuildKeywordPlans(MetadataFolderRef folder)
    {
        var plans = new List<MetadataKeywordPlan>();
        foreach (var seed in EnumerateSourceTexts(folder))
        {
            var plan = BuildKeywordPlan(seed);
            if (string.IsNullOrWhiteSpace(plan.PrimaryKeyword))
                continue;

            if (!plans.Any(existing => string.Equals(existing.PrimaryKeyword, plan.PrimaryKeyword, StringComparison.OrdinalIgnoreCase)))
                plans.Add(plan);
        }

        return plans;
    }

    public IReadOnlyList<string> BuildSearchKeywords(MetadataKeywordPlan plan)
    {
        var ordered = new List<string>();
        AddDistinct(ordered, plan.PrimaryKeyword);
        AddDistinct(ordered, plan.SeasonAwareKeyword);

        if (plan.IsMovieLike)
            AddDistinct(ordered, $"{plan.BaseTitle} Movie");

        AddDistinct(ordered, plan.BaseTitle);
        AddDistinct(ordered, plan.SimplifiedKeyword);
        return ordered;
    }

    public MetadataKeywordPlan BuildKeywordPlan(string input)
    {
        string cleaned = Clean(input, out var seasonNumber, out var yearHint, out var isMovieLike);
        string baseTitle = cleaned;

        if (IsMeaningless(baseTitle))
            return new MetadataKeywordPlan(string.Empty, null, null, string.Empty, seasonNumber, yearHint, true, isMovieLike);

        string? seasonAwareKeyword = seasonNumber.HasValue
            ? $"{baseTitle} Season {seasonNumber.Value}"
            : null;

        string primaryKeyword = seasonAwareKeyword ?? baseTitle;
        if (isMovieLike)
            primaryKeyword = $"{baseTitle} Movie";

        string? simplifiedKeyword = BuildSimplifiedKeyword(baseTitle, seasonNumber);
        bool ambiguous = IsAmbiguousShortKeyword(baseTitle);

        return new MetadataKeywordPlan(
            primaryKeyword,
            seasonAwareKeyword,
            simplifiedKeyword,
            baseTitle,
            seasonNumber,
            yearHint,
            ambiguous,
            isMovieLike);
    }

    internal static double CalculateSimilarity(string left, string right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            return 0;

        if (string.Equals(left, right, StringComparison.Ordinal))
            return 1;

        int distance = LevenshteinDistance(left, right);
        int maxLength = Math.Max(left.Length, right.Length);
        return maxLength == 0 ? 1 : 1d - (double)distance / maxLength;
    }

    internal static string NormalizeForComparison(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char ch in value.Normalize(NormalizationForm.FormKC).ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
                builder.Append(ch);
        }

        return builder.ToString();
    }

    internal static int? ExtractSeasonHint(string value)
        => TryExtractSeason(value.Normalize(NormalizationForm.FormKC));

    internal static bool ContainsMovieMarker(string value)
        => MovieRegex.IsMatch(value.Normalize(NormalizationForm.FormKC));

    internal static bool IsRomanizedKeyword(string value)
    {
        string normalized = NormalizeForComparison(value);
        return normalized.Length >= 8 && normalized.All(ch => ch <= 127);
    }

    private static IEnumerable<string> EnumerateSourceTexts(MetadataFolderRef folder)
    {
        string folderName = folder.FolderName.Trim();
        if (!string.IsNullOrWhiteSpace(folderName))
            yield return folderName;

        string parentName = Path.GetFileName(Path.GetDirectoryName(folder.FolderPath) ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(parentName) &&
            !string.Equals(parentName, folderName, StringComparison.OrdinalIgnoreCase))
        {
            yield return parentName;
        }

        string? firstVideo = folder.VideoFiles.FirstOrDefault();
        string? firstVideoName = Path.GetFileNameWithoutExtension(firstVideo);
        if (!string.IsNullOrWhiteSpace(firstVideoName) &&
            !string.Equals(firstVideoName, folderName, StringComparison.OrdinalIgnoreCase))
        {
            yield return firstVideoName;
        }
    }

    private static string Clean(
        string input,
        out int? seasonNumber,
        out int? yearHint,
        out bool isMovieLike)
    {
        string value = input.Normalize(NormalizationForm.FormKC);
        value = value.Replace('_', ' ')
            .Replace('.', ' ')
            .Replace('[', ' ')
            .Replace(']', ' ')
            .Replace('(', ' ')
            .Replace(')', ' ')
            .Replace('<', ' ')
            .Replace('>', ' ')
            .Replace('【', ' ')
            .Replace('】', ' ')
            .Replace('「', ' ')
            .Replace('」', ' ')
            .Replace('～', ' ');

        isMovieLike = MovieRegex.IsMatch(value);
        seasonNumber = TryExtractSeason(value);
        yearHint = TryExtractYear(value);

        value = ExtensionRegex.Replace(value, " ");
        value = EpisodeTokenRegex.Replace(value, " ");
        value = EpisodeRangeRegex.Replace(value, " ");
        value = ResolutionRegex.Replace(value, " ");
        value = CodecRegex.Replace(value, " ");
        value = VersionTokenRegex.Replace(value, " ");
        value = LanguageTokenRegex.Replace(value, " ");
        value = ReleaseGroupRegex.Replace(value, " ");
        value = YearRegex.Replace(value, " ");
        value = SeasonWordRegex.Replace(value, " ");
        value = OrdinalSeasonRegex.Replace(value, " ");
        value = ChineseSeasonRegex.Replace(value, " ");

        var romanMatch = RomanSuffixRegex.Match(value);
        if (romanMatch.Success && !seasonNumber.HasValue)
            seasonNumber = RomanToSeason(romanMatch.Groups[1].Value);

        value = RomanSuffixRegex.Replace(value, " ");
        value = MovieRegex.Replace(value, " ");
        value = value.Replace('&', ' ');
        value = value.Replace('＋', ' ');
        value = value.Replace('-', ' ');
        value = MultiSpaceRegex.Replace(value, " ").Trim(' ', '-', '_', '~');
        value = TrimTrailingNoise(value, isMovieLike);
        return value;
    }

    private static string? BuildSimplifiedKeyword(string baseTitle, int? seasonNumber)
    {
        string simplified = ArcSuffixRegex.Replace(baseTitle, string.Empty).Trim();
        if (string.Equals(simplified, baseTitle, StringComparison.OrdinalIgnoreCase))
            return seasonNumber.HasValue ? simplified : null;

        return simplified.Length >= 3 ? simplified : null;
    }

    private static bool IsMeaningless(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return true;

        string value = NormalizeForComparison(input);
        if (value.Length == 0)
            return true;

        if (Regex.IsMatch(value, @"^\d+$"))
            return true;

        if (Regex.IsMatch(value, @"^season\d{0,2}$", RegexOptions.IgnoreCase))
            return true;

        return value is "season" or "s01" or "tv" or "webrip" or "bdrip" or "anime" or "video" or "library" or "collection";
    }

    private static bool IsAmbiguousShortKeyword(string baseTitle)
    {
        string normalized = NormalizeForComparison(baseTitle);
        if (normalized.Length == 0)
            return true;

        bool latinHeavy = normalized.All(ch => ch <= 127);
        return latinHeavy ? normalized.Length < 6 : normalized.Length < 3;
    }

    private static int? TryExtractSeason(string value)
    {
        var seasonMatch = SeasonWordRegex.Match(value);
        if (seasonMatch.Success && int.TryParse(seasonMatch.Groups[1].Value, out var season))
            return season;

        var ordinalMatch = OrdinalSeasonRegex.Match(value);
        if (ordinalMatch.Success && int.TryParse(ordinalMatch.Groups[1].Value, out season))
            return season;

        var chineseMatch = ChineseSeasonRegex.Match(value);
        if (chineseMatch.Success)
            return ParseChineseNumber(chineseMatch.Groups[1].Value);

        var romanMatch = RomanSuffixRegex.Match(value);
        if (romanMatch.Success)
            return RomanToSeason(romanMatch.Groups[1].Value);

        return null;
    }

    private static int? TryExtractYear(string value)
    {
        var match = YearRegex.Match(value);
        return match.Success && int.TryParse(match.Value, out var year) ? year : null;
    }

    private static int RomanToSeason(string roman)
        => roman.ToUpperInvariant() switch
        {
            "II" => 2,
            "III" => 3,
            "IV" => 4,
            "V" => 5,
            _ => 1
        };

    private static int ParseChineseNumber(string value)
    {
        if (int.TryParse(value, out var numeric))
            return numeric;

        return value switch
        {
            "一" => 1,
            "二" => 2,
            "三" => 3,
            "四" => 4,
            "五" => 5,
            "六" => 6,
            "七" => 7,
            "八" => 8,
            "九" => 9,
            "十" => 10,
            "两" => 2,
            _ => 1
        };
    }

    private static string TrimTrailingNoise(string value, bool isMovieLike)
    {
        string current = value;
        while (true)
        {
            string next = TrailingStopWordRegex.Replace(current, string.Empty).Trim();
            var trailingNumber = TrailingNumericTokenRegex.Match(next);
            if (trailingNumber.Success &&
                int.TryParse(trailingNumber.Groups[1].Value, out var numeric) &&
                (!isMovieLike || numeric != 0))
            {
                next = next[..trailingNumber.Index].TrimEnd();
            }

            if (string.Equals(next, current, StringComparison.Ordinal))
                return next;

            current = next;
        }
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var costs = new int[right.Length + 1];
        for (int j = 0; j <= right.Length; j++)
            costs[j] = j;

        for (int i = 1; i <= left.Length; i++)
        {
            int previous = costs[0];
            costs[0] = i;

            for (int j = 1; j <= right.Length; j++)
            {
                int current = costs[j];
                int substitutionCost = left[i - 1] == right[j - 1] ? 0 : 1;
                costs[j] = Math.Min(
                    Math.Min(costs[j] + 1, costs[j - 1] + 1),
                    previous + substitutionCost);
                previous = current;
            }
        }

        return costs[right.Length];
    }

    private static void AddDistinct(ICollection<string> list, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (!list.Contains(value, StringComparer.OrdinalIgnoreCase))
            list.Add(value);
    }
}
