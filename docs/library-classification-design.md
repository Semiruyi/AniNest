# Library Classification Interaction Design

## Goal

This document defines the first-version interaction design for library classification in `AniNest`.

The target is to help users quickly organize their library without turning the library page into a heavy management surface.

The core question is not only "what categories should exist", but also:

- how users assign items to those categories
- how the library page exposes those categories
- how much automation is appropriate
- whether custom categories should exist in v1

## Product Decision

The first version should use:

- a fixed `watch status`
- a separate `favorite` flag
- a lightweight top filter bar on the library page

The first version should **not** support user-defined categories.

## Classification Model

Although the UI can show:

- `All`
- `Watching`
- `Unsorted`
- `Completed`
- `Favorites`
- `Dropped`

the data model should not treat them as one six-way mutually exclusive category.

Instead:

- `All` is only a filter
- `Favorites` is an independent boolean flag
- `Watching`, `Unsorted`, `Completed`, and `Dropped` belong to one watch-status enum

Recommended shape:

```csharp
public enum WatchStatus
{
    Unsorted,
    Watching,
    Completed,
    Dropped
}

public sealed record LibraryFolderState(
    WatchStatus Status,
    bool IsFavorite);
```

This split is important because users commonly want combinations such as:

- completed + favorite
- watching + favorite

If `Favorites` is treated as a mutually exclusive category, the interaction will become awkward very quickly.

## Why V1 Should Avoid Custom Categories

User-defined categories sound flexible, but they introduce significant product and implementation complexity.

### UX cost

If custom categories exist, the app must define:

- where users create them
- how users rename them
- how users delete them
- how categories are ordered
- what happens when a category becomes empty
- whether one item can belong to multiple categories
- whether system categories and custom categories share the same surface

That would make the first version of library classification feel much heavier than the current library page.

### Development cost

Supporting custom categories also means adding:

- persistent category definitions
- item-to-category mapping
- category management UI
- migration logic for future model changes
- more filtering and sorting states
- more edge-case handling and tests

For the likely first-wave user needs, this cost is not justified.

### Better expansion path

A better path is:

1. ship fixed status + favorites first
2. validate real usage
3. add custom tags or custom collections later only if needed

This keeps the first design simple without blocking future expansion.

## UX Principles

The interaction should follow four principles.

### 1. Classification should be low-friction

Users should be able to classify items while browsing, not through a separate management flow.

### 2. Default behavior should stay lightweight

Adding a folder to the library should not immediately force the user to choose a category.

New content should enter as `Unsorted`.

### 3. Automation should assist, not decide too much

The app can infer `Watching` from playback behavior, but it should avoid making stronger judgment calls such as automatically marking content as `Dropped`.

### 4. The library page should remain a browsing surface first

The current library page is a visual card surface. Classification UI should fit into that surface instead of turning it into a dense admin panel.

## Recommended Library Page Layout

### Top filter bar

Add a horizontal filter bar at the top of the library page.

Recommended entries:

- `All`
- `Watching`
- `Unsorted`
- `Completed`
- `Favorites`
- `Dropped`

Behavior:

- single-select
- default selection is `All`
- switching filters updates the visible card list in place

Optional later enhancement:

- show item counts, such as `Watching 12`

For v1, counts are useful but not required if they add layout complexity.

### Why top filters work well here

This fits the current page structure:

- users already browse a card grid
- the page does not currently have a heavy left sidebar information architecture
- top filters are visually lightweight and easy to scan

This also scales well if the library later adds search or sort controls near the same area.

## Recommended Card-Level Interaction

Classification should be primarily editable from the card itself.

### Card actions

Each card should expose two lightweight classification actions:

- a `favorite` toggle
- a `status` action

### Favorite toggle

Place a small favorite icon in the card corner, ideally at the top-right.

Behavior:

- single click toggles favorite on or off
- visible on hover, and optionally persistent when already active
- should feel instant, without opening a dialog

### Status action

Expose status editing as a compact action on the card.

This can be implemented in either of these ways:

1. a small status chip/button visible in the card info area
2. a hover action near the favorite button

When activated, it opens a compact popup menu with:

- `Watching`
- `Unsorted`
- `Completed`
- `Dropped`

The current status should have a visible check mark.

This keeps the classification action close to the content and makes bulk browsing and light organization feel natural.

## Recommended Right-Click Menu

Desktop users will expect right-click support, and the current library page already uses a context menu overlay.

The right-click menu should include full classification controls as a secondary path.

Recommended options:

- `Favorite` or `Unfavorite`
- `Mark as Watching`
- `Mark as Unsorted`
- `Mark as Completed`
- `Mark as Dropped`

This should coexist with existing card actions such as thumbnail operations and library management actions.

### Suggested menu structure

To avoid making the menu feel cluttered, classification items should appear near the top as one logical group.

Recommended grouping:

1. classification actions
2. library-management actions
3. thumbnail-generation actions

This improves discoverability because classification becomes a first-class library behavior rather than a hidden side capability.

## How Users Classify Items

The intended primary flow is simple:

1. user browses the library
2. user hovers a card
3. user clicks favorite or opens the status menu
4. the card updates immediately
5. top filters can then be used to view the new segment

The important idea is that classification happens inline, during normal browsing.

Users should not need:

- a dedicated edit mode
- a batch management dialog
- a separate category management page

## Automatic Behavior

Some automatic behavior is worth adding because it reduces maintenance burden without feeling invasive.

### Recommended automation

If an item is currently `Unsorted` and the user starts playing it, the app may automatically change it to `Watching`.

This is a strong and usually correct signal.

### Optional later automation

The app may later offer rules such as marking an item `Completed` when:

- the single video is watched to a completion threshold
- the final episode in a folder is finished

This should be added carefully and may need a setting or a conservative threshold.

### Automation to avoid

The app should not automatically mark content as `Dropped`.

That state is interpretive and should remain a user decision.

## Visual Presentation of Classification State

The card should communicate state, but the UI should remain clean.

Recommended visible signals:

- favorite icon in the top-right area
- one small status label or status dot in the lower info area

Example visual tone:

- `Watching`: cool accent
- `Unsorted`: neutral gray
- `Completed`: calm green
- `Dropped`: subdued red

The state marker should be subtle. The card already contains cover art, title, progress text, and progress bar, so classification should not compete too aggressively for attention.

## First-Version Scope

The first version should include:

- a watch-status enum with four states
- an independent favorite flag
- a top filter bar on the library page
- inline card actions for favorite and status
- right-click menu support for classification
- optional automatic transition from `Unsorted` to `Watching` on first playback

The first version should exclude:

- user-defined categories
- nested categories
- multi-select batch classification
- drag-and-drop category assignment
- a dedicated category management page

## Data and Architecture Direction

To keep future expansion clean, the classification model should be stored as library item metadata rather than inferred purely from progress.

Recommended conceptual fields:

```csharp
public enum WatchStatus
{
    Unsorted,
    Watching,
    Completed,
    Dropped
}

public sealed record LibraryFolderClassification(
    string FolderPath,
    WatchStatus Status,
    bool IsFavorite);
```

If future expansion is needed, add a separate field later such as:

```csharp
IReadOnlyList<string> Tags
```

or

```csharp
IReadOnlyList<string> CollectionIds
```

The important point is that v1 should not force a full custom-category system too early.

## Final Recommendation

For the first release of library classification in `AniNest`:

- use fixed watch-status values
- keep favorites independent
- expose classification through a top filter bar plus inline card actions
- support right-click as a full desktop-friendly fallback
- allow only limited, conservative automation
- do not support user-defined categories yet

This gives the app a clean and practical organization model with low interaction cost, low implementation risk, and a clear path for future expansion.

## Implementation Status

### Step 1 complete

The first implementation step should establish the data and service foundation before any library-page UI work.

Completed scope:

- add persistent folder classification metadata
- store `WatchStatus` and `IsFavorite` separately from playback progress
- expose classification data through `LibraryFolderDto`
- add service APIs for reading and updating folder classification
- add focused persistence and library-service tests

Deferred to later steps:

- library-page filter UI
- card-level favorite and status controls
- right-click classification actions
- automatic transition from `Unsorted` to `Watching`
