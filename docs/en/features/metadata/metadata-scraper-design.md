# Metadata Scraper Design Document

## 1. Background and Design Direction

### 1.1 Background

AniNest's Library page currently displays folder cards for the user's local video library. Card covers rely on local files such as `cover.jpg` and `folder.jpg`; if no cover image is manually placed, a default style is shown. For anime content, metadata such as titles, summaries, ratings, and air dates are completely missing, so browsing feels visually sparse and structurally blind.

### 1.2 Design Direction

Phase 1 should not be designed as "a one-off background scraper". It should be designed as a **library-attached metadata subsystem** with four goals:

- work for both **existing folders** and **newly added folders**
- recover cleanly across **restart, failure, and folder moves**
- keep the UI **quiet but not opaque**
- stay aligned with AniNest's existing "intent + background work + query" architecture style used by thumbnails

### 1.3 Phase 1 Goals

- Automatically fetch anime metadata from Bangumi.
- Use strict matching and prefer false negatives over false positives.
- Refresh card covers automatically: local cover first, metadata poster second, placeholder last.
- Provide lightweight visibility into background metadata state.
- Support current-library reconciliation, including stale-state recovery and orphan cleanup.

### 1.4 Explicit Non-Goals

- No metadata export (`tvshow.nfo`, `poster.jpg`) in Phase 1.
- No manual search dialog in Phase 1.
- No multi-provider federation in Phase 1.
- No rich metadata overlays on cards in Phase 1.

---

## 2. Product Experience

### 2.1 What the User Should Feel

The feature should feel like this:

1. The library opens normally and immediately.
2. Existing folders quietly start receiving posters in the background.
3. Newly added folders join that same pipeline automatically.
4. If matching fails or the network is down, the app does not interrupt the user, but it also does not hide the fact that work stalled.

The target experience is "helpful background enrichment", not "mysterious automation".

### 2.2 User-Facing Behavior

- Library cards always render from existing local data first.
- Metadata never blocks library load.
- The settings page exposes a compact metadata status area:
  - `Needs metadata`
  - `Queued`
  - `Scraping`
  - `Ready`
  - `Needs review`
  - `Disabled`
- The settings page exposes three actions:
  - `Scan Missing Metadata`
  - `Retry Failed`
  - `Pause / Resume Auto Metadata`

Phase 1 does **not** need per-card badges, but it does need a reliable page-level status surface.

### 2.3 Failure Experience

Failures are grouped into user-meaningful buckets:

- `NoMatch`: no candidate or low-confidence candidate
- `NetworkError`: timeout, connection failure, 5xx
- `ProviderError`: invalid response, 404 detail, malformed payload

The UI only needs lightweight breakdown counts in Phase 1, but the storage model should preserve the failure kind so future UI can become smarter.

---

## 3. Core Design

### 3.1 Architectural Shape

Instead of a single service owning every concern, Phase 1 should use four internal roles:

```text
UI Layer
  - Folder card binding
  - Metadata status summary
  - Settings actions

Library Integration Layer
  - Load library folders
  - Read metadata for DTO assembly
  - Forward add/delete events
  - Trigger library metadata sync

Metadata Runtime Layer
  - MetadataSyncCoordinator      // command-oriented entry points
  - MetadataTaskStore            // in-memory work queue and runtime state
  - MetadataWorker               // single-thread background worker
  - MetadataQueryService         // read-only state and summary access

Metadata Storage / Provider Layer
  - MetadataIndexStore           // persistent state
  - MetadataRepository           // metadata payload JSON
  - MetadataImageCache           // poster cache
  - BangumiMetadataProvider      // HTTP + DTO mapping + rate limiting
  - MetadataMatcher              // keyword extraction + strict matching
```

This mirrors the thumbnail direction:

- commands are intent-oriented
- runtime coordination stays separate from persistence
- query access stays read-oriented
- the facade remains easy to reason about

### 3.2 Why This Shape

The previous "one coordinator owns queue + index + reconciliation + UI summary + retry semantics" shape works for a prototype, but it leaves too many ambiguities:

- restart recovery is hard to define cleanly
- UI actions map poorly to service APIs
- persistent state and runtime state get mixed together
- reconciliation becomes a grab bag of unrelated side effects

This redesign makes those seams explicit.

---

## 4. Data Model

### 4.1 Persistent Record

Persistent metadata state should be modeled as a **record**, not just a raw scrape status.

