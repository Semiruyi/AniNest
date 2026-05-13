# Metadata Scraper Design Document

## 1. Background and Goals

### 1.1 Background
AniNest's Library page currently displays folder cards for the user's local video library. Card covers rely on local files such as `cover.jpg` and `folder.jpg`; if no cover image is manually placed, a default style is shown. For anime content, metadata such as titles, summaries, ratings, and air dates are completely missing, resulting in a bare-bones browsing experience.

### 1.2 Phase 1 Goals
**This phase only implements background auto-scraping** with the following core objectives:
- Automatically fetch anime metadata from Bangumi after adding a folder.
- **Strict matching**: Skip scraping when confidence is low; Correction fisrt.
- Library cards automatically refresh their covers: local covers take priority; Bangumi posters are used as fallback.
- Support **auto-exporting** metadata to the video folder (`tvshow.nfo` + `poster.jpg`), controlled by a user setting.
- Architecture预留扩展点 for future features such as manual search, per-episode NFO, and multiple data sources.

### 1.3 Scope
- **Data source**: Bangumi only. The architecture preseve `IMetadataProvider` for extensions.
- **Interaction mode**: Background auto-scraping only; **no manual search dialog in this phase**.
- **Content type**: Anime (Bangumi `type = 2`).
- **Scale**: Personal local library, typically 50-200 titles.

---

## 2. Overall Design

A new independent `Metadata` Feature is added as an **optional decoration layer** for `Library`. The two are loosely coupled via `folderPath`:

```
Library (existing)
  └── Load folder list
  └── Scan video files / local covers
  └── [New] Read associated FolderMetadata
  └── Assemble DTO → Bind EffectiveCoverPath → UI

Metadata (new)
  └── Clean folder name → Search keyword
  └── Bangumi API: Search → Strict match confirmation → Get details
  └── Download poster to local cache
  └── Save metadata JSON
  └── [Optional] Auto-export NFO + poster.jpg to video folder
```

**Key principles**:
- **Local cover priority**: User-placed `cover.jpg` always takes precedence over Bangumi posters.
- **Strict matching**: Auto-scraping silently skips when high-confidence conditions are not met.
- **Zero intrusion**: When no metadata exists, all existing Library behavior remains unchanged.

---

## 3. Architecture Design

### 3.1 Layered Structure

```
┌─────────────────────────────────────────────────────────────┐
│  UI Layer (View / ViewModel)                                │
│  Card cover binding, settings                               │
├─────────────────────────────────────────────────────────────┤
│  App Service Layer                                          │
│  LibraryAppService: Read metadata on load; Register tasks   │
│                         on add                              │
├─────────────────────────────────────────────────────────────┤
│  Metadata Domain Layer (Features/Metadata/Services)         │
│  ├─ IMetadataScraperService ── MetadataScraperCoordinator   │
│  │   ├─ MetadataTaskStore (Pending scrape queue)            │
│  │   ├─ MetadataWorker (Background single-thread consumer)  │
│  │   └─ MetadataScrapeIndex (Persistent index, prevents    │
│  │                            duplicate scraping)           │
│  ├─ IMetadataProvider ── BangumiMetadataProvider            │
│  ├─ IMetadataRepository ── MetadataRepository               │
│  ├─ MetadataImageCache                                      │
│  ├─ MetadataMatcher                                         │
│  └─ MetadataExporter                                        │
├─────────────────────────────────────────────────────────────┤
│  External Layer                                             │
│  Bangumi API (api.bgm.tv) / Local file system               │
└─────────────────────────────────────────────────────────────┘
```

### 3.2 Core Abstractions

