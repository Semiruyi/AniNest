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

## Suggested Public API Evolution

The public thumbnail coordinator surface should evolve in a way that is additive first.

### Current state

Today the public interface mainly supports:

- enqueue folder content
- delete folder content
- query thumbnail state and bytes
- react to player-active mode

### Recommended next shape

The next shape should add intent-aware methods while keeping old ones temporarily:

```csharp
public interface IThumbnailGenerator
{
    void RegisterCollection(LibraryCollectionRef collection, IReadOnlyCollection<string> videoPaths);
    void RemoveCollection(string collectionId);
    void FocusCollection(string collectionId);
    void BoostCollection(string collectionId);
    void BoostVideo(string videoPath);
    void BoostPlaybackWindow(IReadOnlyList<string> orderedVideoPaths, int currentIndex, int lookaheadCount);
}
```

Notes:

- `RegisterCollection(...)` replaces the narrow idea that only folders can seed work
- `FocusCollection(...)` is for passive screen context
- `BoostCollection(...)` and `BoostVideo(...)` are for explicit user commands
- `BoostPlaybackWindow(...)` gives the player a direct way to express "current plus nearby"

The old `EnqueueFolder(...)` can initially translate into:

- register a folder collection
- seed tasks as `BackgroundFill`

## Suggested UI Contract

The UI should send intent, not scheduling math.

That means:

- library page enters category `Watching` -> `FocusCollection("category:watching")`
- right-click a collection card -> `BoostCollection(collectionId)`
- player opens episode list -> `BoostPlaybackWindow(...)`
- right-click a playlist item -> `BoostVideo(videoPath)`

The scheduler decides how these intents interact with the current worker gate and pending tasks.

## Rollout Plan

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

## Concrete Phase A Task Breakdown

Phase A should be kept intentionally narrow. The goal is to replace the scheduler model without pulling category UI into the same change.

### Files that should change in Phase A

Primary:

- `src/AniNest/Infrastructure/Thumbnails/IThumbnailGenerator.cs`
- `src/AniNest/Infrastructure/Thumbnails/ThumbnailGenerator.cs`
- `src/AniNest/Infrastructure/Thumbnails/ThumbnailIndex.cs`
- `src/Tests/Model/ThumbnailGeneratorTests.cs`

Likely:

- `src/Tests/Model/ThumbnailPerformancePolicyTests.cs`

Optional if a new dedicated model file is introduced:

- `src/AniNest/Infrastructure/Thumbnails/ThumbnailWorkIntent.cs`
- `src/AniNest/Infrastructure/Thumbnails/LibraryCollectionRef.cs`

### Phase A implementation steps

#### Step A1: Introduce intent and collection model types

Add small model types for:

- `ThumbnailWorkIntent`
- `LibraryCollectionKind`
- `LibraryCollectionRef`

At this stage these can stay near `Infrastructure/Thumbnails` even if they later move to a more shared location.

#### Step A2: Extend task state with scheduling metadata

Update `ThumbnailTask` so it no longer depends on arithmetic priority.

Remove:

- `Priority`

Add:

- `Intent`
- `SourceCollectionId`
- `IntentUpdatedAtUtcTicks`

This is the minimum state needed to move from ranking math to intent-aware selection.

#### Step A3: Keep task persistence intentionally conservative

For the first scheduler refactor, persistence should continue to store:

- cache identity
- task completion state
- frame count
- deletion mark

The new intent fields do not need to be persisted in the first pass.

Reason:

- intent is inherently session-oriented
- collection focus can be rebuilt at runtime
- avoiding persistence churn keeps migration risk lower

That means `ThumbnailIndex` should continue to load tasks in a neutral scheduling state such as:

- `BackgroundFill`

unless the app re-applies stronger intent during runtime.

#### Step A4: Add collection registration structures inside `ThumbnailGenerator`

Introduce internal maps such as:

- `collectionId -> set of video paths`
- `videoPath -> set of collection ids`

These are runtime-only scheduler structures.

They allow the generator to:

- register folder-backed collections now
- support category-backed collections later
- update many tasks when collection focus or collection boost changes

