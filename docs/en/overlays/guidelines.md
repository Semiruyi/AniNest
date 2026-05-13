# Overlay Guidelines

## Goal

AniNest now has two floating-UI foundations with different responsibilities:

- `AnimatedOverlay` + `OverlayCoordinator`
- `AnimatedPopup` + `PopupInputCoordinator`

This document defines when to use each one and which interaction preset to start from.

## Choose the right foundation

### Use `AnimatedOverlay` for interactive floating UI

Use `AnimatedOverlay` when the surface contains commands, selection, settings, editing, or nested overlay behavior.

Typical examples:

- title bar menus
- library card context menus
- settings submenus
- player speed menu
- future player tool panels

Why:

- tree-hosted, easier to reason about with the rest of the view
- unified outside-click arbitration
- explicit close reasons
- child overlay chain support
- reusable interaction presets

### Use `AnimatedPopup` for presentation-only popup visuals

Use `AnimatedPopup` when the popup is mainly visual feedback and should keep native `Popup` hosting behavior.

Typical examples:

- seek bar thumbnail preview
- simple hover tooltip-like previews

Do not use `AnimatedPopup` for:

- command menus
- settings panels
- context menus
- interaction-heavy tool panels

If the popup needs custom outside-click semantics, anchor toggle semantics, or child overlay behavior, prefer `AnimatedOverlay`.

## Preset guide

### `MenuLike`

Use for anchored menus that should get out of the way of title bar interaction.

Typical examples:

- `FileOverlay`
- `SettingsOverlay`
- `LanguageOverlay`
- `FullscreenAnimationOverlay`

Behavior shape:

- anchor click closes and passes through
- title bar interaction closes and passes through
- content interaction closes and consumes

### `ContextLike`

Use for strict context menus that should strongly own the current interaction.

Typical examples:

- generic context menus inside content areas

Behavior shape:

- anchor click closes and consumes
- outside click closes and consumes
- strong ownership of content interaction

### `CardContextLike`

Use for library card menus.

Typical examples:

- `CardContextMenuOverlay`

Behavior shape:

- left-click outside content still closes and consumes
- right-click on another content-interactive target closes and passes through
- title bar interaction closes and passes through

This preset exists because card context menus need to support:

- close old menu
- immediately open new menu on another card

### `CaptureLike`

Use for overlays that can enter a temporary capture mode.

Typical examples:

- player input binding capture

Behavior shape:

- outside click may cancel capture instead of closing
- `Esc` is reserved while capture is active
- after capture ends, overlay returns to normal close behavior

### `ToolPanelLike`

Use for lightweight tool panels that should allow anchor retoggle and title bar interaction, while still blocking content-side accidental actions.

Typical examples:

- player speed menu
- future player tool panel popovers

Behavior shape:

- anchor click closes and passes through
- title bar interaction closes and passes through
- content interaction closes and consumes
- content background closes and consumes

## Placement guidance

### Keep overlays tree-hosted when they are part of view interaction

If the floating UI should feel like part of the page or window interaction model, keep it on `AnimatedOverlay`.

### Allow out-of-host positioning only when needed

`AnimatedOverlay` defaults to `ConstrainToHostBounds = true`.

Set `ConstrainToHostBounds = false` only when the surface must escape a small host layout, such as:

- player control bar speed menu above the control bar

Do not disable host bounds by default.

## Close behavior guidance

When choosing or designing a preset, think in this order:

1. What should happen on anchor retoggle?
2. What should happen on title bar interaction?
3. What should happen on content interaction?
4. What should happen on content background?
5. Should child overlays keep ancestors alive when they intercept close?
6. Should parent close cascade to descendants?

If those answers are not well represented by an existing preset, add a new preset instead of hand-tuning one overlay in XAML.

## Preferred practice

- Prefer `InteractionPreset` over per-instance raw close/pointer property tuning.
- Prefer adding a new preset/profile when multiple surfaces want the same semantics.
- Keep feature code responsible for business meaning, not low-level pointer arbitration.
- Keep `AnimatedPopup` narrow and avoid growing new interaction rules there.

## Current mapping

- title bar menus: `MenuLike`
- library card menu: `CardContextLike`
- player input capture: `CaptureLike`
- player speed panel: `ToolPanelLike`
- seek bar thumbnail preview: `AnimatedPopup`