| Abstraction | Type | Responsibility |
|-------------|------|----------------|
| `IMetadataScraperService` | Interface | Metadata scraping coordinator. Library registers/deletes folder tasks through it; internal scheduling is self-managed. |
| `MetadataScraperCoordinator` | Class | Coordinator implementation: maintains queue, single-thread worker, rate limiting, state tracking, cross-thread UI notification. |
| `MetadataScrapeIndex` | Class | Lightweight index (JSON): records scrape status and last attempt time per `folderPath` to prevent cross-session duplicate scraping. |
| `IMetadataProvider` | Interface | Data source abstraction: search + get details. Only Bangumi implementation in this phase. |
| `IMetadataRepository` | Interface | Local metadata read/write (JSON). |
| `MetadataImageCache` | Class | Poster download and local caching. |
| `MetadataMatcher` | Static class | Folder name cleaning, outputs search keywords. See [Matching Strategy Document](metadata-matching-strategy.md). |

### 3.3 Extensibility

When adding new data sources or features:
- New data source: Implement `IMetadataProvider`.
- Manual search dialog: Add a new window in the UI layer; expose `SearchCandidatesAsync` in `MetadataScraperCoordinator`.
- Per-episode NFO: Add `GetEpisodesAsync` to `IMetadataProvider`; extend `MetadataExporter` for per-episode export.
- Multi-size images: Extend `MetadataImageCache` caching strategy.

The Library layer (`LibraryAppService`, `FolderListItem`, card templates) **requires no changes**.

---

## 4. Data Flow

### 4.1 Library Load Flow (Read)

```
User opens Library
  → MainPageViewModel.LoadDataAsync()
    → LibraryAppService.LoadLibraryAsync()
      ├─ VideoScanner.ScanFolderAsync(path)
      │   Returns: videoCount, localCoverPath, videoFiles
      ├─ MetadataRepository.Get(path)
      │   Returns: FolderMetadata? (pure local file read, ms-level)
      └─ Assemble LibraryFolderDto(with Metadata)
    → CreateFolderItem(dto)
      → FolderListItem.EffectiveCoverPath
         Priority: localCoverPath > Metadata.LocalPosterPath > default cover
  → Bind to card template
```

**Design decision**: `LoadLibraryAsync` **only reads** existing metadata and **does not enqueue** scraping. This avoids re-registering tasks every time Library is opened.

### 4.2 Auto-Scraping Flow (Write, Background Async)

Following the coordinator pattern used by the thumbnail system. `LibraryAppService` registers tasks **when adding new folders**, and actual execution is scheduled internally by `MetadataScraperCoordinator`:

```
LibraryAppService.AddFolderAsync / AddFolderBatchAsync succeeds
  → _metadataScraperService.RegisterFolder(path, folderName)
    ├─ MetadataScrapeIndex.Get(path)
    │   ├─ status == Completed  → Skip (already has data)
    │   ├─ status == Failed && attempt within 7 days → Skip (avoid repeated retries)
    │   └─ Otherwise → Continue
    ├─ (If AutoScrapeMetadata == true)
    │   → Add to internal pending queue (MetadataTaskStore)
    │   → MetadataScrapeIndex.Update(path, status: Pending)
    │   → Wake background Worker (if not running)
    └─ (If AutoScrapeMetadata == false)
        → MetadataScrapeIndex.Update(path, status: Skipped)

MetadataScraperCoordinator (Background single-thread Worker)
  → Dequeue Pending task
  → MetadataScrapeIndex.Update(path, status: Scraping)
  → Call BangumiMetadataProvider.Search(keyword)
  → Strict match validation (see 4.4)
  │   ├─ All pass → Continue
  │   └─ Any fail → MetadataScrapeIndex.Update(path, status: Failed)
  │                 → Log → End this round
  → BangumiMetadataProvider.GetDetail(sourceId)
  → MetadataImageCache.Download(posterUrl, hash)
  → MetadataRepository.Save(metadata)
  → MetadataScrapeIndex.Update(path, status: Completed)
  → (If AutoExportMetadata == true)
  │   → MetadataExporter.ExportAsync(path)
  → Trigger FolderMetadataRefreshed event (via Dispatcher back to UI thread)
  → Wait 1000ms (rate limit)
  → Consume next task
```

