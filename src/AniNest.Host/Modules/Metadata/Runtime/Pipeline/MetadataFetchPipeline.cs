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
        var acquisition = await _acquisition.AcquireAsync(prepared, cancellationToken);
        var confidence = _confidence.Evaluate(prepared, acquisition);
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
