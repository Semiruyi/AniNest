# UI Animation Architecture

## Goal

This document defines a target animation architecture for `AniNest`.

The app already has useful animation building blocks, but they are currently used through a mix of:

- low-level helper calls
- page-local `Storyboard` blocks
- one-off attached animators
- feature-specific animation helpers

That works for isolated features, but it does not yet form a clean animation system.

The goal of this design is to make animation usage:

- more consistent
- easier to reuse
- easier to reason about
- less dependent on page-local XAML choreography

In short:

> pages should mostly declare state changes, while animation infrastructure should own how those state changes look and feel.

## Problems in the Current Shape

### 1. Trigger style is inconsistent

Different parts of the UI animate in different ways:

- some use direct `Storyboard` triggers in XAML
- some use `AnimationHelper`
- some use attached animators such as `LoadedAnimator`
- some use specialized helpers such as `IconCrossfader`

This makes similar UI transitions feel like separate systems instead of one language.

### 2. The low-level pieces are reusable, but the page-level API is not

`AnimationHelper`, `AnimationEffect`, `EntranceEffect`, and `ExitEffect` are already decent primitives.

However, they are still too low-level for most feature work. A page author still has to decide:

- which helper to call
- when to call it
- which properties to animate
- how to coordinate enter and exit behavior

That is more animation policy than page code should own.

### 3. Common UI transitions do not have a standard home

Several recurring animation patterns show up throughout the app:

- small badges appearing and disappearing
- completion markers popping in
- visibility toggles for small overlays
- icon state swaps
- panel entry and exit

These should be standard animation components, not re-authored case by case.

### 4. The animation system does not yet encode app-wide style

The codebase has visual taste already, but the architecture does not yet formalize it.

Without standard animators and shared defaults, it is easy for durations, easing, scale ranges, and visual weight to drift.

## Design Principles

1. Pages declare state, not animation choreography.
2. Common micro-interactions should use reusable attached animators.
3. Enter and exit behavior should be designed as a pair.
4. Animation defaults should live in one place.
5. Low-level primitives should remain available, but should not be the first tool for common UI work.
6. Page-local `Storyboard` blocks should be reserved for truly custom cases.

## Target Layering

The target architecture has three layers.

### 1. Engine Layer

This layer owns raw animation execution and effect primitives.

Current members:

- `Presentation/Animations/AnimationHelper.cs`
- `Presentation/Animations/AnimationEffect.cs`

Responsibilities:

- animate core WPF properties such as opacity, scale, and translation
- provide shared easing defaults
- define reusable effect data such as `EntranceEffect` and `ExitEffect`
- remain small, stable, and UI-framework-oriented

This layer should not know about business features or specific page semantics.

### 2. State Animator Layer

This layer is the main missing piece today.

It should expose reusable attached animators that translate UI state changes into motion.

Examples:

- `ScaleFadeVisibilityAnimator`
- `ButtonScaleHover`
- `FadeVisibilityAnimator`
- `FadeTextSwapAnimator`
- `IconCrossfader`
- `LoadedAnimator`
- future `SlideVisibilityAnimator`

Responsibilities:

- watch declarative UI state such as visible/hidden or active/inactive
- apply standard enter and exit effects
- centralize animation defaults and state-transition policy
- provide page-friendly APIs

This is the layer that most feature XAML should talk to.

### 3. Page Layer

This layer is feature XAML and view code.

Responsibilities:

- declare state
- choose the correct standard animator
- avoid custom animation mechanics unless the effect is genuinely unique

A page should ideally say:

- this badge is active
- this overlay is visible
- this icon is selected

and let the animation infrastructure decide how that transition animates.

## What We Keep

### Keep `AnimationHelper`

`AnimationHelper` is a good base utility layer.

It already provides:

- shared easing helpers
- opacity animation helpers
- scale transform helpers
- entrance/exit application methods

This should remain the engine entry point for higher-level animators.

### Keep `AnimationEffect`, `EntranceEffect`, `ExitEffect`

These types are structurally useful because they encode:

- from/to values
- duration
- easing
- origin

They are the right shape for composing standard enter/exit motion.

### Keep `LoadedAnimator`, but narrow its role

`LoadedAnimator` should stay focused on first-load entrance only.

It should not become the general answer for ongoing state-driven visibility changes.

### Keep `IconCrossfader`, but classify it as specialized

`IconCrossfader` is valuable, but it is not a general visibility animator.

Its role should be:

- two-state icon transition
- state swap animation
- not general badge/pill/overlay visibility management

It may later be internally refactored to reuse shared state-animation primitives, but its external purpose can stay specialized.

## What We Add

The first new standard animator should be:

### `ScaleFadeVisibilityAnimator`

This animator should handle the most common micro-interaction in the app:

- an element appears
- it scales in
- it fades in
- later it scales out
- and fades out

Typical use cases:

- playlist thumbnail progress pie
- completion check markers
- corner badges
- tiny status chips
- lightweight overlays

#### Desired API shape

Example usage:

```xml
<Grid
    anim:ScaleFadeVisibilityAnimator.IsEnabled="True"
    anim:ScaleFadeVisibilityAnimator.IsActive="{Binding IsSomethingVisible}" />
```

Pages should use standard defaults by default.

Page XAML should not tune raw animation numbers such as:

- duration milliseconds
- easing functions
- scale start values

If a genuine exception appears later, it should be modeled as a named preset rather than ad hoc page-local numeric overrides.

Example future direction:

```xml
<Grid
    anim:ScaleFadeVisibilityAnimator.IsEnabled="True"
    anim:ScaleFadeVisibilityAnimator.IsActive="{Binding IsSomethingVisible}"
    anim:ScaleFadeVisibilityAnimator.Preset="Badge" />
```

#### Responsibilities

- manage opacity and scale together
- treat enter and exit as a pair
- support repeated toggling, not just initial load
- avoid requiring page-local `Storyboard` blocks for common cases
- use standard defaults when no overrides are provided

#### Non-responsibilities

- not a layout transition engine
- not a panel choreography system
- not a two-child crossfade manager
- not a substitute for custom hero or transition animation

## Standard Animator Catalog

This section defines the intended long-term roles of animation tools.

### `LoadedAnimator`

Use for:

- initial page or element entrance after load

Do not use for:

- repeated show/hide state transitions

### `ScaleFadeVisibilityAnimator`

Use for:

- small UI elements that appear and disappear
- badges, marks, tiny overlays, progress indicators

Do not use for:

- large layout transitions
- two-child visual swaps

### `ButtonScaleHover`

Use for:

- button hover and press scale feedback
- compact interactive controls that need tactile scale response

Do not use for:

- general visibility transitions
- content swap or icon swap animation

Configuration rule:

- page code should use defaults or a named preset
- page code should not set raw scale and duration numbers in ordinary XAML

Current presets:

- `Default`
- `Compact`
- `Menu`

### `FadeVisibilityAnimator`

Use for:

- cases where opacity-only is the right feel
- text or surfaces that should not scale

Do not use for:

- micro-badges that should feel more tactile

Configuration rule:

- page code should use defaults or a named preset
- page code should not set raw duration numbers

Current presets:

- `Default`
- `Emphasis`
- `Badge`

### `IconCrossfader`

Use for:

- mutually exclusive icon states
- on/off icon swap

Do not use for:

- generic visibility toggles for standalone elements

Configuration rule:

- page code should use defaults or a named preset
- page code should not set raw duration numbers

Current presets:

- `Default`
- `Emphasis`

### `FadeTextSwapAnimator`

Use for:

- text content swaps where the old and new value should cross-fade
- compact title or label changes

Do not use for:

- general element visibility
- icon state transitions

Configuration rule:

- page code should use defaults or a named preset
- page code should not set raw duration numbers

Current presets:

- `Default`
- `Emphasis`

## Default Motion Style

## Preset Vocabulary

Preset naming should stay consistent across animators whenever the intent is shared.

Shared preset names:

- `Default`
  - the standard motion profile for that animator
- `Emphasis`
  - a more noticeable or slower variant used when the transition carries more visual weight

Animator-specific preset names are allowed only when the component has a truly specialized role.

Current specialized preset:

- `Badge`
  - reserved for compact scale-fade badge or marker behavior in `ScaleFadeVisibilityAnimator`
- `Compact`
  - reserved for smaller hover and press scale feedback in `ButtonScaleHover`
- `Menu`
  - reserved for popup or menu-style press-first button interaction in `ButtonScaleHover`

These defaults are intended as app-level animation guidance.

They can evolve, but standard animators should begin here.

### Small micro-elements

Examples:

- progress pie
- check mark
- tiny status badge

Recommended defaults:

- enter duration: `220-260ms`
- exit duration: `160-220ms`
- from scale: `0.80-0.90`
- enter easing: `EaseOut`
- exit easing: `EaseIn`
- origin: center

### Medium overlays and small panels

Examples:

- popup fragments
- contextual floating controls

Recommended defaults:

- enter duration: `220-300ms`
- exit duration: `180-240ms`
- scale should be subtle unless the surface is intentionally playful

### Text-first transitions

Examples:

- content label swaps
- compact readouts

Recommended defaults:

- opacity-first
- avoid unnecessary scale unless the UI wants a stronger pop

## Usage Rules for Feature Code

1. Prefer standard attached animators before writing a new `Storyboard`.
2. Use engine helpers directly only when building a reusable animator or a clearly custom transition.
3. Keep page XAML focused on state bindings, not animation mechanics.
4. Do not expose raw animation tuning values in ordinary page XAML. Prefer defaults first, presets second.
5. Do not explicitly set a `Default` preset in page XAML. Omit the preset unless a non-default intent is needed.
6. For repeated visibility changes, do not use load-only animation tools.

