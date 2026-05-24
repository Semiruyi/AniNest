namespace AniNest.Host.Modules;

internal sealed class MetadataConfidenceService : IMetadataConfidenceService
{
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

        if (!string.IsNullOrWhiteSpace(candidate.Detail?.Title))
        {
            score += 0.4;
            reasons.Add("provider.detail.title");
        }

        if (!string.IsNullOrWhiteSpace(candidate.Detail?.OriginalTitle))
        {
            score += 0.2;
            reasons.Add("provider.detail.original-title");
        }

        if (context.Aliases.Any(alias =>
                string.Equals(alias, candidate.MatchedTitle, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(alias, candidate.Detail?.Title, StringComparison.OrdinalIgnoreCase)))
        {
            score += 0.3;
            reasons.Add("alias.exact-match");
        }

        if (context.IsMovieLike)
            reasons.Add("format.movie-like-input");

        var level = score switch
        {
            >= 0.8 => MetadataConfidenceLevel.High,
            >= 0.5 => MetadataConfidenceLevel.Medium,
            > 0 => MetadataConfidenceLevel.Low,
            _ => MetadataConfidenceLevel.None
        };

        return new MetadataConfidenceCandidate(candidate, score, level, reasons);
    }
}
