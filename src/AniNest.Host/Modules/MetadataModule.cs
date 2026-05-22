using AniNest.Application.Metadata;
using AniNest.Application.Modules;
using AniNest.Application.Library;
using AniNest.Contracts.Metadata;
using AniNest.Core.Enums;
using AniNest.Host.Events;

namespace AniNest.Host.Modules;

internal sealed class MetadataModule : IMetadataModule
{
    private readonly MetadataService _metadata;
    private readonly ILibraryCatalogStore _libraryCatalogStore;
    private readonly IHostEventStream _events;

    public MetadataModule(IMetadataStore store, ILibraryCatalogStore libraryCatalogStore, IHostEventStream events)
    {
        _metadata = new MetadataService(store);
        _libraryCatalogStore = libraryCatalogStore;
        _events = events;
    }

    public Task<MetadataDto?> GetByFolderAsync(string folderId, CancellationToken cancellationToken = default)
        => Task.FromResult(_metadata.GetByFolderId(folderId));

    public Task RefreshFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        _metadata.RefreshFolder(folderId);
        PublishFolderState(folderId);
        return Task.CompletedTask;
    }

    public Task RetryFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        _metadata.RetryFolder(folderId);
        PublishFolderState(folderId);
        return Task.CompletedTask;
    }

    public Task EnqueueMissingAsync(CancellationToken cancellationToken = default)
    {
        _metadata.EnqueueMissing();
        PublishSummaryChanged();
        return Task.CompletedTask;
    }

    public Task RetryFailedAsync(bool includeNoMatch, CancellationToken cancellationToken = default)
    {
        _metadata.RetryFailed(includeNoMatch);
        PublishSummaryChanged();
        return Task.CompletedTask;
    }

    public Task<MetadataStatusSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_metadata.GetSummary());

    public Task<MetadataProcessingResultDto> ProcessQueueAsync(int maxItems, CancellationToken cancellationToken = default)
    {
        var effectiveMaxItems = Math.Max(1, maxItems);
        var queued = _metadata.GetAll()
            .Where(item => item.State == MetadataState.Queued)
            .Take(effectiveMaxItems)
            .ToArray();

        foreach (var item in queued)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scraping = item with
            {
                State = MetadataState.Scraping,
                FailureKind = MetadataFailureKind.None
            };
            _metadata.Save(scraping);
            PublishFolderState(item.FolderId);

            var libraryFolder = _libraryCatalogStore.GetFolders()
                .FirstOrDefault(folder => string.Equals(folder.FolderId, item.FolderId, StringComparison.OrdinalIgnoreCase));
            var title = item.Title ?? libraryFolder?.Name ?? FormatFolderTitle(item.FolderId);
            var ready = scraping with
            {
                Title = title,
                OriginalTitle = item.OriginalTitle ?? title,
                Summary = item.Summary ?? $"Metadata generated for {title}.",
                Tags = item.Tags.Count > 0 ? item.Tags : ["generated"],
                PosterPath = item.PosterPath ?? $"/artwork/{item.FolderId}/poster.jpg",
                Season = item.Season ?? "S1",
                EpisodeCount = item.EpisodeCount ?? libraryFolder?.VideoCount,
                Source = item.Source ?? "simulated",
                State = MetadataState.Ready,
                FailureKind = MetadataFailureKind.None
            };
            _metadata.Save(ready);
            PublishFolderState(item.FolderId);
        }

        return Task.FromResult(new MetadataProcessingResultDto(
            queued.Length,
            queued.Select(item => item.FolderId).ToArray()));
    }

    private void PublishFolderState(string folderId)
    {
        var metadata = _metadata.GetByFolderId(folderId);
        _events.Publish("metadata.folder_updated", new
        {
            folderId,
            state = metadata?.State.ToString(),
            failureKind = metadata?.FailureKind.ToString()
        });
        PublishSummaryChanged();
    }

    private void PublishSummaryChanged()
    {
        var summary = _metadata.GetSummary();
        _events.Publish("metadata.summary_changed", summary);
    }

    private static string FormatFolderTitle(string folderId)
    {
        if (string.IsNullOrWhiteSpace(folderId))
            return folderId;

        return string.Join(
            ' ',
            folderId
                .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(segment => char.ToUpperInvariant(segment[0]) + segment[1..]));
    }
}
