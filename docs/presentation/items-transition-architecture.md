# Items Transition Architecture

## Goal

This document defines a target architecture for animated item transitions in `AniNest`.

The immediate driver is the library card grid:

- cards should animate when they appear
- cards should animate when they disappear
- remaining cards should smoothly reflow into their new positions

The broader goal is to make this behavior reusable for any future item-based surface, not only the library page.

## Problem Summary

The current implementation already has useful animation pieces, but they do not form a complete list-transition system.

Today the library page uses:

- `ItemsControl` to render cards
- `AnimatedWrapPanel` to animate layout movement
- attached animators such as `ScaleFadeVisibilityAnimator` for small element-level state changes

That setup is good at animating:

- a small child element becoming visible or hidden
- an existing item moving from one position to another

It is not good at animating:

- an item leaving the bound collection
- an item entering while other items are reflowing
- enter, exit, and move transitions as one coordinated interaction

In short:

> the app has element animation and layout animation, but it does not yet have collection-item lifecycle animation.

## Why the Current Shape Fails

### 1. Removed items disappear before they can animate out

`MainPageViewModel.ApplyCurrentFilter()` updates `FolderItems` by directly:

- removing old items
- moving existing items
- inserting new items

Once an item is removed from the bound collection, its visual container is removed from the live visual tree. At that point there is nothing left to animate for exit.

This is the main reason the card grid currently has no proper disappearance animation.

### 2. `AnimatedWrapPanel` only sees live children

`AnimatedWrapPanel` captures positions of the children that still exist during arrange and then generates:

- entrance animations for newly seen children
- move animations for children whose positions changed

That works for live layout transitions, but a removed child is already gone before the panel performs the next layout pass.

So the panel can animate:

- move
- first appearance of a new live child

but it cannot animate:

- exit of a removed child

This is not a bug in the panel. It is a responsibility mismatch.

### 3. Visibility animators operate at the wrong level

`ScaleFadeVisibilityAnimator`, `FadeVisibilityAnimator`, and `LoadedAnimator` are useful state animators, but they are designed for:

- one element
- one visible/hidden state
- one owned lifecycle within the visual tree

They do not own collection membership, item container retention, or layout release timing.

That means they cannot solve the core library-card problem by themselves.

### 4. Multiple animation layers currently compete for `RenderTransform`

Several animation helpers and animators directly assign or replace `RenderTransform`.

This is manageable when a surface uses only one transform-driven behavior, but it becomes fragile when the same element needs multiple simultaneous effects such as:

- lifecycle scale and fade
- layout translation during reflow
- hover or press feedback

Without a transform-composition strategy, these behaviors can overwrite each other or become order-dependent.

## Design Goals

The target architecture should satisfy the following goals.

1. Item appearance, disappearance, and movement should all animate coherently.
2. Exit animation should work even after the logical item has been removed from the collection.
3. Layout should be released promptly so surrounding items can reflow immediately.
4. Lifecycle animation, layout animation, and interaction animation should not fight over transforms.
5. The solution should be reusable for more than one page.
6. Feature pages should mostly declare intent, not manually choreograph list transitions.

## Design Principles

### 1. Collection lifecycle is a separate concern from element visibility

An element becoming hidden is not the same thing as an item leaving a collection.

Visibility animators should remain focused on local state transitions. Collection membership changes need a dedicated list-transition layer.

### 2. Layout movement and lifecycle motion should be separated

The system should treat these as distinct responsibilities:

- item lifecycle motion: enter and exit
- layout motion: reflow and repositioning

Trying to make one component own both concerns leads to awkward timing and hidden coupling.

### 3. Exit animation requires temporary visual retention

If an item should animate out after being logically removed, some representation of that item must remain on screen long enough to play the exit effect.

That temporary representation should not continue participating in layout.

### 4. Transform ownership must be explicit

Scale, translation, and future interaction transforms should be composed through stable slots or layers rather than ad hoc `RenderTransform` replacement.

## Target Architecture

The target architecture has three cooperating layers.

## 1. Transitioning Items Layer

This is the new primary missing layer.

Recommended component:

- `TransitioningItemsControl`