## Storyboard Exceptions

The architecture does not ban `Storyboard` usage outright.

Page-local `Storyboard` blocks are still reasonable when the animation is:

- tightly coupled to a specific control template
- about brush or color transitions rather than reusable visibility/state motion
- a bespoke transition that would become awkward or misleading if forced into a generic animator

Examples that are still reasonable:

- hover and pressed color shifts inside a button template
- template-specific emphasis transitions for selection states
- larger page choreography that is intentionally custom

Examples that should usually move to standard animators instead:

- small badge appear/disappear motion
- completion marker pop-in
- lightweight visibility toggles
- generic icon state visibility transitions

## Migration Strategy

The architecture should be adopted incrementally.

### Phase 1

- write this architecture document
- define standard animator roles
- agree on default motion language

### Phase 2

- implement `ScaleFadeVisibilityAnimator`
- validate it on a small, concrete feature

Recommended first adopter:

- player playlist thumbnail progress pie

This is a good pilot because:

- the visual target is small and contained
- the desired animation is clear
- the state changes are frequent enough to test repeated entry and exit behavior

### Phase 3

- migrate similar micro-elements to the same animator
- reduce page-local duplicated badge animation patterns

### Phase 4

- revisit existing animators such as `FadeVisibilityAnimator` and `IconCrossfader`
- align internal implementation where beneficial
- keep external behavior stable where it already matches the app well

## Storyboard Audit Snapshot

This section records the current review of page-local storyboard usage so future cleanup work can stay intentional.

### Keep

These usages are currently reasonable to keep as page-local or template-local storyboard logic.

#### Shared button hover fades

Files:

- `View/Styles/SharedStyles.xaml`

Reason:

- these are template-local hover opacity transitions
- they are tightly coupled to button background visuals
- they are not generic visibility-state animators

Classification:

- keep

#### Player episode button background color transitions

Files:

- `Features/Player/PlayerPage.xaml`

Reason:

- these are template-specific hover, press, played, and selected color transitions
- they are tied to one control template's brush behavior
- forcing them into generic state animators would not make the system clearer

Classification:

- keep

#### Menu selection indicator slide transitions

Files:

- `View/MainWindow.xaml`

Reason:

- these storyboards move a specific selection background between menu rows
- this is a small piece of bespoke layout choreography
- it is not the same category as micro badge or visibility-state motion

Classification:

- keep for now

### Migrated

These usages used to fit the old mixed model and are now intentionally handled by standard state animators.

#### Playlist thumbnail progress pie

Files:

- `Features/Player/PlayerPage.xaml`
- `Presentation/Animations/ScaleFadeVisibilityAnimator.cs`

Previous shape:

- page-local visibility plus bespoke motion

Current shape:

- `ScaleFadeVisibilityAnimator`

Classification:

- migrated

#### Playlist thumbnail ready check

Files:

- `Features/Player/PlayerPage.xaml`
- `Presentation/Animations/ScaleFadeVisibilityAnimator.cs`

Previous shape:

- page-local pop-in storyboard

Current shape:

- `ScaleFadeVisibilityAnimator`

Classification:

- migrated

#### Title bar player-file visibility fade

Files:

- `View/MainWindowTitleBar.xaml`
- `Presentation/Animations/FadeVisibilityAnimator.cs`

Previous shape:

- attached animator with raw duration override

Current shape:

- `FadeVisibilityAnimator` with default-or-preset rule

Classification:

- migrated to preset-based API

#### Play/pause icon swap timing

Files:

- `Features/Player/ControlBarView.xaml`
- `Presentation/Animations/IconCrossfader.cs`

Previous shape:

- specialized animator with raw duration override

Current shape:

- `IconCrossfader` with default-or-preset rule

Classification:

- migrated to preset-based API

### Future Review Targets

These are not urgent migrations, but they are worth revisiting when the animation layer grows.

#### Popup menu hover color states

Files:

- `View/Styles/SharedStyles.xaml`

Why revisit later:

- they may remain correct as-is
- but if a richer shared button-state animation layer appears, these could become candidates for consolidation

Current decision:

- leave in place

#### Menu selection highlight movement

Files:

- `View/MainWindow.xaml`

Why revisit later:

- if the app later develops a reusable "selection indicator slide" pattern, this could move into a dedicated animator
- today it is still specific enough that keeping the local storyboard is clearer

Current decision:

- leave in place

## Current Recommendation

For the next implementation step, do not redesign the entire animation engine.

Instead:

1. keep `AnimationHelper` and `EntranceEffect/ExitEffect`
2. add `ScaleFadeVisibilityAnimator` as the standard reusable state animator
3. adopt it first on the player playlist thumbnail progress pie
4. use that implementation as the reference pattern for future micro-visibility transitions

This keeps the refactor focused, practical, and aligned with the animation direction the app already has.