#### Step A5: Add additive collection-aware methods to `IThumbnailGenerator`

Add methods such as:

- `RegisterCollection(...)`
- `RemoveCollection(...)`
- `FocusCollection(...)`
- `BoostCollection(...)`
- `BoostVideo(...)`

Keep current methods such as `EnqueueFolder(...)` and `DeleteForFolder(...)` for now.

At this stage:

- `EnqueueFolder(...)` can internally call `RegisterCollection(...)`
- folder items can be seeded as `BackgroundFill`

#### Step A6: Replace `SortQueue()` with on-demand lane selection

Do not maintain one globally sorted list.

Instead:

- keep `_tasks` as task registry/storage
- select next candidate by scanning pending tasks in intent order
- use `IntentUpdatedAtUtcTicks` and stable path order for tie-breaking

This is slightly less elegant than building a dedicated heap, but it is safer for a first pass because:

- the current task volume is manageable
- the existing code already operates on list scans in several places
- correctness matters more than theoretical optimality here

#### Step A7: Re-map old folder enqueue behavior

Current enqueue logic computes:

- folder order
- played / unplayed / last-played bias

That arithmetic should be deleted from scheduler ranking.

For Phase A, replace it with a simpler meaning:

- folder registration creates or revives tasks
- tasks enter neutral background state
- optional seeding can still mark last-played as `FocusedCollection` only if the folder is actively opened later

This is the first explicit break from "folder order controls the world".

#### Step A8: Update tests around behavior rather than numeric order

Rewrite tests to assert semantics such as:

- boosted video is chosen before background video
- focused collection items are chosen before neutral items
- duplicate registration still does not duplicate tasks
- requeue behavior preserves intent when appropriate

Tests that directly depend on `Priority` ordering should be removed or rewritten.

### Phase A expected outcome

After Phase A:

- scheduler no longer depends on folder-order math
- old folder entry points still work
- the runtime can express foreground and background intent
- player and library interaction features can be added without another scheduler rewrite

## Concrete Phase B Task Breakdown

Phase B should deliver the first user-visible payoff with a modest scope: make player entry and title-bar status smarter.

### Files that should change in Phase B

Primary:

- `src/AniNest/Infrastructure/Thumbnails/IThumbnailGenerator.cs`
- `src/AniNest/Infrastructure/Thumbnails/ThumbnailGenerator.cs`
- `src/AniNest/Features/Player/Services/PlayerAppService.cs`
- `src/AniNest/Features/Shell/ShellViewModel.cs`
- `src/AniNest/Data/Languages/zh-CN.json`
- `src/AniNest/Data/Languages/en-US.json`

Likely:

- `src/Tests/Model/ThumbnailGeneratorTests.cs`
- `src/Tests/View/MainPageViewModelTests.cs` only if shared status assumptions change

### Phase B implementation steps

#### Step B1: Add playback-window API

Add:

- `BoostPlaybackWindow(IReadOnlyList<string> orderedVideoPaths, int currentIndex, int lookaheadCount)`

The generator should map:

- current video -> `PlaybackCurrent`
- next few videos -> `PlaybackNearby`
- optionally previous video -> `PlaybackNearby`

This should not require the player feature to understand scheduler internals.

#### Step B2: Call playback-window boost from player entry flow

`PlayerAppService` already knows when folder data has loaded and what the current index is.

Once playlist data is ready, it should send the player working set into the generator.

That is the best first integration point because:

- the ordered video list already exists
- the current index is already resolved
- this happens exactly when the user has expressed strong intent

#### Step B3: Expose current scheduler target in snapshot

`ThumbnailGenerationStatusSnapshot` should be extended with lightweight fields such as:

- `CurrentIntent`
- `CurrentTargetName`
- `ForegroundPendingCount`

These fields should remain presentation-friendly and avoid leaking large internal state.

Example:

```csharp
public sealed record ThumbnailGenerationStatusSnapshot(
    bool IsPaused,
    bool IsPlayerActive,
    int ActiveWorkers,
    int ReadyCount,
    int TotalCount,
    int PendingCount,
    int ForegroundPendingCount,
    string? CurrentTargetName,
    string? CurrentIntentCode);
```

