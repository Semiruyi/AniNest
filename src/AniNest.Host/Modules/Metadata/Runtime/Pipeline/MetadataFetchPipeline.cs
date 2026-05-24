using AniNest.Application.Metadata;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Modules;

internal sealed class MetadataFetchPipeline : IMetadataFetchPipeline
{
    private readonly IMetadataPreparationService _preparation;
    private readonly IMetadataAcquisitionService _acquisition;
    private readonly IMetadataConfidenceService _confidence;
    private readonly IMetadataResolutionService _resolution;
    private readonly ILogger<MetadataFetchPipeline> _logger;

    public MetadataFetchPipeline(
        IMetadataPreparationService preparation,
        IMetadataAcquisitionService acquisition,
        IMetadataConfidenceService confidence,
        IMetadataResolutionService resolution,
        ILogger<MetadataFetchPipeline> logger)
    {
        _preparation = preparation;
        _acquisition = acquisition;
        _confidence = confidence;
        _resolution = resolution;
        _logger = logger;
    }

    public async Task<MetadataResolutionResult> ExecuteAsync(
        MetadataRecord record,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Metadata fetch pipeline started. FolderId={FolderId}", record.FolderId);
        var prepared = await _preparation.PrepareAsync(record, cancellationToken);
        _logger.LogInformation(
            "Metadata preparation completed. FolderId={FolderId}, BaseTitle={BaseTitle}, PrimaryKeyword={PrimaryKeyword}, SeasonAwareKeyword={SeasonAwareKeyword}, SimplifiedKeyword={SimplifiedKeyword}, SeasonNumber={SeasonNumber}, YearHint={YearHint}, IsMovieLike={IsMovieLike}, IsAmbiguousShortKeyword={IsAmbiguousShortKeyword}, Aliases={Aliases}",
            record.FolderId,
            prepared.NormalizedTitle,
            prepared.KeywordPlan.PrimaryKeyword,
            prepared.KeywordPlan.SeasonAwareKeyword,
            prepared.KeywordPlan.SimplifiedKeyword,
            prepared.SeasonNumber,
            prepared.YearHint,
            prepared.IsMovieLike,
            prepared.KeywordPlan.IsAmbiguousShortKeyword,
            string.Join(" | ", prepared.Aliases));
        var acquisition = await _acquisition.AcquireAsync(prepared, cancellationToken);
        _logger.LogInformation(
            "Metadata acquisition completed. FolderId={FolderId}, SearchSucceeded={SearchSucceeded}, CandidateCount={CandidateCount}, FailureReason={FailureReason}, Candidates={Candidates}",
            record.FolderId,
            acquisition.SearchSucceeded,
            acquisition.Candidates.Count,
            acquisition.FailureReason,
            string.Join(" | ", acquisition.Candidates.Select(candidate => $"{candidate.SourceId}:{candidate.MatchedTitle ?? candidate.Detail?.Title ?? "unknown"}")));
        var confidence = _confidence.Evaluate(prepared, acquisition);
        _logger.LogInformation(
            "Metadata confidence completed. FolderId={FolderId}, BestSourceId={BestSourceId}, BestLevel={BestLevel}, BestScore={BestScore}, Reasons={Reasons}",
            record.FolderId,
            confidence.BestCandidate?.Candidate.SourceId,
            confidence.BestCandidate?.Level,
            confidence.BestCandidate?.Score,
            confidence.BestCandidate is null ? string.Empty : string.Join(" | ", confidence.BestCandidate.Reasons));
        _logger.LogInformation(
            "Metadata confidence candidates. FolderId={FolderId}, Candidates={Candidates}",
            record.FolderId,
            string.Join(
                " || ",
                confidence.Candidates.Select(candidate =>
                    $"{candidate.Candidate.SourceId}:{candidate.Level}:{candidate.Score:0.00}:{string.Join(",", candidate.Reasons)}")));
        var resolution = _resolution.Resolve(record, prepared, acquisition, confidence);
        _logger.LogInformation(
            "Metadata fetch pipeline completed. FolderId={FolderId}, NextState={NextState}, FailureKind={FailureKind}, Reason={Reason}",
            record.FolderId,
            resolution.NextState,
            resolution.FailureKind,
            resolution.Reason);
        return resolution;
    }
}
