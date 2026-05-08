# Thumbnail Performance and Hardware Decode Design

## Goal

This document defines a practical design for improving background thumbnail generation in `AniNest`.

The current thumbnail pipeline uses `ffmpeg` in software decode mode and can saturate CPU while scanning and generating previews. For a portable desktop app, the target is not "maximum throughput at all costs", but a smoother and quieter default experience:

- thumbnails appear progressively in the background
- background work should not noticeably hurt library browsing
- entering the player should automatically reduce or pause background thumbnail work
- the app should prefer the most suitable decode path on the current machine
- hardware decode failure should automatically fall back without bothering the user
- user preferences travel with the portable app, while machine capability decisions are re-evaluated per machine

In short:

> Thumbnail generation should be adaptive, low-drama, and subordinate to playback smoothness.

## Target User Experience

### Default behavior

- The library opens quickly even when thumbnails are missing.
- Existing covers or cached thumbnails are shown first.
- Missing thumbnails are filled in gradually in the background.
- Background generation no longer tries to fully occupy CPU by default.

### While browsing the library

- Thumbnail work runs at a controlled pace.
- UI actions such as scrolling, opening menus, and entering a folder remain responsive.
- Progress remains visible in lightweight form, such as `32 / 120`.

### While playing video

- Playback is always higher priority than thumbnail generation.
- Entering the player automatically lowers thumbnail generation pressure.
- In conservative modes, new thumbnail jobs pause while the player is active.
- Users should feel that playback stays smooth and the machine becomes quieter.

### Hardware acceleration

- The app automatically detects the current machine and chooses the best available thumbnail decode strategy.
- Users are not expected to know `cuda`, `qsv`, `d3d11va`, or software decode details.
- If one decode path fails on the current machine, the app automatically falls back to the next candidate.
- The app remembers the last good decoder for the current machine and prefers it next time.

### Portable app expectations

- Preferences are stored in the app's portable data directory and move with the app.
- Hardware capability decisions are scoped to the current machine.
- Moving the app to a different PC triggers re-evaluation instead of blindly reusing the previous machine's decoder result.

## Non-Goals

The first implementation does not aim to provide:

- a full task manager UI for thumbnail jobs
- fine-grained CPU percentage control
- GPU vendor-specific tuning screens
- multiple simultaneous hardware probing strategies
- aggressive benchmarking to find the absolute fastest decoder

The design should stay intentionally modest and easy to evolve.

## Design Principles

1. Playback first.
2. Prefer stable defaults over maximum generation speed.
3. Make background work adaptive by app state.
4. Keep the user-facing controls simple.
5. Persist preferences portably, but bind hardware results to the current machine.
6. Use automatic fallback rather than noisy error surfaces.

## Proposed User Settings

Two user-facing settings are enough for the first version.

### Thumbnail Performance Mode

- `Quiet`
- `Balanced` (default)
- `Fast`

These modes control concurrency and player-time throttling.

### Thumbnail Acceleration Mode

- `Auto` (default)
- `Compatible`

Meaning:

- `Auto`: try hardware-capable decode strategies first, then fall back to software
- `Compatible`: use a more conservative strategy order and prefer stability over acceleration

## Runtime Modes

The system should react to app context rather than only static settings.

### Library-active mode

Triggered when the user is on the library page.

Behavior:

- background generation allowed
- limited concurrency
- normal queue consumption

### Player-active mode

Triggered when the player page is active.

Behavior:

- thumbnail generation throttled more aggressively
- in some modes, no new jobs start until player exit
- current work may either finish naturally or be reduced to minimal concurrency

### Idle / startup mode

Optional internal mode for future use.

For the first version this can behave the same as library-active mode.

## Recommended First-Version Policy

Keep the scheduling policy very simple.

### Quiet

- library page: `max concurrency = 1`
- player page: pause starting new jobs

### Balanced

- library page: `max concurrency = 1`
- player page: do not start new jobs; let current job finish

### Fast

- library page: `max concurrency = 2`
- player page: reduce to `max concurrency = 1`

This is enough to stop CPU runaway without building a complicated scheduler.

## Architecture Changes

The change should fit the current project structure:

- `Infrastructure` owns machine detection, thumbnail execution, and persistence of machine-specific probe results
- `Features` owns user-facing preferences and app-state transitions
- `CompositionRoot` wires the new services into existing services

