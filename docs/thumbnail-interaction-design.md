# Thumbnail Interaction and Collection-Aware Scheduling Design

## Goal

This document defines a new thumbnail generation experience for `AniNest`.

The old queue strategy is centered around folder order plus a baked-in integer priority. That works for simple background generation, but it does not scale well once the app grows toward:

- richer library categories such as `Watching`, `Unsorted`, `Completed`, `Favorites`, `Dropped`, and `All`
- future user-defined categories
- stronger user control over which thumbnails should appear first
- smoother transitions between library browsing and player usage

The new design should make thumbnail work feel responsive to user intent instead of silently following an opaque global order.

In short:

> Thumbnail generation should follow what the user is looking at, touching, or about to need.

## Target User Experience

### Default behavior

- The library still opens quickly even if many thumbnails are missing.
- Existing covers and cached thumbnails appear immediately.
- Missing thumbnails fill in gradually without making the app feel busy or noisy.

### When browsing the library

- Thumbnail work should favor the collection the user is currently focused on.
- Opening a category or custom collection should make that area feel "alive" quickly.
- Users should not need to understand internal queue order to get the result they want.

### When using the player

- The current episode should have the highest thumbnail attention.
- Nearby episodes should become ready sooner than unrelated background content.
- Playback smoothness remains more important than background completion speed.

### When manually managing thumbnails

- Users can explicitly tell the app to generate thumbnails for a single video or for a visible group.
- Manual actions should feel immediate and trustworthy.
- The UI should communicate what the thumbnail system is doing now, not only aggregate counts.

## Why the Old Strategy Should Be Replaced

The current model is based on static numeric priority computed during enqueue time.

That has several problems:

- it is tied to folder order, which is too narrow for future category-based library views
- it does not naturally reflect changing user focus
- it is difficult to explain in the UI
- it encourages incremental tweaking instead of a clearer intent-based interaction model

For the next stage of the app, the thumbnail system should stop thinking in terms of "which folder entered first" and start thinking in terms of "what the user needs next".

## Design Principles

1. User intent is stronger than background order.
2. Current focus is stronger than historical enqueue order.
3. Collections are first-class; folders are only one kind of collection.
4. Manual actions should be explicit and reversible.
5. The scheduling model should stay simple enough to reason about and test.
6. The design should tolerate future system categories and user-defined categories without rework.

## Core Model Shift

The key architectural shift is:

- from `folder-priority queue`
- to `collection-aware, intent-driven scheduling`

This means the thumbnail system should no longer treat a folder as the only meaningful unit of focus.

Instead, it should work with:

- a collection the user is currently focused on
- one or more videos that are currently important
- a low-pressure background backlog for everything else

## Collection-Centric Library Model

The library is expected to grow beyond raw folders.

Examples:

- system categories: `Watching`, `Unsorted`, `Completed`, `Favorites`, `Dropped`, `All`
- user-defined categories
- folder-based views
- future virtual views such as temporary filters or search results

To support this cleanly, thumbnail scheduling should depend on a collection abstraction rather than on folder-specific rules.

Example conceptual model:

```csharp
public enum LibraryCollectionKind
{
    Folder,
    SystemCategory,
    UserCategory,
    Virtual
}

public sealed record LibraryCollectionRef(
    string Id,
    LibraryCollectionKind Kind,
    string Name);
```

The thumbnail layer does not need to know how a collection was created. It only needs to know:

- collection identity
- collection kind
- which videos belong to it right now

## New Interaction Model

The new model should distinguish between:

### 1. Focus

What the user is currently looking at.

Examples:

- opening the `Watching` category
- opening a folder card
- entering the player for a series

Focus should guide near-term scheduling, but it does not permanently reorder the whole backlog.

### 2. Boost

An explicit user request to do something sooner.

Examples:

- right-click a playlist item and choose "Generate this thumbnail first"
- right-click a collection card and choose "Prioritize this group"

Boost should feel stronger than passive focus and should take effect quickly.

### 3. Background Fill

The rest of the library that is not actively in front of the user.

Background fill exists to keep the cache improving over time, but it should not dominate foreground interaction.

## Recommended Scheduling Shape

Instead of one globally sorted list with one opaque `Priority` integer, use two logical lanes:

### Foreground lane

For work that is close to user intent.

Typical sources:

- manually boosted single video
- current playback video
- nearby playback videos
- manually boosted collection
- currently focused collection

### Background lane

For low-pressure completion work across the rest of the library.

Workers should always prefer the foreground lane. Only when it is empty should they consume background work.

This design is easier to explain than a single mutable ranking and is much easier to extend once categories and user collections arrive.

