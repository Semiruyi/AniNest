using AniNest.Application.Modules;
using AniNest.Contracts.Metadata;
using AniNest.Core.Enums;
using AniNest.Host.Modules;

namespace AniNest.Host.Endpoints;

internal static class MetadataEndpoints
{
    public static IEndpointRouteBuilder MapMetadataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/metadata").WithTags("Metadata");

        group.MapGet("/status-summary", (IMetadataModule module, CancellationToken cancellationToken)
            => module.GetSummaryAsync(cancellationToken));

        group.MapGet("/folders/{folderId}", async (string folderId, IMetadataModule module, CancellationToken cancellationToken) =>
        {
            var metadata = await module.GetByFolderAsync(folderId, cancellationToken);
            return metadata is null ? Results.NotFound() : Results.Ok(metadata);
        });

        group.MapGet("/reviews", (IMetadataModule module, CancellationToken cancellationToken)
            => module.GetReviewQueueAsync(cancellationToken));

        group.MapGet("/reviews/{folderId}", async (string folderId, IMetadataModule module, CancellationToken cancellationToken) =>
        {
            var review = await module.GetReviewByFolderAsync(folderId, cancellationToken);
            return review is null ? Results.NotFound() : Results.Ok(review);
        });

        group.MapPost("/reviews/{folderId}:confirm", async (string folderId, ConfirmMetadataReviewRequest request, IMetadataModule module, CancellationToken cancellationToken) =>
        {
            await module.ConfirmReviewAsync(folderId, request.SourceId, cancellationToken);
            return Results.Accepted();
        });

        group.MapPost("/reviews/{folderId}:reject-candidate", async (string folderId, RejectMetadataReviewCandidateRequest request, IMetadataModule module, CancellationToken cancellationToken) =>
        {
            await module.RejectReviewCandidateAsync(folderId, request.SourceId, cancellationToken);
            return Results.Accepted();
        });

        group.MapPost("/folders/{folderId}:refresh", async (string folderId, IMetadataModule module, CancellationToken cancellationToken) =>
        {
            await module.RefreshFolderAsync(folderId, cancellationToken);
            return Results.Accepted();
        });

        group.MapPost("/folders/{folderId}:retry", async (string folderId, IMetadataModule module, CancellationToken cancellationToken) =>
        {
            await module.RetryFolderAsync(folderId, cancellationToken);
            return Results.Accepted();
        });

        app.MapPost("/api/metadata:debug-match", async (
            MetadataDebugMatchRequest request,
            IMetadataAcquisitionService acquisition,
            IMetadataConfidenceService confidence,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.FolderName))
                throw new ArgumentException("FolderName is required.", nameof(request.FolderName));

            var videoFiles = request.VideoFiles ?? [];
            var analysis = MetadataPreparationAnalyzer.Analyze(
                request.FolderName,
                request.ParentFolderName,
                videoFiles);
            var record = new AniNest.Application.Metadata.MetadataRecord(
                "__debug__",
                string.Empty,
                request.FolderName,
                string.Empty,
                MetadataState.Unknown,
                MetadataFailureKind.None,
                null,
                null,
                null,
                null,
                null,
                null);
            var folder = new AniNest.Application.Metadata.MetadataFolderRef(
                "__debug__",
                string.Empty,
                request.FolderName,
                request.ParentFolderName,
                videoFiles,
                videoFiles.Count);
            var context = new MetadataPreparedContext(
                record,
                folder,
                analysis.KeywordPlan,
                analysis.SearchSeed,
                analysis.BaseTitle,
                analysis.Aliases,
                analysis.SeasonNumber,
                analysis.YearHint,
                analysis.IsMovieLike);
            var acquisitionResult = await acquisition.AcquireAsync(context, cancellationToken);
            var confidenceResult = confidence.Evaluate(context, acquisitionResult);

            return Results.Ok(new MetadataDebugMatchResponse(
                new MetadataDebugPreparedDto(
                    context.SearchSeed,
                    context.NormalizedTitle,
                    context.Aliases,
                    context.KeywordPlan.PrimaryKeyword,
                    context.KeywordPlan.SeasonAwareKeyword,
                    context.KeywordPlan.SimplifiedKeyword,
                    context.KeywordPlan.BaseTitle,
                    context.SeasonNumber,
                    context.YearHint,
                    context.IsMovieLike),
                MetadataSearchKeywordBuilder.Build(context),
                acquisitionResult.SearchSucceeded,
                acquisitionResult.FailureReason,
                MapOptionalCandidate(confidenceResult.BestCandidate),
                confidenceResult.Candidates.Select(MapCandidate).ToArray()));
        }).WithTags("Metadata");

        app.MapPost("/api/metadata:enqueue-missing", async (IMetadataModule module, CancellationToken cancellationToken) =>
        {
            await module.EnqueueMissingAsync(cancellationToken);
            return Results.Accepted();
        }).WithTags("Metadata");

        app.MapPost("/api/metadata:retry-failed", async (RetryFailedMetadataRequest request, IMetadataModule module, CancellationToken cancellationToken) =>
        {
            await module.RetryFailedAsync(request.IncludeNoMatch, cancellationToken);
            return Results.Accepted();
        }).WithTags("Metadata");

        app.MapPost("/api/metadata:process-queue", async (int? maxItems, IMetadataModule module, CancellationToken cancellationToken) =>
        {
            var payload = await module.ProcessQueueAsync(maxItems ?? 1, cancellationToken);
            return Results.Ok(payload);
        }).WithTags("Metadata");

        return app;
    }

    private static MetadataDebugCandidateDto? MapOptionalCandidate(MetadataConfidenceCandidate? candidate)
        => candidate is null ? null : MapCandidate(candidate);

    private static MetadataDebugCandidateDto MapCandidate(MetadataConfidenceCandidate candidate)
        => new(
            candidate.Candidate.SourceId,
            candidate.Candidate.MatchedTitle,
            candidate.Candidate.OriginalTitle,
            candidate.Candidate.Detail?.Title,
            candidate.Candidate.Detail?.OriginalTitle,
            candidate.Candidate.Year,
            candidate.Candidate.Detail?.Year,
            candidate.Candidate.HitCount,
            candidate.Candidate.BestRank,
            candidate.Score,
            candidate.Level.ToString(),
            candidate.Reasons);
}