Responsibilities:

- observe item-collection changes
- create live item containers
- retain removed items temporarily as exit visuals
- coordinate enter, exit, and move timing
- expose simple page-level configuration

Conceptually it owns two surfaces:

- `LiveItems`: items that still participate in layout
- `GhostItems`: items that have been removed logically but are still animating out visually

The ghost layer is the key to correct exit animation.

### Why this layer is needed

This layer solves the gap between:

- the ViewModel updating a collection immediately
- the UI still needing the old visual long enough to play an exit animation

Without this layer, exit animation is impossible or becomes a brittle page-local workaround.

## 2. Layout Animation Layer

Recommended component:

- keep `AnimatedWrapPanel`

Responsibilities:

- animate movement of live items as positions change
- remain responsible for layout translation only
- stop pretending to own exit animation

This panel remains valuable, but with a narrower and clearer job.

In the target system it should primarily handle:

- reflow after insertion
- reflow after removal
- reflow after reorder

It should not be the only place where list animation policy lives.

## 3. Transform Composition Layer

Recommended component:

- a small transform-composition helper such as `TransformComposer`

Responsibilities:

- provide named transform slots
- let multiple animation systems share one visual safely
- avoid accidental `RenderTransform` replacement

Suggested conceptual slots:

- `LifecycleScale`
- `LayoutTranslate`
- `InteractionScale`

This is the foundation that lets lifecycle animation and layout animation coexist on the same element without collisions.

## Recommended Enter / Exit / Move Model

### Enter

When a new item appears:

1. create the live container
2. place it in the layout immediately
3. start with lifecycle visuals such as:
   - opacity `0`
   - scale `0.92`
   - slight positive `Y` offset if desired
4. animate to the resting state

This produces a readable appearance animation without delaying layout participation.

### Exit

When an item is removed:

1. remove the item from the live layout layer
2. create or preserve a ghost visual at the old screen position
3. let surrounding live items reflow immediately
4. animate the ghost out using opacity and scale
5. destroy the ghost after completion

This is the critical interaction pattern because it solves two needs at the same time:

- the removed card visibly exits
- the remaining cards start moving right away

### Move

When live items change position because of insertion, removal, or reorder:

1. capture old and new layout positions
2. compute translation deltas
3. animate only the layout-translation slot back to zero

This keeps move behavior independent from enter and exit state.

## Recommended Page-Level API

The goal is for feature XAML to describe intent, not manual choreography.

Example direction:

```xml
<primitives:TransitioningItemsControl
    ItemsSource="{Binding FolderItems}"
    ItemTemplate="{StaticResource LibraryFolderCardItemTemplate}">
    <primitives:TransitioningItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
            <primitives:AnimatedWrapPanel HorizontalAlignment="Center" />
        </ItemsPanelTemplate>
    </primitives:TransitioningItemsControl.ItemsPanel>
</primitives:TransitioningItemsControl>
```

Preferred configuration style:

- use defaults first
- use named presets second
- avoid page-local raw numbers unless a surface is genuinely exceptional

## Why This Design Is Better

### It fixes the real missing capability

The current problem is not "the easing is wrong" or "the panel needs more tuning".

The real problem is that collection removal destroys the visual too early.

This design directly addresses that.

### It separates responsibilities cleanly

Each layer has a clear role:

- transitioning items layer owns collection lifecycle
- layout layer owns movement
- transform composition layer prevents transform conflicts

That separation makes the system easier to reason about and easier to extend.

### It improves behavior under fast repeated changes

The library page can change quickly because of:

- filter switches
- favorite toggles
- status changes
- delete actions

A transition-aware items layer can manage interruption, cancellation, and overlapping updates much more predictably than page-local ad hoc animation code.

### It scales to other surfaces

The same architecture can later support:

- player-side card strips
- thumbnail collections
- future grid or flow-based item surfaces

That makes the effort architectural rather than one-off.

## Problems This Design Solves

This design is intended to solve the following concrete problems.

### 1. No exit animation for removed cards

Solved by:

- ghost-item retention after logical removal

### 2. Enter and move animations feeling disconnected