## Recommended Intent Levels

Within the foreground lane, the system can still keep a small fixed intent order.

Suggested levels:

1. `ManualSingle`
2. `PlaybackCurrent`
3. `PlaybackNearby`
4. `ManualCollection`
5. `FocusedCollection`
6. `BackgroundFill`

This should be modeled as stable intent classes, not as ad hoc arithmetic.

## User-Facing Interaction Plan

### Library collection actions

For any collection card, category entry, or future grouped surface:

- prioritize this group
- regenerate thumbnails for this group
- clear thumbnail cache for this group
- pause background generation for this group (optional later)

The wording can evolve with the UI, but the underlying behavior should be collection-based rather than folder-specific.

### Player playlist actions

For any single video item:

- prioritize this video
- regenerate this video's thumbnails
- delete this video's thumbnails
- prioritize next few videos

This is the most natural home for single-video thumbnail management because the player already exposes concrete episode items and per-item thumbnail status.

### Automatic player behavior

Entering the player should automatically create a small foreground working set:

- current video
- next `3-5` videos
- optionally previous video

This gives the player a strong "ready where it matters" feel without trying to regenerate the whole series at once.

## Status and Feedback

The title-bar background-task surface should evolve from a pure progress indicator into a lightweight status summary.

Good examples:

- `Generating: Episode 08`
- `Up next: Episode 09-12`
- `Background fill: 32 / 180`

The important shift is from "how many are done" to "what is the system doing for me right now".

## Implementation Approach

### 1. Replace folder-first APIs with collection-aware APIs

Current folder-specific entry points should be treated as transitional.

The scheduling-facing API should move toward concepts such as:

- register a collection and its video membership
- focus a collection
- boost a collection
- boost a video
- remove or invalidate videos

Possible conceptual surface:

```csharp
void RegisterCollection(LibraryCollectionRef collection, IReadOnlyList<string> videoPaths);
void FocusCollection(string collectionId);
void BoostCollection(string collectionId);
void BoostVideo(string videoPath);
```

The exact API can stay smaller in the first implementation, but the semantics should head in this direction.

### 1.1 Transitional API strategy

The current code already depends on folder-oriented APIs such as:

- `IThumbnailGenerator.EnqueueFolder(...)`
- `IThumbnailGenerator.DeleteForFolder(...)`
- `ILibraryAppService.LoadLibraryAsync(...)`

These do not need to disappear immediately. The safer migration path is:

1. keep the existing folder-oriented APIs working
2. add collection-aware APIs beside them
3. move feature code toward the new APIs
4. demote old folder-first APIs into compatibility wrappers

That keeps the first refactor smaller and avoids forcing the library page, player flow, and persistence model to change all at once.

### 2. Introduce a collection resolver boundary

Thumbnail infrastructure should not own category logic.

Instead, a separate boundary should resolve:

- which videos belong to a collection
- how collections are named and identified
- which collection is currently visible or active

That keeps future library evolution out of the thumbnail coordinator.

### 2.1 Recommended boundary shape

The resolver boundary should live closer to the library feature than to thumbnail infrastructure.

Its job is to answer questions like:

- what collections currently exist
- which videos belong to a given collection
- which collection is active on screen
- which collection should be treated as the source of a user command

Possible conceptual surface:

```csharp
public interface ILibraryCollectionResolver
{
    IReadOnlyList<LibraryCollectionRef> GetCollections();
    IReadOnlyList<string> ResolveVideoPaths(string collectionId);
}
```

The final interface may need async methods once collections become more dynamic, but the important part is the dependency direction:

- library knows how collections are built
- thumbnail scheduling only consumes resolved collection membership

### 3. Keep storage and rendering layers unchanged where possible

This redesign is mostly about scheduling and interaction, not cache format.

Existing work such as:

- bundled thumbnail storage
- frame index lookup
- decode fallback strategy
- player-active throttling

can remain in place and be reused.

The main change is the policy that decides which job should be started next.

### 4. Keep the first scheduler modest

The first implementation does not need a heavy job system.

A practical first version can still use:

- one task registry keyed by video path
- one foreground candidate set
- one background candidate set
- a small worker gate based on existing performance policy

The point is not to build a complex scheduler. The point is to make the scheduler express user intent clearly.

### 4.1 Suggested internal state model

The current `ThumbnailTask` model is close to what is needed for rendering and persistence, but it is missing scheduling intent.

A practical first extension would be:

```csharp
public enum ThumbnailWorkIntent
{
    BackgroundFill,
    FocusedCollection,
    ManualCollection,
    PlaybackNearby,
    PlaybackCurrent,
    ManualSingle
}

public sealed class ThumbnailTask
{
    public string VideoPath { get; init; } = "";
    public string Md5Dir { get; set; } = "";
    public ThumbnailState State { get; set; } = ThumbnailState.Pending;
    public int TotalFrames { get; set; }
    public long MarkedForDeletionAt { get; set; }

    public ThumbnailWorkIntent Intent { get; set; } = ThumbnailWorkIntent.BackgroundFill;
    public string? SourceCollectionId { get; set; }
    public long IntentUpdatedAtUtcTicks { get; set; }
}
```

This is intentionally modest:

- keep task persistence and rendering identity as they are
- add explicit scheduling metadata
- stop baking ordering into one arithmetic `Priority`

### 4.2 Suggested scheduler responsibilities

`ThumbnailGenerator` should remain the central coordinator, but its scheduling responsibilities should be reframed:

- maintain task registry by video path
- maintain collection membership lookups
- mark tasks as foreground or background candidates
- pick the next pending task by intent lane
- apply existing performance policy and player throttling
- expose current foreground target for lightweight UI status

What it should not do:

- understand library category business rules
- own category naming
- encode folder order as a permanent global ranking

### 4.3 Foreground selection rules

The first scheduler should stay deterministic and easy to debug.

Suggested next-task selection order:

1. pending tasks with `ManualSingle`
2. pending tasks with `PlaybackCurrent`
3. pending tasks with `PlaybackNearby`
4. pending tasks with `ManualCollection`
5. pending tasks with `FocusedCollection`
6. pending tasks with `BackgroundFill`

Within the same intent level, prefer:

1. most recently boosted or focused
2. currently visible collection membership when known
3. stable file-path order as a final tie-breaker

This gives the system predictable behavior without reintroducing opaque weighting math.

## Mapping to the Current Codebase

This redesign can fit the current project structure with a fairly clean split.

### Infrastructure

Keep ownership here:

- thumbnail task registry
- task persistence
- render execution
- decode fallback
- concurrency gating
- scheduler state reporting

Primary module:

- `Infrastructure/Thumbnails/ThumbnailGenerator`

### Library feature

Own here:

- collection identity
- collection membership resolution
- collection-focused commands from the library UI
- mapping current library surface to collection refs

Likely modules:

- `Features/Library/Services/LibraryAppService`
- a future `LibraryCollectionResolver`
- future category view models

### Player feature

Own here:

- current video focus
- nearby video working set
- item-level manual thumbnail commands

Likely modules:

- `Features/Player/Services/PlayerAppService`
- `Features/Player/PlaylistViewModel`
- `Features/Player/Services/PlayerThumbnailSyncService`

### Composition root

Service registration should continue to wire the scheduler as a singleton, but it will eventually need the collection resolver boundary once category features arrive.

Likely touch point:

- `CompositionRoot/ServiceRegistration`

## Rollout Plan

### Current status

Already in place:

- intent-aware scheduler foundation
- collection registration and focus/boost APIs
- playback-window boosting with current and nearby videos
- ready-item skipping inside the playback window
- stale playback worker protection via keep-target logic

Still pending:

- library-side collection actions beyond the current folder-based bridge
- player-side single-video manual thumbnail actions
- a structural refactor of `ThumbnailGenerator`

### Structural refactor plan for `ThumbnailGenerator`

The current implementation has already moved toward intent-aware scheduling, but the class is still carrying too many responsibilities at once.

Today it mixes:

- task registry and collection membership storage
- intent application and playback-window promotion rules
- worker lifecycle and cancellation/preemption
- queue loop and next-task selection
- thumbnail index persistence and cache-directory cleanup
- status aggregation and UI-facing progress reporting

That shape was acceptable while the feature was still settling, but it will become harder to reason about as the app adds richer library categories, more manual thumbnail actions, and more player-driven scheduling behavior.

The goal of the refactor is not to replace `ThumbnailGenerator` as the public coordinator.

The goal is to keep it as the central entry point while moving its internal responsibilities into smaller, testable collaborators.

#### Refactor target shape

After the refactor, `ThumbnailGenerator` should primarily do three things:

- receive external commands through `IThumbnailGenerator`
- coordinate the internal thumbnail components
- own high-level startup and shutdown lifecycle

The detailed logic should move into focused components under `Infrastructure/Thumbnails`.

Suggested internal split:

- `ThumbnailTaskStore`
  - owns registered tasks, lookup dictionaries, collection membership, and task state transitions
- `ThumbnailPlaybackWindowCoordinator`
  - owns playback-window intent application plus stale-worker/preemption decision input for playback changes