**Alignment with thumbnail system**:
- `RegisterFolder` ≈ `RegisterCollection`: Library only registers, does not directly execute.
- `DeleteFolder` ≈ `DeleteCollection`: Deletion synchronously cleans up queue, state, and cache.
- Background Worker single-thread serial consumption ≈ `ThumbnailWorkerPool` (metadata only needs single thread due to rate limiting).
- State tracking (Pending / Scraping / Completed / Failed / Skipped) ≈ Thumbnail's `ThumbnailState`.
- Completion determined by disk file + index ≈ Thumbnail's index/thumbnail file existence.
- Cross-thread progress notification ≈ `ThumbnailProgressChanged`.

### 4.3 Rate Limiting and Fault Tolerance

- **Global rate limiting**: `BangumiMetadataProvider` uses an internal `SemaphoreSlim(1)`; all search/detail requests execute serially with a **1000ms** interval.
- **Retry strategy**: On network timeout/exception, exponential backoff retry up to 2 times (2s → 4s). HTTP 404 is not retried; marked as failed directly.
- **Failure handling**: Any scraping failure (no results, match rejection, network failure, image download failure) **silently skips**, logs, and does not pop up dialogs or block the UI.

### 4.4 Strict Matching Strategy

Auto-scraping must satisfy **all** of the following conditions; otherwise it gives up:

| Condition | Threshold | Description |
|-----------|-----------|-------------|
| Search returns non-empty | `candidates.Count > 0` | Bangumi returns at least one result. |
| Type is anime | `type == 2` | Fixed in request filter. |
| Title similarity | `>= 0.5` | Similarity between cleaned keyword and Bangumi `name_cn` (or `name`). Chinese uses LCS; English uses Levenshtein. |
| Non-short ambiguous word | Chinese `>= 3` chars / English `>= 6` chars | Rejects ambiguous short words like "Fate", "AB". |
| Year match (optional) | If keyword contains 4-digit year, Top1 year error `<= 1` | Enhances precision; not mandatory. |

### 4.5 Cross-Thread UI Refresh

`MetadataWorker` runs on a background thread and must not directly manipulate `ObservableCollection`. Notification chain:

```
MetadataScraperCoordinator (background thread)
  → FolderMetadataRefreshed?.Invoke(folderPath)
    → Application.Current.Dispatcher.BeginInvoke(...)
      → MainPageViewModel.OnFolderMetadataRefreshed(folderPath)
        → Find corresponding FolderListItem in FolderItems
        → Update item.Metadata = new data
        → EffectiveCoverPath automatically changes → Card refreshes cover
```

`FolderListItem.Metadata` uses `ObservableObject`'s `[ObservableProperty]`; changes automatically trigger `PropertyChanged`, and the card template binding responds.

---

## 5. UI and Settings

### 5.1 Card Display

The card template uses `FolderListItem.EffectiveCoverPath`:

1. **Has local cover** → Show user's local cover image (respects user choice).
2. **No local cover, has Bangumi poster** → Show Bangumi poster.
3. **Nothing at all** → Default placeholder.

**Not in this phase**: Title/rating text overlay on cards, unscraped badge, manual search dialog, context menu extensions.

### 5.2 Settings

Three new fields in `AppSettings`:

```csharp
public bool AutoScrapeMetadata { get; set; } = true;      // Auto background scraping after adding folder
public bool AutoExportMetadata { get; set; } = false;     // Auto-export NFO + poster to folder after scraping
public string? BangumiAccessToken { get; set; }           // Empty = anonymous calls
```

- `AutoScrapeMetadata` defaults to `true`: Works out of the box for new users.
- `AutoExportMetadata` defaults to `false`: Exporting modifies user folder contents; disabled by default until user explicitly enables.
- Settings page reserves input controls for these three fields; in this phase they can temporarily use simple `InputBox` or configuration items.

---

