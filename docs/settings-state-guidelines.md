# Settings State Guidelines

## Goal

This document defines a lightweight rule set for how `AniNest` should represent, read, cache, and refresh user-facing settings state.

It is intentionally practical. The aim is not to introduce a new framework, but to reduce drift between:

- persisted settings
- runtime state
- `ViewModel` state used for WPF binding

The thumbnail performance work exposed a recurring risk:

> a `ViewModel` keeps its own copy of settings-like state, and the copy gradually stops matching the real source of truth.

This document gives a default answer for those cases.

## Scope

This guidance applies to:

- settings menus under `Shell`
- feature options backed by `ISettingsService`
- UI state derived from persisted preferences
- feature commands that must update both persisted settings and runtime components

This guidance does not try to fully define:

- player playback state flow
- long-running background pipeline state
- general observable-store architecture

Those may need stronger patterns later.

## Terms

### Persisted settings state

State stored through `ISettingsService`, such as:

- language
- fullscreen animation mode
- thumbnail performance mode
- thumbnail acceleration mode

### Runtime state

State held by long-lived runtime components, such as:

- current thumbnail scheduler mode
- whether the player page is active
- active thumbnail worker count

### View state

Short-lived UI state owned by a `ViewModel`, such as:

- whether a popup is open
- whether a submenu is expanded
- whether a command is currently running
- which temporary text should be shown during an interaction

## Core Principles

1. One fact should have one owner.
2. `ViewModel` should not cache persisted settings by default.
3. Runtime state and persisted settings should be modeled separately, even when they are related.
4. If one user action updates runtime state and persisted settings together, the operation belongs in an app service.
5. If caching becomes necessary, cache below the `ViewModel` layer and make the cache explicit.

## Recommended Ownership

### `ViewModel`

`ViewModel` should own:

- popup open/close state
- selection hover state
- loading and in-progress flags
- command availability
- display composition derived from services

`ViewModel` should usually not own:

- a copied current settings value that can be read from a service
- a second copy of runtime state already owned by a controller or infrastructure component

### Preferences-facing service

A service such as `ShellPreferencesService` should:

- expose current persisted settings in UI-friendly form
- translate raw storage values into feature-friendly values
- write simple settings updates that only affect persistence

It should not usually:

- coordinate multi-step runtime transitions
- own rollback rules across runtime and persistence
- hide business sequencing behind a "preferences" name

### App service

An app service should own a settings-related operation when the operation:

- updates more than one subsystem
- has ordering requirements
- has rollback behavior
- needs an async boundary for UI safety

Example:

- switching thumbnail performance mode updates runtime scheduler state and persisted settings together

## Default Read Rule

For persisted settings state, prefer:

> read current value from the service when needed, instead of storing a duplicate field in the `ViewModel`

This is the default unless there is a proven performance problem.

Good fit:

- selected option index for a settings menu
- summary label text derived from a current settings value
- boolean "is selected" properties for option buttons

Less suitable:

- rapidly changing state read many times per frame
- state whose source requires expensive IO or heavy computation on every access

## Default Refresh Rule

Removing `ViewModel` caches does not remove the need for UI refresh signals.

If a WPF-bound property reads from a service in its getter, the UI still needs property change notification when the underlying source changes.

Default rule:

- keep the source of truth outside the `ViewModel`
- after a successful mutation, raise `OnPropertyChanged(...)` for the affected computed properties

Typical examples:

- selected index
- selected booleans
- summary text
- command enabled state

This is still simpler than storing a mirrored settings field and manually keeping it in sync.

## When `ViewModel` Caching Is Acceptable

`ViewModel` caching is acceptable only when at least one of these is true:

- reading the source is measurably expensive
- multiple UI properties need a stable snapshot for one interaction
- the data is not canonical settings state, but temporary UI composition state
- the cache lifetime is local, explicit, and easy to invalidate

If a `ViewModel` caches settings-like data, it should be clear:

- what the source of truth is
- when the cache is refreshed
- what event invalidates it

If those answers are vague, the cache probably should not exist.

## Where Caching Should Go Later

If settings reads become slow in the future, prefer one of these approaches:

1. Add explicit caching inside `ISettingsService` implementation.
2. Add a feature-facing snapshot service with a clear invalidation policy.
3. Add a shared observable state object for a feature.

Avoid:

- each `ViewModel` inventing its own private cache
- different features applying different refresh rules to the same setting

## Runtime State vs Persisted Settings

Do not merge these concepts just because the UI displays them near each other.

Persisted settings answer:

- what mode the user selected
- what should be restored next launch

Runtime state answers:

- what the system is doing right now
- whether background work is active, paused, throttled, or blocked

UI guidance:

- option selection can usually follow persisted settings
- live status text should usually follow runtime state

The important part is that one command should not leave them unintentionally diverged.

## Atomic Settings Commands

When one user action must change both runtime state and persisted settings, use this rule:

> the command succeeds only if both changes succeed

Recommended flow:

1. read previous persisted value
2. apply runtime change
3. if runtime change fails, stop
4. persist the new setting
5. if persistence fails, attempt runtime rollback
6. notify the UI after the operation finishes

This is an app-service concern, not a `ViewModel` concern.

## Practical Rules For New Code

When adding a new settings item, use this checklist:

1. Decide whether the value is persisted settings state, runtime state, or view state.
2. Pick one owner for the fact.
3. Do not mirror persisted settings into a `ViewModel` field unless there is a clear reason.
4. Put multi-step updates in an app service.
5. After mutation, raise property-changed notifications for computed UI properties.
6. If caching is needed, place it in a shared lower layer and document invalidation.

## Applying This To Current Shell Settings

### Good current direction

- `IsApplyingThumbnailPerformanceMode` is valid `ViewModel` state.
- thumbnail performance switching as an app service operation fits the architecture.
- persisted thumbnail mode and runtime thumbnail mode are treated as separate concepts.

### Likely future cleanup candidates

These can be reviewed later using the same rule set:

- settings values mirrored into `ShellViewModel` fields only for menu highlighting
- settings summaries that can be computed directly from current service values
- other shell settings commands that currently mix persistence and orchestration responsibilities

## Non-Goals For This Guideline

This document does not require an immediate refactor of all existing `ViewModel` fields.

Its purpose is:

- to guide new work
- to help evaluate future cleanup
- to keep similar settings features from drifting into different local patterns

## Decision Shortcut

When unsure, ask:

1. Is this a real source of truth, or just UI glue?
2. If the value changes elsewhere, can this `ViewModel` copy become stale?
3. If yes, can we read it from a service instead?
4. If reading becomes slow later, can we cache it below the `ViewModel`?

If the answers are "yes, it can become stale" and "yes, we can read it from a service", do not cache it in the `ViewModel`.