- `ThumbnailWorkIntentPriority`
  - owns the stable intent ranking rules shared by scheduling decisions
- `ThumbnailWorkerPreemption`
  - owns worker preemption selection rules for incoming higher-priority work and stale playback workers
- `ThumbnailWorkerPool`
  - owns active worker tracking, start/cancel/requeue behavior, and worker completion draining
- `ThumbnailIndexRepository`
  - owns loading and saving the task index plus cache-directory cleanup and expiry deletion
- `ThumbnailGenerationRunner`
  - owns one-task execution, decode-strategy fallback, and render invocation
- `ThumbnailWorkerExecutionHost`
  - owns the lifecycle of a single running worker, including execute, cancel/requeue handling, and finalize/update hooks
- `ThumbnailStatusTracker`
  - owns aggregated ready/total counts, current foreground target, and status snapshot composition

These names are descriptive rather than mandatory. The important part is the responsibility boundary.

#### Recommended directory layout

The current `Infrastructure/Thumbnails` module is still flat.

That was fine when the module only contained a few files, but the refactor described above will add enough internal structure that a single directory will become noisy.

The recommended change is modest:

- keep `Thumbnails` as one infrastructure module
- do not reorganize the whole repository around this work
- add a small set of subdirectories inside `Infrastructure/Thumbnails` so the thumbnail pipeline can be read by responsibility

Suggested layout:

```text
Infrastructure/Thumbnails/
  Abstractions/
    IThumbnailGenerator.cs
    IVideoScanner.cs

  Models/
    LibraryCollectionRef.cs
    ThumbnailAccelerationMode.cs
    ThumbnailGenerationStatusSnapshot.cs
    ThumbnailGeneratorWorker.cs
    ThumbnailPerformanceMode.cs
    ThumbnailState.cs
    ThumbnailWorkIntent.cs

  Scheduling/
    ThumbnailGenerator.cs
    ThumbnailPlaybackWindowCoordinator.cs
    ThumbnailPlaybackWindowUpdate.cs
    ThumbnailTaskStore.cs
    ThumbnailWorkIntentPriority.cs
    ThumbnailWorkerPool.cs
    ThumbnailWorkerPreemption.cs

  Execution/
    ThumbnailDecodeStrategy.cs
    ThumbnailGenerationRunner.cs
    ThumbnailRenderer.cs
    ThumbnailWorkerExecutionHost.cs

  Storage/
    ThumbnailBundle.cs
    ThumbnailFrameIndex.cs
    ThumbnailIndex.cs
    ThumbnailIndexRepository.cs

  Scanning/
    VideoScanner.cs
```

This layout is intentionally conservative:

- `Scheduling` contains the coordinator and its internal collaborators
- `Execution` contains render-time and decode-strategy logic
- `Storage` contains bundle/index/cache persistence logic
- `Models` contains shared enums, records, and small state holders
- `Abstractions` contains public interfaces consumed outside the thumbnail module
- `Scanning` keeps video discovery separate from scheduling concerns

The exact file split can evolve. The important part is to avoid a directory where public interfaces, state enums, cache persistence, worker management, and render execution all sit side by side without a visible boundary.

This should stay a thumbnail-local cleanup, not a repository-wide taxonomy exercise.

#### Boundary rules

To keep the split meaningful, each component should follow a narrow role:

- scheduling code should not delete directories or save the index
- worker-pool code should not know library collection semantics
- persistence code should not decide intent priority
- intent code should not directly own background loop timing
- UI-facing status reporting should read scheduler state, not compute queue policy inline across the whole coordinator

If a method needs to both mutate task intent and perform filesystem cleanup, that is a signal that the boundary is still too blurred.

#### Recommended extraction order

The refactor should be incremental. Do not try to split everything in one pass.

Recommended order:

1. extract `ThumbnailIndexRepository`
2. extract `ThumbnailTaskStore`
3. extract shared scheduling rules such as intent ranking and worker preemption
4. extract playback-window intent coordination
5. extract `ThumbnailWorkerPool`
6. extract `ThumbnailGenerationRunner`
7. optionally extract `ThumbnailStatusTracker` if status composition still feels noisy after the earlier steps

This order is intentional:

- index and cleanup logic are relatively isolated and low-risk
- task-store extraction reduces the largest concentration of mutable state first
- intent and scheduler extraction then makes the new interaction model visible in code structure
- worker-pool extraction comes later because it is tightly coupled to cancellation and lifecycle behavior

#### First-step implementation scope

The safest first implementation step is to extract persistence and cache-cleanup behavior without changing scheduling semantics.