### New or updated modules

#### 1. `ThumbnailGenerator`

This remains the central thumbnail job coordinator, but gains a small scheduling layer.

New responsibilities:

- track current performance mode
- track whether player page is active
- limit concurrent generation workers
- pause or resume starting new jobs
- ask for the current decoder strategy chain when launching `ffmpeg`
- record success/failure of decoder attempts

It does not need a full-blown job system. A bounded queue plus simple worker gate is enough.

#### 2. `IThumbnailPerformancePolicy` or internal policy object

This can be a small object or helper inside `ThumbnailGenerator`.

Responsibilities:

- convert user mode plus app state into:
  - allowed concurrency
  - whether new jobs may start

Example output:

- `AllowedConcurrency = 1`
- `AllowStartNewJobs = false`

This can stay internal at first if that keeps the code smaller.

#### 3. `IHardwareCapabilityService`

New infrastructure service for machine-aware decoder selection.

Responsibilities:

- compute a stable machine identity
- inspect the current machine for likely decoder options
- return a ranked decoder candidate chain
- expose a method to refresh probe results when machine identity changes

The first version does not need deep benchmarking. It only needs a reliable candidate list and fallback order.

#### 4. Machine-scoped decode cache

Stored under portable settings data.

Responsibilities:

- remember the current machine id
- remember the last successful thumbnail decoder on this machine
- remember probe timestamp and probe outcome summary

This can be stored directly in `AppSettings` first to reduce moving parts.

#### 5. Player-state hook

Existing player flow should notify thumbnail generation when player state changes.

Likely integration points:

- `PlayerAppService.EnterPlayerAsync`
- `PlayerAppService.BeginLeavePlayerAsync`
- `ShellViewModel` page transitions if needed

The thumbnail system only needs a coarse signal:

- player active
- player inactive

## Decoder Strategy Model

The implementation should not hardcode a single decode path.

Instead, each thumbnail job should run against a decoder strategy chain.

Example strategies:

- `Software`
- `D3D11VA`
- `IntelQsv`
- `NvidiaCuda`

The actual `ffmpeg` arguments can remain encapsulated in one place.

### Example candidate order

These are not strict promises, just first-version heuristics.

#### NVIDIA-capable machine

`NvidiaCuda -> D3D11VA -> Software`

#### Intel-capable machine

`IntelQsv -> D3D11VA -> Software`

#### AMD or generic DirectX-capable machine

`D3D11VA -> Software`

#### Unknown or incompatible machine

`Software`

### Failure handling

For each thumbnail job:

1. pick the preferred decoder chain for the current machine
2. try the first decoder
3. if the job succeeds, remember that decoder as the current machine preference
4. if it fails, log the failure and try the next decoder
5. if all fail, report the thumbnail as failed for now

The user should not see a modal failure unless the whole pipeline is fundamentally unavailable.

## Machine Identity

Because the app is portable, hardware decisions must be machine-aware.

### Requirements

Machine identity should be:

- stable enough across app restarts on the same PC
- different across different PCs in normal usage
- available without installer assumptions
- cheap to compute

### First-version approach

Use a composite machine fingerprint from several low-risk values, for example:

- machine name
- CPU model
- primary GPU adapter name

Hash the concatenated value before persisting.

This does not need to be security-grade identity. It only needs to distinguish "same machine" from "probably different machine" for cache invalidation.

## Persistence Model

The easiest first implementation is to extend `AppSettings`.

### User preference fields

- `ThumbnailPerformanceMode`
- `ThumbnailAccelerationMode`

### Machine-scoped fields

- `ThumbnailDecoderMachineId`
- `ThumbnailPreferredDecoder`
- `ThumbnailLastProbeUtc`
- `ThumbnailProbeSummary`

If this becomes crowded later, it can move into nested settings objects without changing the overall design.

## High-Level Runtime Flow

### App startup

1. load settings
2. load user thumbnail performance preferences
3. compute current machine id
4. compare with cached machine id
5. if machine changed, clear preferred decoder cache for thumbnail generation
6. warm the hardware capability service lazily or on first thumbnail request

### Library load

