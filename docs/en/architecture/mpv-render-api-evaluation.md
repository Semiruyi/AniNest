# MPV Render API Evaluation

## Goal

This document evaluates whether `AniNest` should move from the current `LibVLC + WriteableBitmap` player path to an `mpv + render API + GPU surface` integration.

The focus is not "can `mpv` play video", but whether it can satisfy all of the following at the same time:

- no WPF airspace issue
- better rendering efficiency than CPU frame upload
- compatibility with the current overlay-heavy player UI
- a realistic implementation and maintenance cost for this project

## Current State

`AniNest` currently uses `WPF + LibVLC` as the playback stack.

- project dependency: `LibVLCSharp.WPF` and `VideoLAN.LibVLC.Windows`
- player view: the video area is a WPF `Image`
- media path: `MediaPlayerController` exposes `WriteableBitmap`
- frame delivery: `VideoFrameProvider` receives decoded frames through LibVLC video callbacks and writes them into the bitmap on the WPF dispatcher

This has one important upside:

> The current player surface already lives inside the WPF visual tree, so normal overlay UI can appear above the video without classic child-window airspace issues.

But it also has a practical ceiling:

- frame data is copied into managed buffers
- the WPF UI thread participates in presentation
- high-resolution and high-refresh playback has limited headroom
- the architecture is friendlier to "works everywhere" than to "maximum rendering efficiency"

In short:

> The current path solves airspace by treating video as UI content, but it pays for that with CPU-side frame movement.

## Problem Statement

The project wants a stronger player surface without giving up the current UI style.

More specifically:

- the player page relies on overlay UI, animation, and input layers
- the app should avoid `HwndHost`-style airspace constraints
- performance should improve rather than regress
- the rendering path should remain stable across page transitions, fullscreen changes, and long playback sessions

This makes the obvious "just embed another native player window" option unattractive.

## Options Considered

### Option A: keep the current `LibVLC + WriteableBitmap` path

Pros:

- already integrated
- no new native interop layer
- no airspace problem

Cons:

- per-frame CPU upload remains the long-term bottleneck
- limited headroom for higher resolutions and refresh rates
- the project keeps paying presentation cost on the WPF side

This option is the lowest-risk path, but it is not the strongest long-term rendering architecture.

### Option B: use `mpv` as a child window

This usually means native window embedding through a host window handle.

Pros:

- relatively direct way to get `mpv` on screen
- likely strong playback performance

Cons:

- brings back child-window composition constraints
- overlay freedom becomes worse
- reintroduces the exact class of airspace problems the current player already avoids

This option fails one of the primary goals and is therefore not preferred.

### Option C: use `mpv + render API + GPU surface`

This means:

- `mpv` handles decode and playback timing
- the host application owns the render target
- rendered video is bridged into a WPF-visible GPU surface
- WPF composes the rest of the UI above it

Pros:

- preserves in-tree composition and overlay freedom
- removes the need for CPU-side frame upload as the primary presentation path
- opens the door to a higher-performance renderer

Cons:

- implementation is substantially harder than both Option A and Option B
- the hard part is not playback control, but graphics interop and lifecycle stability
- WPF is not the most natural host for modern GPU interop

This is the most promising option for end-state quality, but also the most expensive one.

## Feasibility

## Practical answer

This path is feasible.

More precisely:

- `mpv` render API itself is mature enough for embedded rendering
- the hard engineering work lies in bridging the rendered GPU output into WPF
- this route has been demonstrated by community implementations, but it should still be treated as an advanced integration rather than a drop-in package swap

So the practical conclusion is:

> `mpv + render API + GPU surface` is not speculative, but it is also not a cheap migration.

## Why it is feasible

### 1. The render API is designed for host-owned rendering

The `mpv` render API is specifically intended for applications that want to render video inside their own surface instead of allowing `mpv` to own a top-level or child window.

That means the application can:

- own the render context
- own the target texture or framebuffer
- decide when to render
- compose video with its own UI

This is the right integration model for a WPF app that cares about overlays and custom presentation.

### 2. The current project architecture is already UI-composition-oriented

`AniNest` is not built around a native child video window.

It already assumes:

- video is part of the page
- controls and overlays can sit above it
- transitions and fullscreen behavior belong to application logic

Because of that, the move to a GPU-backed in-tree surface is conceptually aligned with the current design. The project is not changing product direction; it is upgrading the rendering path under the same product shape.

### 3. Community evidence suggests the route is workable

There are existing community efforts that expose `mpv` to WPF by bridging OpenGL-oriented render output into a WPF-compatible surface.

That does not prove the route is effortless, but it does show that:

- the integration is technically achievable
- the graphics bridge can be made to work
- the main concerns are stability and maintenance, not theoretical impossibility

## Rendering Principle

The expected architecture is roughly:

```text
Media file
  -> libmpv decode / timing / playback state
  -> mpv render context
  -> host-owned GPU render target
  -> interop bridge
  -> WPF-visible GPU surface
  -> WPF visual tree composition
  -> overlays / controls / popups / animations
```

In practical terms, the flow would look like this:

1. Create `mpv_handle`
2. Create `mpv_render_context`
3. Configure the render backend expected by `mpv`
4. Let `mpv` render into a GPU target owned by the app
5. Bridge that target into a WPF-visible surface
6. Present the video surface inside the player page
7. Let the rest of the WPF UI render above it normally

The essential idea is simple:

> `mpv` renders the video, but the application owns the surface.

That ownership is what removes the need for a child window and therefore avoids classic airspace behavior.

## Likely WPF Graphics Bridge

The most realistic WPF-oriented implementation is not "mpv renders directly to a native WPF object". WPF does not provide such a modern direct path.

Instead, the likely bridge is:

- `mpv` render API on an OpenGL-oriented path
- interop or translation layer to Direct3D
- a WPF-visible `D3DImage`-style surface

Possible implementation families include:

- OpenGL -> Direct3D interop
- ANGLE-backed translation to a Direct3D-friendly path

The exact implementation detail should be decided by a prototype, not by assumption alone.

## Expected Work Items

### 1. Technical spike

Goal:

- prove that `mpv` can render into a WPF-visible GPU surface in this project environment

Deliverables:

- a minimal host control
- local file playback
- basic render loop
- resize handling

This phase should answer one question only:

> Is the rendering bridge stable enough to justify integration work?

### 2. Core interop layer

Goal:

- build a reusable player surface abstraction for `AniNest`

Likely responsibilities:

- native handle ownership
- render context lifecycle
- device creation and recreation
- render invalidation
- surface resize
- teardown order

### 3. Playback service integration

Goal:

- replace the current `MediaPlayerController` path without changing the player feature contract more than necessary

Likely responsibilities:

- play / pause / stop / seek
- rate change
- playback events
- current item switching
- position and duration reporting

### 4. UI integration

Goal:

- preserve the current player experience while swapping the rendering core

Likely responsibilities:

- bind the new surface into `PlayerPage`
- preserve overlay layering
- keep fullscreen and transition timing correct
- keep input routing predictable

### 5. Stability pass

Goal:

- make the system reliable enough for daily use

Likely responsibilities:

- resize stress
- fullscreen entry and exit
- alt-tab and focus changes
- monitor and DPI changes
- device lost or driver resets
- suspend and resume edge cases
- cleanup during page leave and app shutdown

This phase is likely to consume more time than the initial "it renders" milestone.

## Estimated Workload

These estimates assume one developer who is comfortable with `C#` and `WPF`, but is not already carrying a finished `mpv + WPF GPU interop` stack.

### Prototype and technical validation

Estimated time:

- `3 to 7 days`

Expected outcome:

- video can render in a WPF test surface
- basic playback works
- the team can decide whether the route is worth deeper investment

### Initial project integration

Estimated time:

- `1 to 2 weeks`

Expected outcome:

- the new render path is integrated into the player page
- basic playback controls work
- switching videos and entering the player are functional

### Stabilization to internal daily-use quality

Estimated time:

- `2 to 4 weeks`

Expected outcome:

- the player survives common windowing and lifecycle events
- regressions around fullscreen, resizing, and teardown are reduced to a manageable level

### Reaching product-quality polish

Estimated time:

- `1 to 2 additional weeks`

Expected outcome:

- the render path behaves well with the project's animation and overlay style
- performance and stability tradeoffs are tuned rather than merely tolerated

## Overall estimate

Reasonable total estimate:

- `4 to 8 weeks`

Useful shorthand:

- prove it works: `3 to 7 days`
- make it usable inside the project: `2 to 3 weeks`
- make it feel reliable and polished: `4 to 8 weeks`

These numbers are not guarantees. The biggest variable is graphics interoperability stability across machines.

## Expected Final Result

If the work succeeds, the likely end-state is:

- video remains inside the WPF composition model
- overlay UI can sit above the player naturally
- performance is meaningfully better than CPU-side bitmap upload
- the player architecture is better suited for higher-end rendering work later

From a user-facing perspective, the result should feel like:

- a more modern video surface
- cleaner overlay composition
- less CPU pressure during playback
- better headroom for more demanding video content

## What this path does not automatically guarantee

Even with a successful migration, this path does not automatically guarantee:

- zero driver-specific issues
- perfect behavior on every GPU and monitor configuration
- effortless HDR support
- lower maintenance cost than the current stack

The route improves the ceiling, but it also raises the complexity floor.

## Recommendation

The current recommendation is:

1. do not commit to a full migration before a render-surface spike
2. treat the spike as a yes-or-no technical gate
3. proceed only if the WPF GPU bridge proves stable enough on target machines

If the project's priorities are:

- overlay freedom
- stronger rendering performance
- long-term player quality

then this route is worth serious evaluation.

If the project's priorities are instead:

- minimum engineering risk
- shortest path to a stable release
- lowest maintenance burden

then the current player path remains the safer default.

## Suggested Next Step

Create a dedicated proof-of-concept branch with a narrow goal:

- one player surface
- one local file
- one WPF host
- basic resize and fullscreen handling

The purpose of that branch is not to finish the migration. It is to determine whether the graphics bridge is solid enough to deserve the rest of the work.

## References

- `mpv` render API header:
  - <https://raw.githubusercontent.com/mpv-player/mpv/master/include/mpv/render.h>
- `libmpv` embedding docs:
  - <https://www.mintlify.com/mpv-player/mpv/embedding/libmpv>
- `mpv` examples:
  - <https://github.com/mpv-player/mpv-examples/blob/master/libmpv/README.md>
- `mpv` manual:
  - <https://mpv.io/manual/stable/>
- WPF `D3DImage`:
  - <https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.d3dimage>
- Community `WPF + mpv` implementation reference:
  - <https://github.com/vrjure/LibMPV.AutoGen>