#### Step B4: Upgrade shell status text generation

`ShellViewModel` already derives:

- status text
- count text
- detail text
- tooltip text

This is the right place to enrich status copy without burdening the generator with localization concerns.

Recommended evolution:

- keep existing aggregate counts
- add one short line describing current target
- optionally adjust tooltip to mention foreground backlog

#### Step B5: Keep title-bar UI shape mostly unchanged

The existing title-bar popup already has enough structure:

- title
- status row
- progress bar
- detail text
- tooltip-style secondary text

So Phase B should mostly change bound text, not layout.

That keeps the visual change small while still making the scheduler feel more understandable.

### Phase B expected outcome

After Phase B:

- entering the player immediately biases thumbnail work toward the active series
- the title bar can say what the system is doing now
- users get visible benefit before any category UI is built

## Concrete Phase C Task Breakdown

Phase C is the first library-facing manual-control phase. It should still avoid full category implementation.

### Files that should change in Phase C

Primary:

- `src/AniNest/Features/Library/Services/ILibraryAppService.cs`
- `src/AniNest/Features/Library/Services/LibraryAppService.cs`
- `src/AniNest/Features/Library/MainPageViewModel.cs`
- `src/AniNest/Features/Library/MainPage.xaml`

Likely:

- `src/AniNest/Features/Library/Services/LibraryContracts.cs`
- `src/AniNest/Data/Languages/zh-CN.json`
- `src/AniNest/Data/Languages/en-US.json`

### Phase C implementation steps

#### Step C1: Introduce collection refs in library-facing models

The first version can treat each folder card as one `Folder` collection ref.

That gives the library UI a new contract without requiring system categories yet.

#### Step C2: Add collection-level library commands

Recommended first commands:

- prioritize this group
- regenerate thumbnails for this group
- clear thumbnail cache for this group

These commands should talk to `ILibraryAppService`, not directly to `IThumbnailGenerator`.

#### Step C3: Keep library service as the orchestration boundary

`LibraryAppService` should translate library-facing requests into:

- collection registration
- collection focus
- collection boost
- regeneration / deletion operations

This keeps view models smaller and preserves a consistent feature-to-infrastructure boundary.

#### Step C4: Use focus signals conservatively

The first focus integration can be modest:

- when a library surface becomes active, focus its current collection
- do not try to recompute focus on every hover or scroll event

That keeps the interaction calm and avoids turning navigation noise into scheduler churn.

### Phase C expected outcome

After Phase C:

- users can manually prioritize visible groups
- library logic starts speaking in collection terms
- folder cards become the first concrete instance of a broader collection system

## Phase D and Beyond

Once categories and custom collections are implemented, the scheduler should not require a conceptual redesign.

At that point the main work becomes:

- building collection definitions
- refreshing collection membership
- exposing collection-focused UI

The thumbnail system should already be capable of consuming those inputs.

## Recommended "Do Not Do Yet" List

To keep the first implementation disciplined, the following should be explicitly deferred:

- persist foreground intent across app restarts
- speculative generation based on hover
- continuously re-ranking tasks from scroll position
- one-off special priority rules for each future category
- a separate standalone job-manager window

These would add complexity before the new scheduling contract has proved itself.

## Practical First Milestone

If the project wants one concrete milestone with visible value and manageable risk, the best first milestone is:

1. finish Phase A scheduler refactor
2. wire Phase B playback-window boosting
3. surface current target in title-bar status

This gives a meaningful user-experience improvement before library category work begins, and it validates the core architecture with minimal UI churn.

## Migration Strategy

### Stage 1

- keep current thumbnail task persistence
- remove static folder-order priority arithmetic
- introduce foreground vs background lane selection
- auto-boost player current and nearby videos
- add collection-level manual prioritization

### Stage 2

- expand library interactions to system categories and user categories
- add single-video manual actions in the player playlist
- improve status text around current target and next targets

### Stage 3

- optionally support richer collection-level controls
- optionally support temporary virtual collections such as filtered views or search results

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