1. `LibraryAppService` scans folders and enqueues thumbnail work
2. `ThumbnailGenerator` accepts jobs into its queue
3. scheduler applies current library-mode concurrency
4. each worker requests decoder candidates
5. worker launches `ffmpeg` with fallback chain until success or exhaustion
6. progress events update the UI

### Enter player

1. `PlayerAppService.EnterPlayerAsync` marks thumbnail generator as player-active
2. thumbnail scheduler reduces concurrency or stops starting new jobs
3. playback initialization proceeds with higher effective priority

### Leave player

1. `PlayerAppService.BeginLeavePlayerAsync` or transition completion marks thumbnail generator as player-inactive
2. thumbnail scheduler resumes library-mode behavior
3. queue continues draining in the background

## Proposed Implementation Phases

Keep delivery incremental.

### Phase 1: Scheduling Control

Goal:

- stop CPU saturation
- prioritize playback over thumbnail work

Work:

- add performance mode setting
- add player-active signal into thumbnail generator
- add concurrency limit
- add pause-or-don't-start policy during playback

This phase should already produce a visible UX improvement.

### Phase 2: User Settings Surface

Goal:

- expose simple controls to users

Work:

- add `Quiet / Balanced / Fast`
- add `Auto / Compatible`
- persist these in settings
- reflect them in localization resources and settings UI

### Phase 3: Hardware Capability and Fallback

Goal:

- reduce CPU usage further on supported machines

Work:

- add machine fingerprint logic
- add hardware capability service
- define decoder strategy chain
- make thumbnail execution try decoder fallback chain

### Phase 4: Machine-Scoped Caching

Goal:

- avoid re-learning the best decoder every run

Work:

- persist machine id and preferred decoder
- invalidate when machine changes
- reuse preferred decoder as first choice on later runs

### Phase 5: Refinement

Possible follow-ups:

- optional cool-down after repeated decoder failures
- optional lightweight probe command at startup
- richer status text in UI
- more nuanced queue prioritization

## Concrete Todo List

This is the recommended implementation order.

1. Add new settings fields for thumbnail performance and acceleration modes.
2. Add enum types for performance mode and decoder mode.
3. Extend `SettingsService` read/write logic for the new fields.
4. Add a lightweight player-active signal path into `ThumbnailGenerator`.
5. Implement simple concurrency limiting in `ThumbnailGenerator`.
6. Implement first-version mode policy:
   - `Quiet`
   - `Balanced`
   - `Fast`
7. Wire player enter/leave lifecycle to update thumbnail scheduling behavior.
8. Add settings UI and localization strings for the two new user-facing options.
9. Add a hardware capability service with machine fingerprint generation.
10. Define decoder strategy enum and ffmpeg argument mapping.
11. Update thumbnail generation execution to try decoder candidates in order.
12. Persist machine-scoped preferred decoder cache.
13. Invalidate machine-scoped cache when machine identity changes.
14. Add logs for decoder selection, fallback, and scheduling decisions.
15. Add tests for:
   - settings persistence
   - performance mode policy
   - machine-id change invalidation
   - decoder fallback ordering

## Suggested Tests

### Unit tests

- performance mode maps to expected concurrency and start policy
- machine id change clears stale preferred decoder
- decoder chain is ordered correctly for a mocked capability result
- thumbnail generator does not start new jobs while player-active under `Quiet` and `Balanced`

### Integration-style tests

- entering player reduces thumbnail throughput policy
- leaving player resumes background work
- decoder failure falls through to software decode

## Risks and Tradeoffs

### Hardware detection complexity

Risk:

- probing every possible accelerator path can become messy and brittle

Mitigation:

- keep first-version detection heuristic and fallback-driven

### Overly aggressive scheduling

Risk:

- even hardware decode can still create disk and memory pressure

Mitigation:

- retain strict concurrency caps even in `Fast`

### Portable cache confusion

Risk:

- stale hardware result copied from one PC to another

Mitigation:

- always compare machine identity before trusting cached decoder preference

### UI overexposure

Risk:

- too many options create support burden

Mitigation:

- start with two simple settings only

## Recommended First Task

Start with Phase 1.

The best first slice is:

1. add `ThumbnailPerformanceMode`
2. teach `ThumbnailGenerator` to cap concurrency
3. notify it when the player becomes active/inactive

That will solve the most visible pain quickly, while keeping the later hardware work compatible with the same design.