```csharp
public sealed class MetadataRecord
{
    public string FolderPath { get; set; } = "";
    public string FolderName { get; set; } = "";
    public string FolderFingerprint { get; set; } = "";

    public MetadataState State { get; set; } = MetadataState.NeedsMetadata;
    public MetadataFailureKind FailureKind { get; set; } = MetadataFailureKind.None;

    public string? SourceId { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? LastSucceededAtUtc { get; set; }
    public DateTime? CooldownUntilUtc { get; set; }

    public string? MetadataFilePath { get; set; }
    public string? PosterFilePath { get; set; }
}
```

### 4.2 State Model

Use one user-comprehensible state model end to end:

```csharp
public enum MetadataState
{
    NeedsMetadata,   // never scraped or stale and eligible for work
    Queued,          // pending in background queue
    Scraping,        // currently being processed
    Ready,           // metadata available locally
    NeedsReview,     // terminal-ish issue: no match or low confidence
    Disabled         // auto metadata disabled by user
}

public enum MetadataFailureKind
{
    None,
    NoMatch,
    NetworkError,
    ProviderError
}
```

This intentionally avoids `Skipped` vs `NotScraped` ambiguity. `Disabled` is user intent. `NeedsMetadata` is work still worth doing.

### 4.3 Runtime State vs Persistent State

Important rule:

- `Queued` and `Scraping` are **runtime-first** states
- they may be persisted for crash recovery hints
- they must never be trusted blindly after restart

On startup, previously persisted `Queued` or `Scraping` records are normalized back to `NeedsMetadata` unless the worker explicitly reclaims them in the same session.

This is the same broad principle thumbnails already use when translating stale in-progress state back into pending work.

### 4.4 Folder Identity and Move Handling

Phase 1 should still use `folderPath` as the primary identity, but it should add a lightweight `FolderFingerprint` to reduce churn on moves and renames.

`FolderFingerprint` can be derived from:

- normalized folder name
- video count
- up to first N sorted video file names

It does **not** need to be perfect. It only needs to support best-effort migration.

If reconciliation sees:

- one old record missing from the current library
- one new folder with no record
- matching or highly similar fingerprint

then the record should be migrated forward to the new path instead of being deleted immediately.

If no reasonable migration candidate exists, the old record is treated as orphaned cache and removed.

---

## 5. Command and Query Design

### 5.1 Public Command Surface

```csharp
public interface IMetadataSyncService
{
    Task SyncLibrarySnapshotAsync(
        IReadOnlyList<MetadataFolderRef> folders,
        CancellationToken ct = default);

    Task RegisterFolderAsync(
        MetadataFolderRef folder,
        CancellationToken ct = default);

    Task DeleteFolderAsync(
        string folderPath,
        CancellationToken ct = default);

    Task EnqueueMissingAsync(
        CancellationToken ct = default);

    Task RetryFailedAsync(
        bool includeNoMatch = false,
        CancellationToken ct = default);

    Task SetAutoMetadataEnabledAsync(
        bool enabled,
        CancellationToken ct = default);
}
```

These APIs map directly to user or library intents:

- `SyncLibrarySnapshotAsync` -> reconcile current library
- `RegisterFolderAsync` -> fast path for new additions
- `DeleteFolderAsync` -> synchronous cleanup on removal
- `EnqueueMissingAsync` -> settings action
- `RetryFailedAsync` -> settings action
- `SetAutoMetadataEnabledAsync` -> settings toggle behavior

### 5.2 Public Query Surface

```csharp
public interface IMetadataQueryService
{
    FolderMetadata? GetMetadata(string folderPath);

    MetadataState GetState(string folderPath);

    MetadataStatusSummary GetSummary();
}

public sealed record MetadataStatusSummary(
    int NeedsMetadataCount,
    int QueuedCount,
    int ScrapingCount,
    int ReadyCount,
    int NeedsReviewCount,
    int DisabledCount,
    int NetworkErrorCount,
    int NoMatchCount,
    int ProviderErrorCount);
```

This keeps status reads out of the command service and makes the UI simpler.

### 5.3 UI Event Surface

```csharp
public interface IMetadataEvents
{
    event EventHandler<FolderMetadataRefreshedEventArgs>? FolderMetadataRefreshed;
    event EventHandler<MetadataSummaryChangedEventArgs>? SummaryChanged;
}
```

`FolderMetadataRefreshed` updates individual cards.

`SummaryChanged` updates the settings/status surface without forcing the UI to poll.

---

## 6. Reconciliation Design

### 6.1 Why Reconciliation Is the Centerpiece