That first step should move out:

- loading the thumbnail index at startup
- saving the index after task mutations
- deleting expired thumbnail directories
- deleting temp and backup thumbnail directories
- deleting per-video thumbnail directories during reset or removal

Expected result of the first step:

- no behavior change in library or player flows
- a smaller `ThumbnailGenerator`
- clearer separation between in-memory scheduling logic and filesystem maintenance

#### Second-step implementation scope

After persistence is separated, extract task and collection state management.

That step should move out:

- task registration by video path
- collection registration and membership replacement
- reverse lookups from video to collection ids
- task state transitions such as `Pending`, `Generating`, `Ready`, and `Failed`
- deletion marking metadata and task counters

Expected result of the second step:

- `ThumbnailGenerator` stops directly managing multiple mutable dictionaries
- intent and scheduling code can operate on a smaller state surface
- unit tests can target task-store behavior without running the queue loop

#### Success criteria for the refactor

The refactor is successful when:

- `ThumbnailGenerator` reads like an orchestrator instead of a god object
- intent-driven scheduling is visible in the class structure, not only in enum values
- player-triggered boosts and worker preemption remain easy to trace
- storage and cleanup changes can be made without reopening scheduling code
- future collection types can be added without increasing coordinator complexity again

### Phase A: Scheduler foundation

Goal:

- remove arithmetic folder priority
- introduce intent-aware scheduling fields
- preserve existing persistence and rendering behavior

Work:

- add `ThumbnailWorkIntent`
- add collection registration data structures
- replace `SortQueue()` logic with lane-based next-task selection
- keep `ThumbnailPerformancePolicy` unchanged

Expected result:

- no user-facing category UI yet
- cleaner coordinator internals
- player and manual commands can be layered on top safely

### Phase B: Player-first interaction

Goal:

- make the player feel immediately smarter

Work:

- add playback-window boost API
- on player enter, mark current and nearby videos as foreground
- expose current foreground target in scheduler status
- extend title-bar status text to mention active target

Expected result:

- the currently played series becomes thumbnail-ready faster
- behavior improves even before library categories ship

### Phase C: Library collection actions

Goal:

- make manual thumbnail control visible in the library

Work:

- introduce collection refs in the library layer
- add collection-level commands such as prioritize, regenerate, clear cache
- wire library focus events to `FocusCollection(...)`

Expected result:

- the app can treat folders and future system categories through the same scheduling contract
- the library UI can expose manual collection actions without exposing scheduler internals

### Phase D: Rich category support

Goal:

- support system categories and user-defined categories without scheduler redesign

Work:

- introduce collection resolver service
- allow non-folder collections to register and refresh membership
- add user-category and system-category surfaces

Expected result:

- thumbnail scheduling becomes a reusable platform capability rather than a folder-page behavior

## Test Strategy

The tests should shift from checking numeric priority order toward checking intent behavior.

Recommended unit-test focus:

- manual single boost outranks background work
- playback current outranks playback nearby
- focused collection outranks background fill
- removing a collection does not remove shared video tasks incorrectly
- player-active throttling still blocks or limits starts according to current policy
- legacy folder registration still seeds background work correctly

This keeps tests aligned with user-facing semantics rather than with implementation arithmetic.

## Open Questions

These do not block the architecture, but they should be answered during implementation:

- can one video belong to multiple collections at once in the scheduler, and if so how is its current dominant intent chosen
- should manual boosts expire automatically after some time or only when replaced by stronger intent
- should collection focus be updated by navigation alone or only by explicit visible-surface activation
- how much collection membership should be persisted versus rebuilt at startup

The first version can answer these conservatively and still gain most of the user-experience benefit.

## Recommended "Do Not Do Yet" List

To keep the first implementation disciplined, the following should be explicitly deferred:

- persist foreground intent across app restarts
- speculative generation based on hover
- continuously re-ranking tasks from scroll position
- one-off special priority rules for each future category
- a separate standalone job-manager window

These would add complexity before the new scheduling contract has proved itself.

## Non-Goals

This design does not aim to provide in the first version:

- a full thumbnail job manager UI
- per-category concurrency tuning
- complex speculative generation based on hover
- highly dynamic reordering based on tiny pointer movements

The design should remain calm, predictable, and easy to evolve.

## Summary

The new thumbnail experience should be built around a simple idea:

- the app knows what collection the user is in
- the app knows which video the user cares about right now
- foreground intent wins over global backlog order

That gives `AniNest` a thumbnail system that can grow naturally from folder browsing into system categories, user-defined categories, and richer library organization without rewriting the scheduling model again.
