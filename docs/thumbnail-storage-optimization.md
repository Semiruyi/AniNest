# Thumbnail Storage Optimization Design

## Goal

This document defines a practical plan for reducing the amount of small-file disk IO produced by thumbnail generation in `AniNest`.

The current pipeline generates many individual thumbnail image files per video. That is simple and easy to inspect, but it is unfriendly to HDDs and still inefficient on SSDs when the library is large.

The target is:

- fewer filesystem objects per video
- more sequential writes and fewer scattered writes
- lower directory enumeration overhead
- compatibility with the current thumbnail queue and decode fallback design
- incremental delivery without a risky one-shot rewrite

In short:

> Keep thumbnail generation behavior, but stop treating every frame as a separate file on disk.

## Current Behavior

The current implementation stores thumbnails as one directory per video, containing many JPEG files:

- `ThumbnailRenderer.GenerateAsync` writes `tmpDir\%04d.jpg` through `ffmpeg`
- success moves or normalizes the temporary directory into the final thumbnail directory
- `ThumbnailFrameIndex` stores per-frame second offsets in `frames.json`
- `ThumbnailGenerator.GetThumbnailPath` resolves a frame to a concrete file path

### Current per-video layout

```text
thumbs/<md5>/
  0001.jpg
  0002.jpg
  0003.jpg
  ...
  frames.json   (keyframe-only mode)
```

## Problems

### 1. Too many small files

For a long video, generation can produce dozens or hundreds of JPEG files. Across a library, this becomes a large number of filesystem entries.

Effects:

- poor HDD performance due to seek-heavy writes
- extra filesystem metadata updates
- slower cleanup and migration
- worse cache locality during preview access

### 2. Temporary write amplification

The pipeline currently writes many files into `.tmp_<md5>` and then moves or renames them into the final directory.

Effects:

- large number of create/close operations
- extra directory churn
- work that is mostly metadata-bound instead of throughput-bound

### 3. Read-side overhead

The current read model depends on resolving a path and opening an individual image file for each preview request.

Effects:

- repeated open/read/close for tiny files
- directory scans such as `Directory.GetFiles(..., "*.jpg")`
- more pressure on filesystem caching than necessary

### 4. Storage format is tied to path-based consumption

`GetThumbnailPath(videoPath, second)` assumes the result is always a physical file path. That makes future storage changes harder than they need to be.

## Non-Goals

This design does not aim to:

- redesign thumbnail scheduling
- change decoder strategy selection
- introduce a database
- optimize for cross-process sharing
- build an in-memory image cache as the first step

Those can be layered on later.

## Design Principles

1. Reduce file count first.
2. Keep writes append-friendly and sequential where possible.
3. Preserve the existing queue and task model.
4. Ship in phases with compatibility for existing cached thumbnails.
5. Avoid changing UI behavior until storage is stable.

## Proposed Direction

Move from:

- many frame files per video

to:

- one thumbnail bundle per video, plus optional small metadata file

### Target per-video layout

```text
thumbs/<md5>/
  bundle.bin
  frames.json
```

Or, if metadata is embedded:

```text
thumbs/<md5>/
  bundle.bin
```

The main idea is that the image payloads for a video's thumbnail frames are stored in one file, not split across `0001.jpg`, `0002.jpg`, and so on.

## Candidate Approaches

### Option A: Bundle file with per-frame JPEG payloads

Store each generated frame as compressed image bytes inside a single binary file.

Example structure:

```text
Header
Frame table:
  - second
  - offset
  - length
Payload area:
  - jpeg bytes for frame 0
  - jpeg bytes for frame 1
  - ...
```

Pros:

- solves the small-file problem directly
- keeps random frame access
- easy to add versioning
- close to the current mental model of "many frames"

Cons:

- requires a new read API
- needs bundle writer and reader code

### Option B: Contact sheet or sprite atlas

Pack many frames into one larger image and store a coordinate index.

Pros:

- minimum file count
- very efficient for sequential browsing

Cons:

- less natural for arbitrary frame access
- UI side likely needs cropping support
- partial regeneration is awkward

### Option C: Keep files but reduce their count aggressively

Reduce sampling density and output size, but still store individual files.

Pros:

- lowest implementation cost
- good immediate relief

Cons:

- does not solve the root filesystem pattern
- large libraries still accumulate many small files

## Recommendation

Use a phased strategy:

1. immediate mitigation with a duration-based sampling policy
2. then introduce a single-file thumbnail bundle format
3. only after that, consider pipe-based generation or memory caching