Phase 1 is not primarily about "what happens when a new folder is added". It is primarily about "how metadata stays consistent with the actual library over time".

That means the center of the system is `SyncLibrarySnapshotAsync(...)`, not `RegisterFolderAsync(...)`.

### 6.2 Reconciliation Steps

```text
LibraryAppService.LoadLibraryAsync completes
  -> MetadataSyncService.SyncLibrarySnapshotAsync(current folders)
     1. Load current metadata records
     2. Normalize stale runtime states:
        - Queued    -> NeedsMetadata
        - Scraping  -> NeedsMetadata
     3. Build current folder-path set
     4. Attempt move/rename migration by fingerprint
     5. Remove orphaned records that no longer map to library folders
     6. Ensure every current folder has a record
     7. If Auto Metadata is enabled:
        - enqueue eligible NeedsMetadata records
        - leave cooldowned failures alone
     8. Publish summary update
```

### 6.3 Eligibility Rules

A folder is eligible to enqueue automatically when all are true:

- state is `NeedsMetadata`
- folder still exists in library
- folder is not already queued or scraping in current runtime
- cooldown is absent or expired
- auto metadata is enabled

### 6.4 Retry Rules

- `RetryFailed(includeNoMatch: false)` retries transient failures first.
- `RetryFailed(includeNoMatch: true)` is a stronger action that also retries `NoMatch`.
- `EnqueueMissingAsync()` only touches `NeedsMetadata`, not `NeedsReview`.

This gives the product simple knobs without conflating "never tried" and "already deemed low-confidence".

---

## 7. Background Worker Design

### 7.1 Execution Model

Phase 1 uses a single-thread background worker. That is enough because:

- Bangumi requests need serialization anyway
- the library size is small
- correctness and simplicity matter more than raw throughput

### 7.2 Worker Flow

```text
MetadataWorker
  -> Dequeue next folder
  -> State = Scraping
  -> Build keyword plan from MetadataMatcher
  -> Try search attempts in order:
     - season-aware keyword if available
     - base keyword
     - simplified fallback keyword
  -> Evaluate candidates with strict matching
  -> If accepted:
     - fetch detail
     - download poster
     - save metadata JSON
     - update record to Ready
     - raise FolderMetadataRefreshed
  -> If rejected:
     - update record to NeedsReview + NoMatch
  -> If transient error:
     - update record to NeedsMetadata or NeedsReview based on policy
     - apply cooldown if needed
  -> Raise SummaryChanged
```

### 7.3 Matching Policy

Strict matching remains conservative:

- anime only (`type = 2`)
- similarity threshold `>= 0.5`
- short ambiguous keywords rejected
- year hint helps but does not hard reject

The notable difference in this redesign is that matching should use a **keyword plan**, not a single keyword string:

1. season-aware keyword
2. base title keyword
3. simplified fallback keyword

That gives better recovery without making the algorithm much more complex.

### 7.4 Cooldown Policy

- `NoMatch` -> long cooldown, default 7 days
- `NetworkError` -> short cooldown, default 30 minutes
- `ProviderError` -> medium cooldown, default 1 day

This is better than one cooldown for every failure type because the user experience intent is different.

---

## 8. Storage Design

### 8.1 Metadata Payload

- **Location**: `{AppPaths.CacheDirectory}/metadata/{folderPathHash}.json`
- **Format**: one payload file per folder
- **Ownership**: app-owned cache

```csharp
public class FolderMetadata
{
    public string FolderPath { get; set; } = "";
    public string? Title { get; set; }
    public string? OriginalTitle { get; set; }
    public string? Summary { get; set; }
    public string? PosterUrl { get; set; }
    public string? LocalPosterPath { get; set; }
    public string? Date { get; set; }
    public double? Rating { get; set; }
    public int? Episodes { get; set; }
    public string? Platform { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? SourceId { get; set; }
    public DateTime ScrapedAt { get; set; }
}
```

### 8.2 Poster Cache

- **Location**: `{AppPaths.CacheDirectory}/metadata/posters/{folderPathHash}.jpg`
- **Policy**: persistent while the folder remains part of the library

### 8.3 Record Index

- **Location**: `{AppPaths.CacheDirectory}/metadata/index.json`
- **Purpose**: source of truth for metadata runtime and history state

Example:

```json
{
  "C:\\Anime\\Attack on Titan": {
    "folderName": "Attack on Titan",
    "folderFingerprint": "a7f1f4d1c3",
    "state": "Ready",
    "failureKind": "None",
    "sourceId": "55770",
    "lastAttemptAtUtc": "2026-05-13T10:00:00Z",
    "lastSucceededAtUtc": "2026-05-13T10:00:02Z",
    "cooldownUntilUtc": null,
    "metadataFilePath": "metadata\\6ad3....json",
    "posterFilePath": "metadata\\posters\\6ad3....jpg"
  }
}
```

### 8.4 Crash-Safe Persistence

Index updates should follow the same temp-file promote pattern already used by thumbnail storage:

- write `index.json.tmp`
- promote atomically to `index.json`
- optionally keep a `.bak`

This part is boring, which is exactly why it should copy the existing repo pattern.

---

## 9. Library Integration

### 9.1 Load Path

`LibraryAppService.LoadLibraryAsync(...)` should:

1. load folders from settings
2. scan covers and video counts
3. read metadata payloads from `IMetadataQueryService`
4. return UI DTOs immediately
5. fire-and-forget `SyncLibrarySnapshotAsync(...)`

### 9.2 Add Path

`AddFolderAsync(...)` and `AddFolderBatchAsync(...)` should:

1. persist the folder in library settings
2. scan local files
3. return the folder DTO
4. call `RegisterFolderAsync(...)`

### 9.3 Delete Path

`DeleteFolderAsync(...)` should:

1. remove folder from library settings
2. remove thumbnail collection
3. call `DeleteFolderAsync(...)` on metadata sync service

### 9.4 Threading

UI updates must stay event-driven and marshaled through `Dispatcher`.

- card refresh uses `FolderMetadataRefreshed`
- summary/status refresh uses `SummaryChanged`

No metadata service should ever mutate `ObservableCollection` directly.

---

## 10. Phase 1 Implementation Plan

### Step 1: Storage and State

1. Define `MetadataRecord`, `MetadataState`, `MetadataFailureKind`.
2. Implement `MetadataIndexStore` with temp-file promotion.
3. Implement `MetadataRepository`.
4. Implement `MetadataImageCache`.
5. Add settings:
   - `AutoScrapeMetadata`
   - `BangumiAccessToken`

### Step 2: Provider and Matching

1. Implement `BangumiMetadataProvider`.
2. Implement `MetadataMatcher` keyword-plan API:
   - season-aware keyword
   - base keyword
   - simplified fallback keyword
3. Add strict-match evaluator and failure classification.

### Step 3: Runtime Layer

1. Implement `MetadataTaskStore`.
2. Implement `MetadataQueryService`.
3. Implement `MetadataSyncCoordinator`.
4. Implement `MetadataWorker`.
5. Implement events:
   - `FolderMetadataRefreshed`
   - `SummaryChanged`

### Step 4: Library Integration

1. Extend `LibraryFolderDto` / `FolderListItem` with metadata payload.
2. Read metadata during `LoadLibraryAsync`.
3. Trigger `SyncLibrarySnapshotAsync` after load.
4. Wire folder add/delete events to metadata sync service.

### Step 5: Settings Surface

1. Add metadata status summary.
2. Add actions:
   - `Scan Missing Metadata`
   - `Retry Failed`
   - `Pause / Resume Auto Metadata`
3. Show coarse failure breakdown counts.

### Step 6: Testing

1. Existing-library bootstrap.
2. New-folder registration.
3. Restart normalization of stale `Queued` / `Scraping`.
4. Fingerprint-based move/rename migration.
5. Orphan cleanup.
6. Failure cooldown behavior by failure kind.
7. Cover refresh after successful scrape.

---

## 11. Future Evolution

This design intentionally leaves room for:

| Feature | How It Fits |
|---------|-------------|
| Manual search dialog | Add command APIs for explicit candidate selection; keep query side unchanged. |
| Metadata export | Add exporter as a separate command path; do not fold it into scraping core. |
| Multiple providers | Add new `IMetadataProvider` implementations and provider selection policy. |
| Per-card status badge | Reuse existing `MetadataState` and `MetadataFailureKind`. |
| Force refresh by `sourceId` | Add refresh command that bypasses search and re-fetches detail. |
| Smarter move detection | Replace lightweight fingerprint with a stronger stable folder identity later. |

---

## 12. Summary

The key change in this redesign is conceptual:

- metadata is not a background side effect
- metadata is a **tracked subsystem attached to the library**

That leads to a better Phase 1:

- existing libraries are first-class
- restart recovery is explicit
- moved folders do not always lose metadata
- UI has a small but honest status surface
- command, runtime, query, and storage responsibilities stay readable

That is the version I would build.
