# Backend Extraction Plan

## Goal

This document describes how to extract AniNest's backend from the current WPF application so the project can adopt a new UI stack later without rewriting the application logic twice.

The immediate goal is **not** to migrate to Avalonia, `mpv`, or another frontend in the same phase.

The immediate goal is:

- keep the current WPF app working
- remove WPF and Windows UI types from backend-facing contracts
- isolate platform-specific implementations behind interfaces
- make the application logic buildable as plain `.NET` code

If this phase succeeds, a later UI rewrite becomes a frontend replacement project instead of a full application rewrite.

## Why this is needed

AniNest already has a meaningful amount of reusable application logic:

- library scanning and folder workflows
- metadata storage and synchronization
- thumbnail scheduling and progress tracking
- player session orchestration
- settings persistence

However, that logic is still mixed with WPF-specific concepts in several critical places:

- playback contracts expose `WriteableBitmap`
- playback state exposes `ImageSource`
- app services and view models touch `Application.Current` and `Dispatcher`
- shell logic directly opens dialogs and folder pickers
- some runtime behavior assumes Windows-specific UI services

As long as those dependencies remain in shared services, the backend cannot move cleanly to another UI framework.

## Migration principle

The extraction should follow one rule:

> Backend layers may know about state, commands, files, tasks, and domain events, but not about how a specific UI toolkit draws pixels or opens windows.

In practice, that means:

- no `System.Windows` types in backend projects
- no `Microsoft.Win32.OpenFolderDialog` in backend projects
- no `LibVLCSharp.WPF` types in backend contracts
- no direct `Application.Current` or `Dispatcher` use outside the WPF shell

## Current coupling hotspots

The main coupling points in the current codebase are:

- `src/AniNest/Infrastructure/Media/IMediaPlayerController.cs`
  - exposes `WriteableBitmap`
- `src/AniNest/Features/Player/PlayerPlaybackStateController.cs`
  - exposes `ImageSource`
- `src/AniNest/Features/Player/Services/PlayerAppService.cs`
  - uses `Application.Current.Dispatcher`
- `src/AniNest/Features/Shell/ShellViewModel.cs`
  - uses `MessageBox`, `OpenFolderDialog`, and `Application.Current`
- `src/AniNest/Infrastructure/Presentation/*`
  - already contains useful UI abstractions, but they are still WPF-oriented in naming and placement
- `src/AniNest/Infrastructure/Interop/*`
  - includes Windows-specific behavior such as taskbar coordination
- `src/AniNest/Infrastructure/Thumbnails/Execution/WindowsThumbnailProcessController.cs`
  - is platform-specific and should stay outside extracted shared backend layers

## Target shape

The project should move toward a split similar to this:

```text
src/
  AniNest.Core/
  AniNest.App/
  AniNest.Ports/
  AniNest.Infrastructure/
  AniNest.Platform.Windows/
  AniNest.Frontend.Wpf/
```

### `AniNest.Core`

Purpose:

- pure models
- value objects
- input protocol models
- feature state objects
- shared enums and contracts that do not depend on runtime infrastructure

Should contain examples such as:

- playlist item models
- metadata records and summaries
- player input gesture models
- state snapshot types

### `AniNest.App`

Purpose:

- application orchestration
- use-case sequencing
- coordination between services

Should contain examples such as:

- `LibraryAppService`
- `PlayerAppService`
- `PlayerSessionController`
- `MetadataSyncCoordinator`

This layer should depend on abstractions from `AniNest.Ports`, not on WPF or Windows APIs.

### `AniNest.Ports`

Purpose:

- abstractions for runtime services needed by the application layer

Should contain examples such as:

- playback engine interface
- main-thread dispatcher interface
- dialog and picker abstractions
- window/taskbar lifecycle abstractions
- renderer-agnostic playback event contracts

### `AniNest.Infrastructure`

Purpose:

- shared implementations that are not tied to one UI toolkit

Should contain examples such as:

- settings persistence
- metadata repositories
- file scanning
- thumbnail scheduling
- background workers that can remain cross-platform after process-launch assumptions are isolated

### `AniNest.Platform.Windows`

Purpose:

- Windows-only implementations

Should contain examples such as:

- taskbar auto-hide coordination
- Windows folder picker implementation
- Windows thumbnail process host
- any Win32-only lifecycle helpers

### `AniNest.Frontend.Wpf`

Purpose:

- existing WPF views
- WPF-only controls and animations
- WPF composition root
- WPF adapters that translate frontend concerns into backend calls

This project should become a shell over extracted backend services rather than the place where backend logic lives.

## The most important boundary change

The player contract must stop exposing UI surface types.

Current direction:

```text
backend -> WriteableBitmap / ImageSource -> WPF view
```

Target direction:

```text
backend -> playback state + commands + events
frontend -> owns video surface and rendering integration
```

This is the single most important extraction step because any future `mpv` integration will be frontend-surface-driven, not bitmap-binding-driven.

## Recommended replacement for the media contract

Replace the current `IMediaPlayerController` shape with a backend-facing playback contract that only describes behavior and state.

Example direction:

```text
IPlaybackEngine
  InitializeAsync()
  WarmupAsync()
  Load(filePath, startTimeMs)
  Play()
  Pause()
  Stop()
  SeekTo(timeMs)
  SeekForward(deltaMs)
  SeekBackward(deltaMs)
  SetRate(rate)
  SetVolume(volume)
  SetMute(isMuted)

PlaybackStateSnapshot
  IsPlaying
  CurrentTime
  TotalTime
  CurrentFilePath
  Rate
  Volume
  IsMuted

Playback events
  Playing
  Paused
  Stopped
  ProgressUpdated
  MediaChanged
  PlaybackFailed
```

Important constraint:

- no bitmap, texture, `ImageSource`, or WPF control references in this contract

The WPF frontend can keep a temporary adapter for the current `LibVLC + WriteableBitmap` rendering path while the backend is being extracted.

## Platform abstractions to add first

Before moving files between projects, extract the platform boundaries already implied by the code:

- `IMainThread` or keep `IUiDispatcher` with toolkit-neutral naming
- `IDialogService`
- `IFolderPickerService`
- `IWindowLifecycleService` or narrower window state abstractions
- `ITaskbarCoordinator` or a more generally named shell integration service

Some of these abstractions already exist in partial form:

- `IDialogService`
- `IUiDispatcher`
- `ITaskbarAutoHideCoordinator`

The main work is to:

1. move them to a backend-facing contracts area
2. rename them if their current names are too WPF-specific
3. stop direct framework calls from bypassing those interfaces

## Input model extraction

The current player input stack still relies on `System.Windows.Input` types.

That is acceptable inside the WPF shell, but not inside extracted shared backend code.

Recommended direction:

- keep backend input semantics in toolkit-neutral models
- map WPF key and mouse events to those models in the WPF frontend

For example:

- backend defines `PlayerInputAction`, `PlayerInputGesture`, `PlayerInputModifier`, `PlayerInputTriggerKind`
- WPF adapter translates `Key`, `MouseButton`, wheel input, and modifier state into those models

This avoids binding the future frontend to WPF key enums.

## Thumbnail subsystem expectations

The thumbnail subsystem should not be treated as fully platform-neutral yet.

The scheduling, indexing, and prioritization logic are good extraction candidates.
The process launching and decoder selection details may need platform-specific implementations.

Recommended split:

- cross-platform candidate:
  - queue state
  - task prioritization
  - index and cache bookkeeping
  - progress reporting
- platform-specific:
  - process controller
  - hardware decoder probing details
  - OS-specific process and path assumptions

This allows the architecture to preserve most of the thumbnail logic while making room for future non-Windows implementations.

## Phased plan

### Phase 1: backend de-WPF

Goal:

- remove direct WPF framework usage from backend-facing services

Work items:

- replace direct `Application.Current` and `Dispatcher` access with dispatcher abstraction
- replace direct `MessageBox` and folder picker usage with service abstractions
- stop exposing `WriteableBitmap` and `ImageSource` in shared service contracts
- isolate Windows-only helpers behind interfaces

Exit criteria:

- backend-facing services no longer reference `System.Windows`
- WPF app still compiles and runs

### Phase 2: contract cleanup

Goal:

- define stable boundaries that a future Avalonia frontend can consume

Work items:

- create playback engine contract
- create state snapshot and event contracts
- define frontend-agnostic input models
- move existing general abstractions into a dedicated ports/contracts area

Exit criteria:

- player and shell orchestration depend only on contracts, not WPF types
- contracts are understandable without opening WPF view files

### Phase 3: project split

Goal:

- physically separate shared backend code from the WPF shell

Work items:

- create `AniNest.Core`, `AniNest.App`, `AniNest.Ports`, and extracted `AniNest.Infrastructure` projects
- move WPF-only code into `AniNest.Frontend.Wpf`
- move Windows-only implementations into `AniNest.Platform.Windows`
- update dependency injection wiring

Exit criteria:

- shared backend projects build as plain `net9.0`
- WPF project targets `net9.0-windows` and references backend projects

### Phase 4: stabilization

Goal:

- confirm that extraction did not regress runtime behavior

Work items:

- restore or update tests after namespace and project moves
- verify player enter/leave transitions
- verify metadata synchronization
- verify thumbnail scheduling and progress reporting
- verify settings persistence and startup flow

Exit criteria:

- existing core workflows still work in the WPF app
- most backend tests run without WPF runtime dependencies

## Recommended file move order

Do not move everything at once.

Recommended order:

1. extract contracts and adapters
2. extract application services
3. extract data and persistence services
4. extract reusable infrastructure pieces
5. move the WPF shell last

This order keeps the application runnable while boundaries are still shifting.

## What should stay in WPF for now

The following should stay in the WPF shell during the extraction phase:

- XAML views
- WPF custom controls
- WPF animations and behaviors
- `MainWindow`
- current `LibVLC + WriteableBitmap` rendering adapter
- WPF event-to-input translation

Trying to replace the frontend at the same time would multiply the number of moving parts and make backend boundary mistakes harder to see.

## What should not be redesigned yet

Avoid these changes during extraction unless they are required to unblock the split:

- full Avalonia migration
- `mpv` integration
- broad namespace cleanup for style alone
- thumbnail algorithm redesign
- unrelated UI restyling

The purpose of this phase is boundary cleanup, not product redesign.

## Risks

Main risks:

- extracting too much at once and breaking the working app
- confusing frontend state objects with backend state objects
- preserving old WPF assumptions under new interface names
- letting temporary adapters become permanent architecture

Mitigations:

- keep phases narrow
- keep the WPF app running throughout the migration
- add tests when changing shared contracts
- prefer adapters over large one-shot rewrites

## Definition of done for the extraction phase

The backend extraction phase can be considered complete when all of the following are true:

- shared backend projects build without WPF references
- playback contracts no longer expose WPF image types
- platform-specific services are behind interfaces
- WPF remains a frontend shell rather than the owner of backend logic
- a new frontend can be started without first undoing old WPF coupling

## Suggested first implementation slice

If the work starts immediately, the first slice should be:

1. introduce a folder picker abstraction
2. route shell dialog usage through services only
3. replace direct dispatcher usage in app services
4. design the new playback engine contract
5. adapt current WPF playback rendering behind a temporary frontend adapter

This slice is small enough to land incrementally, but important enough to unlock the rest of the migration.
