# Overlay Selection Highlight

## Problem

Some submenu overlays use `SelectableOptionGroup` to render a moving highlight behind the selected option. When an overlay closed and reopened, the data selection could remain correct while the highlight visually snapped to the first row.

This showed up in the thumbnail performance menu as:

- actual runtime mode stayed `Balanced`
- `ShellViewModel` selection stayed on the balanced option
- the highlight bar sometimes rendered at the paused row

## Root cause

`SelectionHighlightAnimation` relied on passive `LayoutUpdated` timing. During overlay close and hidden layout transitions, child elements could momentarily report a valid size but a temporary `(0, 0)` position. That position was then reused on the next open if no later layout update corrected it.

## Architectural rule

- `ViewModel` owns the selected value
- `AnimatedOverlay` owns visible/open lifecycle
- `SelectionHighlightAnimation` owns highlight placement, but only when the target subtree is visible and layout-stable

Presentation state must not infer final geometry from hidden or closing layout passes.

## Fix strategy

1. `SelectionHighlightAnimation` ignores highlight updates when the highlight subtree is not visibly laid out.
2. `SelectionHighlightAnimation` exposes an explicit invalidation entry point for a subtree.
3. `AnimatedOverlay` consumers trigger highlight invalidation after the overlay `Opened` event so the final visible layout always gets one deterministic refresh.

## Regression checks

- change selected option, close overlay, reopen overlay
- repeat the sequence multiple times without restarting the app
- verify other overlays using `SelectableOptionGroup` still align correctly