## 6. Data Persistence

### 6.1 Metadata Body

- **Location**: `{AppPaths.CacheDirectory}/metadata/{folderPathHash}.json`
- **Format**: Independent JSON, one per folder.
- **Hash**: First 16 chars of `MD5(folderPath)`.
- **Content**: Serialized `FolderMetadata`.

```csharp
public class FolderMetadata
{
    public string FolderPath { get; set; } = "";
    public string? Title { get; set; }           // name_cn fallback name
    public string? OriginalTitle { get; set; }   // name
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

### 6.2 Poster Images

- **Location**: `{AppPaths.CacheDirectory}/metadata/posters/{folderPathHash}.jpg`
- **Source**: Bangumi `images.common` (fallback `images.medium`).
- **Strategy**: Permanent cache; Bangumi data is static and unchanging.

### 6.3 Scrape Index (Duplicate Prevention)

- **Location**: `{AppPaths.CacheDirectory}/metadata/index.json`
- **Purpose**: Records scrape status and last attempt time per folder to prevent cross-session duplicate scraping and repeated retries of known failures.
- **Format**:

```json
{
  "C:\\Anime\\Attack on Titan": {
    "status": "Completed",
    "sourceId": "55770",
    "lastAttemptAt": "2026-05-13T10:00:00Z"
  },
  "C:\\Anime\\Season 1": {
    "status": "Failed",
    "lastAttemptAt": "2026-05-13T10:05:00Z"
  }
}
```

- **Retry cooldown**: Entries with `Failed` status are not re-enqueued within 7 days. After 7 days, they may be retried in future versions (e.g., if Bangumi data has been supplemented).
- **Deletion cleanup**: `DeleteFolder` synchronously removes the corresponding entry from the index.

### 6.4 Auto-Export to Folder

Controlled by `AutoExportMetadata` setting. After scraping succeeds, if enabled, `MetadataExporter` automatically generates in the video folder:

| File | Description |
|------|-------------|
| `tvshow.nfo` | Kodi/XBMC standard XML. Contains title, originaltitle, plot, genre, studio, year, rating, episode, uniqueid(bangumi), etc. |
| `poster.jpg` | Copied from local cache poster. |

**Overwrite strategy**: If file already exists, silently overwrite (application-generated metadata).
**Failure handling**: On read-only/no-permission/path offline (NAS disconnected), log without popup blocking.

#### NFO Example

```xml
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<tvshow>
    <title>Attack on Titan</title>
    <originaltitle>進撃の巨人</originaltitle>
    <showtitle>Attack on Titan</showtitle>
    <season>1</season>
    <episode>25</episode>
    <plot>107 years ago...(Bangumi summary)</plot>
    <genre>Anime</genre>
    <genre>Action</genre>
    <studio>WIT STUDIO</studio>
    <premiered>2013-04-07</premiered>
    <year>2013</year>
    <rating>8.4</rating>
    <votes>25000</votes>
    <uniqueid type="bangumi" default="true">55770</uniqueid>
