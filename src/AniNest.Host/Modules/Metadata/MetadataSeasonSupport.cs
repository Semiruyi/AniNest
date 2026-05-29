using System.Text.RegularExpressions;

namespace AniNest.Host.Modules;

internal static partial class MetadataSeasonSupport
{
    public static int? DetectSeason(IEnumerable<string> titles)
    {
        foreach (var title in titles)
        {
            var season = DetectSeason(title);
            if (season.HasValue)
                return season;
        }

        return null;
    }

    public static int? DetectSeason(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var normalized = NormalizeTitle(title);

        var chineseMatch = ChineseSeasonRegex().Match(normalized);
        if (chineseMatch.Success)
            return ParseSeasonToken(chineseMatch.Groups["season"].Value);

        var numericMatch = NumericSeasonRegex().Match(normalized);
        if (numericMatch.Success)
            return ParseSeasonToken(numericMatch.Groups["season"].Value);

        var ordinalMatch = OrdinalSeasonRegex().Match(normalized);
        if (ordinalMatch.Success)
            return ParseSeasonToken(ordinalMatch.Groups["season"].Value);

        return null;
    }

    public static string BuildPreferredSeasonKeyword(string baseTitle, int seasonNumber)
        => ContainsCjk(baseTitle)
            ? $"{baseTitle} 第{seasonNumber}季"
            : $"{baseTitle} Season {seasonNumber}";

    public static string? BuildAlternateSeasonKeyword(string baseTitle, int seasonNumber)
    {
        if (ContainsCjk(baseTitle))
        {
            var chineseSeason = ToChineseNumeral(seasonNumber);
            return chineseSeason is null
                ? $"{baseTitle} 第{seasonNumber}期"
                : $"{baseTitle} 第{chineseSeason}季";
        }

        return $"{baseTitle} {ToOrdinal(seasonNumber)} season";
    }

    public static IReadOnlyList<string> BuildSeasonKeywordVariants(string baseTitle, int seasonNumber)
    {
        var keywords = new List<string>
        {
            BuildPreferredSeasonKeyword(baseTitle, seasonNumber)
        };

        AddVariant(keywords, BuildAlternateSeasonKeyword(baseTitle, seasonNumber));

        if (ContainsCjk(baseTitle))
        {
            AddVariant(keywords, $"{baseTitle} 第{seasonNumber}期");
            AddVariant(keywords, $"{baseTitle} {ToOrdinal(seasonNumber)} season");
        }
        else
        {
            AddVariant(keywords, $"{baseTitle} 第{seasonNumber}季");
        }

        return keywords;
    }

    private static void AddVariant(ICollection<string> keywords, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            keywords.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        keywords.Add(value);
    }

    private static string NormalizeTitle(string value)
        => value
            .Normalize()
            .Replace('\uFF1A', ':')
            .Replace('\u3000', ' ')
            .Replace('\uFF10', '0')
            .Replace('\uFF11', '1')
            .Replace('\uFF12', '2')
            .Replace('\uFF13', '3')
            .Replace('\uFF14', '4')
            .Replace('\uFF15', '5')
            .Replace('\uFF16', '6')
            .Replace('\uFF17', '7')
            .Replace('\uFF18', '8')
            .Replace('\uFF19', '9');

    private static bool ContainsCjk(string value)
        => value.Any(ch => ch is >= '\u4e00' and <= '\u9fff');

    private static int? ParseSeasonToken(string token)
    {
        if (int.TryParse(token, out var numeric))
            return numeric;

        return token.ToUpperInvariant() switch
        {
            "I" => 1,
            "II" => 2,
            "III" => 3,
            "IV" => 4,
            "V" => 5,
            "VI" => 6,
            "\u4e00" => 1,
            "\u4e8c" => 2,
            "\u4e09" => 3,
            "\u56db" => 4,
            "\u4e94" => 5,
            "\u516d" => 6,
            _ => null
        };
    }

    private static string ToOrdinal(int seasonNumber)
        => seasonNumber switch
        {
            1 => "1st",
            2 => "2nd",
            3 => "3rd",
            _ => $"{seasonNumber}th"
        };

    private static string? ToChineseNumeral(int seasonNumber)
        => seasonNumber switch
        {
            1 => "\u4e00",
            2 => "\u4e8c",
            3 => "\u4e09",
            4 => "\u56db",
            5 => "\u4e94",
            6 => "\u516d",
            _ => null
        };

    [GeneratedRegex(@"\u7b2c\s*(?<season>\d+|[\u4e00\u4e8c\u4e09\u56db\u4e94\u516d])\s*[\u5b63\u671f\u90e8]", RegexOptions.IgnoreCase)]
    private static partial Regex ChineseSeasonRegex();

    [GeneratedRegex(@"\b(?:season|s|part)\s*(?<season>\d+|i|ii|iii|iv|v|vi)\b|\b(?<season>\d+)\s*[\u5b63\u671f\u90e8]\b", RegexOptions.IgnoreCase)]
    private static partial Regex NumericSeasonRegex();

    [GeneratedRegex(@"\b(?<season>\d+)(?:st|nd|rd|th)\s+season\b", RegexOptions.IgnoreCase)]
    private static partial Regex OrdinalSeasonRegex();
}
