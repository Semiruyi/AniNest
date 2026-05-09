# Thumbnail Storage Optimization

## Goal

This document records the current thumbnail storage design in `AniNest` after the small-file cleanup work.

The primary goals were:

- reduce filesystem object count per video
- reduce HDD-unfriendly scattered writes
- preserve seek-preview precision on the progress bar
- decouple thumbnail preview from physical jpg file paths
- make final cache commit more resilient

In short:

> Thumbnail generation should still behave the same to the user, but the cache should no longer be "one frame, one file".

## Current Result

The thumbnail pipeline has already been migrated to a bundle-based design.

### Current per-video layout

```text
thumbnails/<md5>/
  bundle.bin
```

The old "many jpg files plus metadata" layout is no longer the main runtime format.

## What Changed

### 1. Sampling precision is now duration-based

The old fixed `fps=1` policy was replaced with a duration-derived interval:

```text
samplingIntervalSeconds = clamp(durationSeconds / 1200, 0.5, 5.0)
fps = 1 / samplingIntervalSeconds
```

Anchors:

- `10 min` -> `0.5s`
- `20 min` -> `1s`
- `40 min` -> `2s`

This keeps preview precision aligned with what the user can reasonably control on the seek bar.

### 2. Preview mapping is millisecond-based

The preview path no longer maps hover position to a coarse whole-second request.

Current flow uses:

- progress-bar hover time in milliseconds
- nearest-frame lookup from stored frame positions in milliseconds

That makes the storage model match the actual preview interaction model more closely.

### 3. Thumbnails are stored in a single bundle file

Each frame is stored as JPEG bytes inside `bundle.bin`.

The bundle stores:

- magic
- version
- frame count
- per-frame `positionMs`
- per-frame payload `offset`
- per-frame payload `length`
- JPEG payload bytes

This keeps random access while eliminating thousands of tiny files across a library.

### 4. Generation no longer writes temporary `%04d.jpg`

This was the most important IO cleanup.

The current generation path is:

1. `ffmpeg` decodes and samples frames
2. `ffmpeg` writes JPEG output to `pipe:1`
3. `ThumbnailRenderer.ReadJpegFramesAsync(...)` splits the MJPEG byte stream into individual JPEG frames
4. `ThumbnailBundle.BundleWriter` appends each frame directly into bundle staging files
5. on success, the staged directory is promoted into the final thumbnail directory

That means generation no longer creates temporary per-frame jpg files on disk.

### 5. Preview reads bytes, not file paths

The read side now works like this:

1. UI asks for preview bytes at `positionMs`
2. `ThumbnailFrameIndex` resolves the nearest frame index from bundle metadata
3. `ThumbnailBundle.ReadFrameBytes(...)` reads the selected JPEG payload
4. UI decodes the bytes and shows the preview image

The important architectural change is:

- consumers ask for frame content
- storage decides how the bytes are obtained

## Current End-to-End Flow

### Generation path

`ThumbnailGenerator`
-> schedules a `ThumbnailTask`
-> selects a decode strategy chain
-> calls `ThumbnailRenderer.GenerateAsync(...)`

`ThumbnailRenderer`
-> computes duration and sampling policy
-> launches `ffmpeg`
-> reads JPEG bytes from `pipe:1`
-> extracts frame boundaries from the MJPEG stream
-> writes frames into `ThumbnailBundle.BundleWriter`
-> commits the bundle on success
-> promotes the staged directory into the final cache directory

### Read path

`ThumbnailPreviewController`
-> requests preview at a millisecond position
-> `ThumbnailGenerator.GetThumbnailBytes(videoPath, positionMs)`
-> `ThumbnailFrameIndex.ResolveFrameIndex(...)`
-> `ThumbnailBundle.ReadFrameBytes(...)`
-> UI decodes JPEG bytes into an image
-> preview is shown on screen

## Bundle Format

The current bundle uses a simple binary structure:

```text
Magic          8 bytes   "ANITHMB1"
Version        4 bytes
FrameCount     4 bytes

Repeated frame table:
  PositionMs   8 bytes
  Offset       8 bytes
  Length       4 bytes

Payload bytes...
```

Notes:

- little-endian integers
- versioned from the start
- frame positions are embedded in the bundle
- no separate `frames.json` is required for the current format

## Commit and Recovery Strategy

The final commit path is now more careful than a simple delete-and-move.

### Bundle file commit

`bundle.bin` promotion uses:

1. move old file to `.bak`
2. move staged file into place
3. delete backup on success
4. restore backup on failure

### Final directory commit

Thumbnail directory promotion uses:

1. move existing final directory to `.bak`
2. move staged directory into final location
3. delete backup on success
4. restore backup on failure

### Index file commit

`index.json` uses the same staged-file plus backup rollback strategy.

### Startup cleanup

Cleanup removes:

- `.tmp_*` thumbnail staging directories
- leftover `.bak` directories
- leftover `.bak` files under the thumbnail cache tree

## Keyframe-Only Path

Some videos still use keyframe-only extraction.

In that mode:

- `ffmpeg` uses `-skip_frame nokey`
- `showinfo` is parsed from stderr
- frames are still written through the same pipe-to-bundle path
- frame times are updated from parsed keyframe timestamps before final commit

So the storage pipeline is unified even though frame selection differs.

## Tests Added

The current implementation is covered by focused tests for:

- sampling interval and fps formatting
- sampled frame timestamp calculation
- JPEG frame extraction from MJPEG stream
- bundle writer roundtrip
- bundle frame-position update
- bundle file promotion and rollback
- final directory promotion and rollback
- frame index lookup from bundle metadata
- thumbnail index promotion and rollback

## Remaining Optional Work

The main storage redesign is complete enough for normal development use.

Possible follow-up work:

- add lightweight runtime logging for bundle payload growth during long renders
- benchmark old vs new generation throughput and disk IO
- optimize bundle read caching if preview decode becomes hot
- consider a debug inspection tool for `bundle.bin`

## Summary

The thumbnail pipeline is now materially different from the original design:

- no per-frame final jpg files
- no per-frame temporary jpg files during generation
- bundle-backed random access reads
- millisecond-based preview mapping
- more resilient final commit behavior

The practical effect is that thumbnail generation is much friendlier to disk IO while preserving progress-bar preview quality.