For the main storage redesign, prefer **Option A: bundle file with per-frame payloads**.

It gives the best balance between:

- implementation complexity
- compatibility with current frame lookup behavior
- IO improvement
- future extensibility

## Phased Plan

## Phase 0: Immediate Relief

Goal:

- reduce disk writes before the storage format changes

Changes:

- replace fixed `fps=1` sampling with a duration-based precision policy
- optionally reduce thumbnail dimensions
- optionally raise JPEG quantization slightly
- stop scanning the thumbnail directory for frame count on normal startup when `TotalFrames` is already known

This phase is low risk and can be shipped independently.

### Suggested sampling policy

The sampling policy should be derived from seek-preview precision, not from an arbitrary frame-count cap.

The user expectation behind the progress bar is:

- shorter videos should allow finer preview control
- longer videos may tolerate coarser preview control
- the precision should degrade gradually and predictably as duration grows

### Experience anchors

The current working anchors are:

- `10 min` video: `0.5s` precision
- `20 min` video: `1s` precision
- `40 min` video: `2s` precision

These anchors imply a simple linear formula:

```text
samplingIntervalSeconds = durationMinutes / 20
```

Equivalent implementation form:

```text
samplingIntervalSeconds = durationSeconds / 1200
```

Examples:

- `5 min`: `0.25s`
- `10 min`: `0.5s`
- `20 min`: `1s`
- `23m41s` (`1421s`): about `1.184s`
- `40 min`: `2s`
- `60 min`: `3s`

This formula is attractive because it is easy to reason about and gives smooth progression between durations.

### Practical bounds

The raw formula should still be bounded so very short or very long videos do not become extreme.

Suggested first-version implementation:

```text
samplingIntervalSeconds = clamp(durationSeconds / 1200, 0.5, 5.0)
```

Meaning:

- minimum interval: `0.5s`
- maximum interval: `5s`

That preserves high precision for shorter videos and prevents extremely long videos from generating effectively unbounded frame counts.

### Important implication

This policy does **not** primarily optimize for the lowest possible frame count.

Instead, it optimizes for:

- predictable preview precision
- consistency with progress-bar interaction
- reduced write pressure relative to fixed `fps=1` on longer videos

For videos around `20` to `30` minutes, this may still produce roughly second-level sampling, which is acceptable because the real storage problem is the number of files, not only the number of frames.

### Relationship to `ffmpeg` `fps`

The implementation can convert the interval into `fps` as:

```text
fps = 1 / samplingIntervalSeconds
```

Examples:

- `0.5s` interval -> `fps=2`
- `1s` interval -> `fps=1`
- `2s` interval -> `fps=0.5`
- `3s` interval -> `fps=0.333333...`

## Phase 1: Introduce Bundle Storage

Goal:

- keep one or two files per video instead of many JPEG files

### New components

#### `ThumbnailBundleWriter`

Responsibilities:

- accept ordered frame payloads
- write a temporary bundle file
- finalize atomically into the target directory

#### `ThumbnailBundleReader`

Responsibilities:

- open a bundle
- resolve frame metadata
- read payload bytes for a selected frame

#### `ThumbnailFrameLocator`

This can either extend the current `ThumbnailFrameIndex` or replace part of its read logic.

Responsibilities:

- map requested second to nearest frame index
- return frame metadata rather than only file path

### Storage format sketch

Suggested first-version binary layout:

```text
Magic          8 bytes   ("ANITHMB1")
Version        4 bytes
FrameCount     4 bytes

Repeated Frame Table:
  Second       4 bytes
  Offset       8 bytes
  Length       4 bytes

Payload bytes...
```

Notes:

- use little-endian integers
- keep the header deliberately simple
- store offsets relative to the beginning of the payload file
- use versioning from day one

### Write flow

1. generate frames into a temporary area
2. enumerate generated images in order
3. write them into `bundle.bin.tmp`
4. write metadata or embed metadata
5. atomically move completed files into the final thumbnail directory
6. delete the temporary loose frame files

This keeps the first bundle implementation simple because it does not require changing the `ffmpeg` output mode yet.

## Phase 2: Read API Transition

Goal:

- decouple the rest of the app from physical thumbnail file paths

### Current API limitation

Current API:

- `GetThumbnailPath(string videoPath, int second)`

This assumes thumbnails are stored as files and consumed by path.

### Proposed direction

Add a new retrieval API for bundled storage, for example:

- `GetThumbnailBytes(videoPath, second)`
- `OpenThumbnailStream(videoPath, second)`
- `TryGetThumbnailFrame(videoPath, second, out ThumbnailFrameHandle frame)`