Solved by:

- one transition layer coordinating item lifecycle with panel reflow

### 3. Layout blocking while waiting for exit animation

Solved by:

- removing the item from the live layout immediately
- animating only a retained ghost visual

### 4. `RenderTransform` conflicts between animation systems

Solved by:

- explicit transform composition slots

### 5. Re-implementing list transition policy page by page

Solved by:

- a reusable `TransitioningItemsControl`

## Problems This Design Avoids

The design is also valuable because it avoids several bad long-term outcomes.

### 1. Polluting item ViewModels with visual lifecycle state

A tempting shortcut is to add flags such as:

- `IsVisible`
- `IsExiting`
- `PendingRemoval`

to item models or page ViewModels.

That makes visual timing a ViewModel responsibility and couples UI choreography to domain-facing state.

This architecture keeps that policy in the presentation layer where it belongs.

### 2. Teaching the panel to do too much

Trying to force `AnimatedWrapPanel` to own enter, exit, ghost retention, and layout timing would make it both more fragile and less reusable.

The panel should stay a layout-motion component, not become a general collection-transition engine.

### 3. Continuing transform replacement as a hidden convention

As the animation system grows, implicit `RenderTransform` ownership becomes harder to reason about.

Introducing composition now prevents future animation work from turning into local transform fights.

## Implementation Direction

The recommended rollout is incremental.

### Phase 1

- introduce this design document
- agree on layer boundaries
- define transform-composition strategy

### Phase 2

- add transform-composition infrastructure
- adapt `AnimationHelper` and `AnimatedWrapPanel` to use it

### Phase 3

- add `TransitioningItemsControl`
- support live items plus ghost exit visuals

### Phase 4

- migrate the library page from `ItemsControl` to `TransitioningItemsControl`
- keep `AnimatedWrapPanel` as the items panel
- tune timing and presets on the real surface

### Phase 5

- evaluate reuse for other item-based surfaces

## Implementation Status

The first implementation wave is now in place for the library card grid.

### Completed

- add `TransitioningItemsControl`
- retain removed items temporarily as ghost visuals for exit animation
- keep `AnimatedWrapPanel` focused on live-item movement
- migrate the library page from `ItemsControl` to `TransitioningItemsControl`
- add `TransformComposer`
- route shared scale and layout-translate animation paths through transform composition

### Current Shape

The library page now uses:

- `TransitioningItemsControl` for item enter and exit
- `AnimatedWrapPanel` for live-item reflow
- `TransformComposer` so lifecycle scale and layout translation can coexist

This means the library grid now has the intended architectural split:

- collection lifecycle
- layout movement
- transform composition

### Not Done Yet

- broader reuse on other item-based surfaces
- richer preset vocabulary if more list surfaces appear
- focused UI-level verification for rapid repeated changes and interruption behavior

## Initial Timing Guidance

These defaults should feel aligned with the current app motion language.

### Card enter

- duration: `220-260ms`
- easing: `EaseOut`
- start scale: `0.90-0.94`

### Card exit

- duration: `160-200ms`
- easing: `EaseIn`
- end scale: `0.86-0.92`

### Card move

- duration: `240-320ms`
- easing: `EaseOut`

These numbers are guidance, not a hard API contract.

## Success Criteria

The architecture should be considered successful when the library page behaves like this:

1. switching filters animates removed cards out
2. remaining cards reflow immediately and smoothly
3. newly visible cards animate in coherently
4. repeated rapid state changes do not leave broken ghost visuals behind
5. hover and press interactions can coexist with list transitions without transform conflicts

## Final Recommendation

Do not treat the library-card problem as a missing flourish inside `AnimatedWrapPanel`.

Treat it as the absence of a dedicated collection-transition layer.

The right architecture is:

1. keep element visibility animators for local state changes
2. keep `AnimatedWrapPanel` for live layout movement
3. add a `TransitioningItemsControl` for item lifecycle transitions
4. add transform composition so these layers can cooperate safely

This design is worth doing because it does more than add animation polish.

It makes list behavior:

- visually coherent
- technically predictable
- reusable across the app
- easier to extend without page-local hacks