</tvshow>
```

### 6.5 Lifecycle Management

- **Add folder**: `AddFolderAsync` / `AddFolderBatchAsync` calls `RegisterFolder`; coordinator decides whether to enqueue.
- **Delete folder**: `LibraryAppService.DeleteFolderAsync` calls `_metadataScraperService.DeleteFolder`, which synchronously cleans up:
  - `MetadataRepository.Delete`
  - `MetadataImageCache.Delete`
  - `MetadataScrapeIndex.Delete`
- **Rename folder**: Does not trigger re-scraping. Bangumi data is static; no need to refresh due to renaming.

---

## 7. Responsibility Separation

| Component | Responsible For | Explicitly NOT Responsible For |
|-----------|-----------------|--------------------------------|
| `BangumiMetadataProvider` | Bangumi HTTP requests, JSON mapping, global rate limiting. | Local storage, UI, matching decisions. |
| `MetadataRepository` | Local metadata JSON read/write/delete. | Network, images, business logic. |
| `MetadataScrapeIndex` | Index JSON read/write/delete (status/time). | API calls, images. |
| `MetadataImageCache` | Poster download, local cache path management. | Metadata parsing. |
| `MetadataMatcher` | Folder name cleaning. | I/O. |
| `MetadataScraperCoordinator` | Orchestrates auto-scraping full flow: register → index check → enqueue → single-thread consumption → rate limiting → strict matching → details → cache → save → index update → optional export → UI notification. | Does not directly manipulate UI controls; only notifies via Dispatcher events. |
| `MetadataExporter` | NFO serialization, poster copy to video folder. | API calls. |
| `LibraryAppService` | Reads metadata into DTO on load; RegisterFolder on add; DeleteFolder on delete. | Bangumi API. |
| `MainPageViewModel` | Subscribes to `FolderMetadataRefreshed` event, updates corresponding FolderListItem on UI thread. | Does not directly read/write files or initiate network requests. |

---

## 8. Interface Draft

### 8.1 Provider

```csharp
public interface IMetadataProvider
{
    Task<IReadOnlyList<MetadataSearchCandidate>> SearchAsync(string keyword, CancellationToken ct = default);
    Task<FolderMetadata?> GetDetailAsync(string sourceId, CancellationToken ct = default);
}

public record MetadataSearchCandidate(
    string SourceId,
    string Title,
    string? OriginalTitle,
    string? Year,
    double? Rating,
    string? PosterUrl,
    string? Summary);
```

### 8.2 Repository

```csharp
public interface IMetadataRepository
{
    FolderMetadata? Get(string folderPath);
    void Save(FolderMetadata metadata);
    void Delete(string folderPath);
}
```

### 8.3 Scraper Service (Coordinator Interface)

```csharp
public interface IMetadataScraperService
{
    /// <summary>
    /// Register folder to coordinator. Coordinator internally checks index and
    /// AutoScrapeMetadata to decide whether to enqueue.
    /// Only called when adding new folders; not called in LoadLibraryAsync.
    /// </summary>
    void RegisterFolder(string folderPath, string folderName);

    /// <summary>
    /// Delete folder: synchronously cleans up queue, cache, index, and metadata JSON.
    /// </summary>
    void DeleteFolder(string folderPath);

    MetadataScrapeState GetFolderState(string folderPath);

    event EventHandler<FolderMetadataRefreshedEventArgs>? FolderMetadataRefreshed;
}

public enum MetadataScrapeState
{
    Completed,   // Index or metadata JSON exists
    Pending,     // Enqueued, waiting for execution
    Scraping,    // Currently scraping
    Failed,      // Attempted but failed (no retry within 7 days)
    Skipped      // AutoScrapeMetadata disabled, not enqueued
}

