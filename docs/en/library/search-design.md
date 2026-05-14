# Library Search Design

## Goal

This document defines the first-version search experience for the `AniNest` library page.

The goal is to help users quickly narrow a medium or large card library without turning the page into a heavy management UI.

Phase 1 search should feel:

- immediate
- quiet
- predictable
- compatible with the existing top filter bar

## Product Decision

Phase 1 should use:

- a persistent search box on the library page
- placement directly below the top filter bar
- local in-memory filtering
- combined behavior with existing classification filters

Phase 1 should **not** use:

- a footer search bar
- a separate search page
- network-backed search
- fuzzy ranking or advanced query syntax

## Target Experience

The intended experience is:

1. the user opens the library page
2. the page loads normally and cards appear immediately
3. the user types into the search box
4. the card grid filters in place as the user types
5. the active top filter and the search query both apply at the same time
6. clearing the query restores the current filter view without a page refresh

The target feeling is "narrow the current library view", not "launch a separate search workflow".

## Layout Decision

### Chosen layout: Option A

The search box should be placed **below the existing top filter bar**.

Recommended visual order:

1. top filter bar
2. search box
3. card grid

### Why this layout fits

This works better than a persistent bottom search bar because:

- search is usually a pre-browse action
- users expect search controls near other view controls
- the library page already has a top filter area
- a bottom-fixed bar would compete with scrolling and reduce available vertical space

This layout also keeps future additions such as sort controls or metadata scope toggles close to the same control cluster.

## UX Principles

### 1. Search should be instant

Typing should update visible results immediately. Phase 1 should not require pressing Enter or clicking a search button.

### 2. Search should stack with filters

The selected library filter and the search query should always compose.

Examples:

- `Favorites` + `Bocchi`
- `Watching` + `2024`
- `Completed` + `Gundam`

### 3. Search should stay forgiving, but not magical

Phase 1 should support simple contains-style matching with lightweight normalization.

It should not attempt aggressive fuzzy logic that can make results feel surprising.

### 4. Empty states should be explicit

The UI should distinguish:

- the library is empty
- the library has items, but the current search returned no results

Those are different user situations and should not share the same message.

## Search Scope

Phase 1 search should check these fields for each library folder card:

- folder name
- metadata title
- metadata original title
- metadata tags

This makes metadata immediately useful without requiring a richer search system.

### Explicitly excluded in Phase 1

- metadata summary full-text search
- episode filename search
- provider/source-id search
- advanced field filters
- custom query syntax

## Matching Behavior

Phase 1 matching should use:

- case-insensitive comparison
- trim of leading and trailing whitespace
- simple contains matching
- lightweight normalization shared with metadata matching where practical

Recommended normalization direction:

- normalize case
- collapse repeated spaces
- treat common separators consistently

The exact implementation does not need to be perfect, but it should avoid obvious misses caused by formatting differences alone.

## Search Box Behavior

The search box should support:

- placeholder text
- live update while typing
- a clear action when the query is not empty

Recommended behavior:

- empty query means "search disabled"
- whitespace-only query behaves the same as empty query
- clearing the query preserves the selected top filter

`Ctrl+F` can later be added as a focus shortcut, but it is not required for Phase 1.

## Result Behavior

Search results should reuse the existing filtered card surface instead of creating a new result list model.

That means:

- cards keep their existing order
- existing card actions continue to work
- context menus, favorite toggles, and status changes remain unchanged
- search only changes visibility, not card identity

This is important because the current library page already maintains a master list plus a visible filtered list.

## Empty-State Behavior

### Library empty

If the library itself is empty, the page should keep the existing empty-library presentation.

### Search no results

If the library contains items but the query returns no matches, the page should show a search-specific empty state.

Recommended tone:

- tell the user no items matched the current search
- provide an obvious clear action
- avoid implying failure or broken loading

## Architecture Direction

Phase 1 should be implemented as an extension of the current library filtering flow, not as a new subsystem.

Recommended shape:

- keep `_allFolderItems` as the full in-memory source
- keep `FolderItems` as the visible projection
- extend the existing filter application step so it evaluates:
  - selected filter match
  - search query match

Conceptually:

```csharp
visible = allItems
    .Where(MatchesSelectedFilter)
    .Where(MatchesSearchQuery);
```

This keeps the implementation aligned with the current library page architecture and minimizes behavioral risk.

## Metadata Interaction

Search should remain reactive to metadata updates.

If metadata for a folder arrives after the page is already open:

- the folder card metadata updates as it already does
- the current search query should be re-applied

This matters because a folder may become searchable by metadata title or tags after background scraping completes.

## First-Version Scope

Phase 1 should include:

- a persistent search box below the top filter bar
- local live filtering
- folder-name search
- metadata title/original-title/tag search
- combined behavior with existing library filters
- a clear action
- search-specific empty state

Phase 1 should exclude:

- bottom search bar
- advanced ranking
- full-text metadata summary search
- per-field search toggles
- saved searches
- standalone search results page

## Suggested Implementation Order

1. add a library search query property to the page ViewModel
2. extend filter application to include search matching
3. add a top search box to the library page UI
4. add empty-state differentiation for no-result search
5. add focused tests for:
   - filter + search composition
   - metadata-driven matches
   - query clear behavior
   - no-result state

## Summary

The first version of library search in `AniNest` should be a lightweight top-of-page narrowing tool:

- always visible
- placed below the filter bar
- immediate while typing
- combined with classification filters
- powered entirely by local in-memory data

This gives the library page a much stronger day-to-day browsing experience without adding heavy search architecture too early.