The exact shape depends on how the WPF image-loading path currently works, but the important part is:

- the consumer should request frame content
- the storage layer decides whether it comes from a loose file or a bundle

### Compatibility strategy

During transition, the generator should support both:

- legacy directory-of-jpg thumbnails
- new bundle-based thumbnails

That allows:

- old caches to continue working
- new caches to be written in the new format
- migration to happen lazily

## Phase 3: Optional Pipe-Based Write Path

Goal:

- eliminate temporary loose frame files as well

Instead of writing `%04d.jpg` to a temporary directory, use `ffmpeg` pipe output and package frames directly into the bundle writer.

Pros:

- lowest write amplification
- no temporary per-frame files

Cons:

- more complex parsing and frame-boundary handling
- cancellation and failure paths become trickier

This phase should wait until the bundle format is stable and tested.

## Data Model Changes

The existing `ThumbnailTask` model probably does not need large changes.

Possible additions:

- `StorageFormat`
- `BundleVersion`

But these may also be inferred from disk layout and kept out of `ThumbnailTask` for the first version.

### `ThumbnailIndex` considerations

Current `ThumbnailIndex` persists:

- `Md5`
- `State`
- `TotalFrames`
- `MarkedForDeletionAt`

That is mostly enough. The first bundle implementation can continue using the same task index, with only small updates if format tracking becomes necessary.

## Compatibility and Migration

Compatibility matters because users may already have existing thumbnail caches.

### Recommended first-version migration policy

- continue reading legacy thumbnail directories
- write new thumbnail generations in the new format
- do not force immediate migration on startup

### Optional lazy migration

When a legacy thumbnail directory is touched again:

- either leave it alone
- or regenerate into the new format when the task naturally re-runs

This avoids expensive bulk migration and keeps startup behavior calm.

## Failure Handling

Bundle writes must remain crash-safe.

### Requirements

- never replace a good bundle with a partially written one
- temporary files should be easy to detect and clean up
- cancellation should leave the previous thumbnail state intact

### Recommended approach

- write to `.tmp_<md5>` as today
- create `bundle.bin.tmp`
- finalize with move/replace only after the full bundle is valid
- clean temporary files on startup via existing temp cleanup logic

## Testing Strategy

## Unit tests

- frame lookup resolves the nearest frame correctly from bundle metadata
- bundle writer produces readable output for multiple frames
- reader rejects invalid magic/version safely
- startup can load legacy thumbnail directories
- startup can load bundle-based thumbnails

## Integration-style tests

- generating a thumbnail task writes one bundle instead of many JPEG files
- cancellation during bundle generation leaves no final broken bundle
- regeneration replaces a previous bundle cleanly
- old thumbnail cache still resolves preview frames during transition

## Risks and Tradeoffs

### Risk: UI path assumptions are deeper than expected

The current app may rely on file paths in more places than `ThumbnailGenerator`.

Mitigation:

- add the new content-based API before deleting the old path-based flow
- bridge old and new storage behind one service boundary

### Risk: Bundle reader adds complexity

Small files are simple to debug; bundle formats are not.

Mitigation:

- keep the format minimal
- document it clearly
- add a tiny debug helper later if needed

### Risk: First implementation still writes temporary loose frames

That means the full IO benefit is not realized immediately.

Mitigation:

- accept this as an intentional intermediate step
- prioritize correctness and compatibility first

### Risk: Lower frame count hurts seek-preview quality

Mitigation:

- tune by progress-bar precision, not by frame-count alone
- preserve sub-second or second-level sampling where shorter videos need it

## Recommended Implementation Order

1. Replace fixed `fps=1` with the duration-based precision formula.
2. Remove unnecessary directory scans for ready thumbnails when cached metadata is sufficient.
3. Add a new bundle format documentable with versioned header and frame table.
4. Implement bundle writer and reader.
5. Add a storage abstraction that can serve either loose-file or bundle-backed thumbnails.
6. Make new generations write bundles.
7. Keep legacy thumbnail reads working during transition.
8. Evaluate whether pipe-based generation is worth the extra complexity.

## Recommended First Coding Task

Start with Phase 0.

The best first slice is:

1. implement the precision formula `interval = clamp(durationSeconds / 1200, 0.5, 5.0)`
2. keep the current directory layout for now
3. verify that progress-bar preview still feels accurate on typical episode lengths

That gives immediate disk IO relief and prepares the system for the later bundle migration without forcing a wide API change on day one.