public class FolderMetadataRefreshedEventArgs : EventArgs
{
    public string FolderPath { get; }
}
```

`MetadataScraperCoordinator` internal responsibilities:
1. `MetadataTaskStore`: In-memory queue recording Pending tasks.
2. `MetadataWorker`: Background single thread, serially consumes queue, executes Search → Match → Detail → Cache → Save → Export.
3. Sleeps 1000ms after each API request for Bangumi rate limiting.
4. `MetadataScrapeIndex`: Cross-session persistence; Failed items are not re-enqueued within 7 days.
5. After completion, triggers `FolderMetadataRefreshed` via `Dispatcher.BeginInvoke`.

### 8.4 Exporter

```csharp
public class MetadataExporter
{
    /// <summary>
    /// Export cached metadata as tvshow.nfo + poster.jpg to folderPath.
    /// Throws IOException if folder is read-only or path does not exist;
    /// logged by coordinator.
    /// </summary>
    public Task ExportAsync(string folderPath, CancellationToken ct = default);
}
```

---

## 9. Phase 1 Implementation Steps

### Step 1: Infrastructure (1 day)
1. Define `FolderMetadata` model, `IMetadataProvider`, `IMetadataRepository`.
2. Implement `MetadataRepository` (JSON read/write).
3. Implement `MetadataScrapeIndex` (index JSON read/write, including 7-day cooldown logic).
4. Add `MetadataDirectory`, `MetadataPosterDirectory` to `AppPaths`.
5. Add `AutoScrapeMetadata`, `AutoExportMetadata`, `BangumiAccessToken` to `AppSettings`.
6. Register new services in `ServiceRegistration`.

### Step 2: Bangumi Integration and Coordinator (1 day)
1. Implement `BangumiMetadataProvider` (Search + GetDetail + global rate limiting + retry).
2. Implement `MetadataImageCache` (poster download).
3. Implement `MetadataMatcher` (see [Matching Strategy Document](metadata-matching-strategy.md)).
4. Implement `MetadataScraperCoordinator`:
   - `RegisterFolder` / `DeleteFolder`
   - `MetadataTaskStore` + `MetadataWorker` (single-thread queue consumption)
   - Strict match validation + rate limiting
   - `FolderMetadataRefreshed` event (with Dispatcher)
5. Implement `MetadataExporter` (NFO XML + poster copy).
6. Unit tests: Bangumi API DTO mapping, Matcher cleaning cases, similarity calculation, index cooldown logic.

### Step 3: Library Integration (0.5 day)
1. Add `Metadata` and `EffectiveCoverPath` to `LibraryFolderDto` / `FolderListItem`.
2. `LibraryAppService.LoadLibraryAsync` reads `MetadataRepository` (read-only, no enqueue).
3. `LibraryAppService.AddFolderAsync` / `AddFolderBatchAsync` calls `_metadataScraperService.RegisterFolder`.
4. `LibraryAppService.DeleteFolderAsync` calls `_metadataScraperService.DeleteFolder`.
5. `MainPageViewModel` subscribes to `FolderMetadataRefreshed`, updates corresponding card on UI thread.
6. Card template binds `EffectiveCoverPath`.

### Step 4: Settings and End-to-End Testing (0.5 day)
1. Integrate `AutoScrapeMetadata` / `AutoExportMetadata` / `BangumiAccessToken` into settings page.
2. End-to-end testing: Add 20-50 folders, verify:
   - Index correctness (Completed / Failed / Skipped)
   - Rate limiting (1s interval)
   - Cover refresh
   - NFO export
   - Failed items are not retried after restart

---

## 10. Future Evolution

Not in this phase, but architecture预留 extension points:

| Feature | Evolution Description | Required Extensions |
|---------|----------------------|---------------------|
| **Manual search dialog** | User right-clicks card, popup shows candidate list for manual selection. | UI: Add `MetadataSearchDialog`; Coordinator: expose `SearchCandidatesAsync` for dialog use. |
| **Multiple data sources** | Support TMDb, Douban, etc. | Add `TmdbMetadataProvider` : `IMetadataProvider`. |
| **Per-episode NFO** | Generate corresponding `.nfo` per video file with episode title, summary, thumbnail. | Provider: add `GetEpisodesAsync`; Exporter: extend per-episode export; parse episode number from filename. |
| **Multi-size images** | Cache and export `fanart.jpg`, `banner.jpg`, etc. | Extend `MetadataImageCache` for multi-size; `MetadataExporter` adds file outputs. |
| **Refresh metadata** | Force re-fetch latest data for already-matched entries. | `MetadataScraperCoordinator` add `RefreshAsync`, skip search and re-fetch details by existing `sourceId`; reset index cooldown. |
| **Unscraped filter** | Library filter bar adds "Unscraped" option. | `LibraryFilter` add enum; `MatchesSelectedFilter` add logic. |
| **Bangumi user data sync** | After Bangumi login, sync "watching/watched" status to WatchStatus. | OAuth login; call Bangumi collection API. |
