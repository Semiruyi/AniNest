using AniNest.Application.Metadata;
using AniNest.Contracts.Metadata;
using AniNest.Host.Modules;

namespace AniNest.Backend.Tests;

internal sealed class FakeMetadataRuntimeStateService : IMetadataRuntimeStateService
{
    public Dictionary<string, MetadataDto> MetadataByFolderId { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, MetadataRecord> RecordsByFolderId { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<MetadataRecord> GetAllRecords() => RecordsByFolderId.Values.ToArray();

    public MetadataRecord? GetRecord(string folderId)
        => RecordsByFolderId.TryGetValue(folderId, out var record) ? record : null;

    public MetadataRecord RequireRecord(string folderId)
        => GetRecord(folderId) ?? throw new KeyNotFoundException($"Metadata for folder '{folderId}' was not found.");

    public void SaveRecord(MetadataRecord record)
        => RecordsByFolderId[record.FolderId] = record;

    public void DeleteRecord(string folderId)
        => RecordsByFolderId.Remove(folderId);

    public MetadataDto? GetMetadata(string folderId)
        => MetadataByFolderId.TryGetValue(folderId, out var metadata) ? metadata : null;

    public MetadataStatusSummaryDto BuildSummary() => new(0, 0, 0, 0, 0, 0, 0, 0, 0);

    public MetadataFolderStateSummary GetFolderStateSummary(string folderId)
    {
        var metadata = GetMetadata(folderId);
        return metadata is null
            ? new MetadataFolderStateSummary(false, AniNest.Core.Enums.MetadataState.NeedsMetadata, null, null)
            : new MetadataFolderStateSummary(
                !string.IsNullOrWhiteSpace(metadata.Title) || !string.IsNullOrWhiteSpace(metadata.PosterPath),
                metadata.State,
                metadata.Title,
                metadata.PosterPath);
    }

    public void NormalizeTransientStates()
    {
    }

    public void PublishFolderState(string folderId)
    {
    }

    public void PublishSummaryChanged()
    {
    }

    public Task ExecuteAsync(MetadataRecord record, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
