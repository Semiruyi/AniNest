# UI Style System

## Goal

AniNest now has a clearer WPF style system for tokens, shared patterns, and feature-specific UI styles.

This document defines where new UI resources should live, how they should depend on each other, and which mistakes to avoid when refactoring XAML.

The goal is simple:

- keep pages focused on structure and bindings
- keep visual rules in resource dictionaries
- keep resource dependencies predictable
- avoid regressions caused by XAML load order or frozen shared objects

## Layering

The current UI resource system is organized in layers.

### 1. Design tokens

Files:

- `src/AniNest/Resources/Colors.xaml`
- `src/AniNest/Resources/Typography.xaml`
- `src/AniNest/Resources/Sizes.xaml`
- `src/AniNest/Resources/Spacings.xaml`

Use these for:

- colors
- brushes
- font sizes
- corner radii
- spacing
- dimensions

Rules:

- prefer semantic token names over raw values
- add a token when the same visual value appears in more than one feature
- do not place feature-specific control templates here

### 2. Shared primitives and interaction foundations

Files:

- `src/AniNest/View/Styles/IconGeometries.xaml`
- `src/AniNest/View/Styles/SharedStyles.xaml`
- `src/AniNest/Presentation/Theming/Buttons.xaml`

Use these for:

- generic text/button/surface patterns
- shared control templates
- cross-feature visual primitives

Rules:

- these files should not depend on feature-specific resources
- if a style could be used by Library, Player, and MainWindow, it probably belongs here

### 3. Feature or domain style dictionaries

Current files:

- `src/AniNest/View/Styles/LibraryCardStyles.xaml`
- `src/AniNest/View/Styles/LibraryButtonStyles.xaml`
- `src/AniNest/View/Styles/PlayerStyles.xaml`
- `src/AniNest/View/Styles/PlayerButtonStyles.xaml`
- `src/AniNest/View/Styles/TitleBarButtonStyles.xaml`
- `src/AniNest/View/Styles/TitleBarStyles.xaml`
- `src/AniNest/View/Styles/OverlayMenuStyles.xaml`
- `src/AniNest/View/Styles/MainWindowOverlayStyles.xaml`

Use these for:

- page-family-specific styles
- feature-specific button styles
- overlay composition patterns
- title bar and window-specific visuals

Rules:

- keep write ownership clear
- split by UI domain once a dictionary starts mixing unrelated responsibilities
- avoid moving a style here unless it really belongs to that feature family

## Current load order

`App.xaml` currently merges dictionaries in this order:

1. `Colors.xaml`
2. `Typography.xaml`
3. `Sizes.xaml`
4. `Spacings.xaml`
5. `IconGeometries.xaml`
6. `SharedStyles.xaml`
7. `LibraryCardStyles.xaml`
8. `PlayerStyles.xaml`
9. `Buttons.xaml`
10. `TitleBarButtonStyles.xaml`
11. `TitleBarStyles.xaml`
12. `LibraryButtonStyles.xaml`
13. `PlayerButtonStyles.xaml`
14. `OverlayMenuStyles.xaml`
15. `MainWindowOverlayStyles.xaml`

This order matters.

## Dependency rules

### Prefer one-way dependency flow

Good direction:

- tokens -> shared primitives -> feature styles -> page XAML

Avoid:

- shared primitives depending on feature styles
- early dictionaries depending on resources introduced later

### If a style is `BasedOn` another style, load order must support it

Examples:

- `PlayerSpeedOptionButton` must load after `PopupMenuButton`
- `PlayerInputBindingValueButton` must load after `PopupMenuButton`
- `MainWindowBaseOverlay` must load after the default `AnimatedOverlay` style exists

If a style depends on a later dictionary, move it to a later dictionary instead of trying to outsmart `StaticResource`.

## `StaticResource` rules

AniNest uses `StaticResource` heavily.

That keeps things fast and explicit, but it means:

- resource names are case-sensitive
- load order is strict
- forward references across dictionaries are fragile

Preferred practice:

- if a resource is widely shared, place it earlier
- if a style is feature-specific and depends on later shared pieces, move the style to the later phase that already has those dependencies available
- do not assume WPF will resolve a later dictionary for `StaticResource`

## Animation safety rules

This project uses shared animators and direct transform animation.

That means some object types must **not** be shared through reusable setter values when those objects will be animated.

### Do not share mutable animated objects in style setters

Be careful with:

- `ScaleTransform`
- `TranslateTransform`
- `RotateTransform`
- `TransformGroup`
- mutable `Brush` objects that will be directly animated
- mutable `Geometry` objects that will be directly animated

Why:

- WPF may freeze or seal shared instances
- later animation calls can fail with errors like:
  - `ScaleTransform ... is sealed or frozen`

Preferred practice:

- keep animated transforms local in control templates or element instances
- only move static transform metadata, such as `RenderTransformOrigin`, into shared styles
- if animation infrastructure touches `RenderTransform`, assume each animated element should own its own transform instance

### Defensive animation infrastructure

`AnimationHelper` now clones frozen scale transforms before animating exits.

That is a safety net, not permission to freely put animated transforms into shared setters.

Still prefer instance-owned transforms in XAML when the element is expected to animate.

## Overlay rules

### Use `AnimatedOverlay` for interactive floating surfaces

Examples:

- title bar menus
- settings menus
- player speed menu
- player input capture panel

### Use `AnimatedPopup` for presentation-only popup visuals

Examples:

- seek bar thumbnail preview

See also:

- `docs/overlays/guidelines.md`

### If you style `AnimatedOverlay`, inherit the default control style

When creating overlay-specific styles, use:

```xml
<Style TargetType="{x:Type overlays:AnimatedOverlay}"
       BasedOn="{StaticResource {x:Type overlays:AnimatedOverlay}}">
```

Why:

- the default control template provides `PART_PositionHost`
- the default control template provides `PART_Surface`
- dropping that base style can cause repeated open retries or broken overlay lifecycle

## Page cleanup rules

When refactoring a page:

1. move repeated text/surface/button rules into styles first
2. keep page-local structure and bindings in the page
3. leave behavior logic untouched unless a real bug is being fixed
4. test the actual UI path after any overlay or animation refactor

Good candidates for extraction:

- repeated text formatting
- repeated icon size rules
- repeated border/surface shells
- repeated popup menu item composition
- repeated overlay shell configuration

Bad candidates for over-eager extraction:

- unique one-off layout
- transform instances that are animated
- styles that would create awkward cross-dictionary dependencies
- templates or styles that depend on page-local event handlers

If a template or style needs a page code-behind handler such as `Click="..."`, keep it page-local unless that interaction is being redesigned.

## Current domain ownership

### Library

Use:

- `LibraryCardStyles.xaml`
- `LibraryButtonStyles.xaml`

### Player

Use:

- `PlayerStyles.xaml`
- `PlayerButtonStyles.xaml`

### Window/title bar/overlays

Use:

- `TitleBarButtonStyles.xaml`
- `TitleBarStyles.xaml`
- `OverlayMenuStyles.xaml`
- `MainWindowOverlayStyles.xaml`

## Refactor checklist

Before finishing a UI refactor:

1. build the app
2. verify that no new `StaticResource` dependency points to a later dictionary
3. verify that animated transforms are still instance-owned where needed
4. click through the affected overlay or popup path

Minimum command:

```powershell
dotnet build src/AniNest/AniNest.csproj
```

High-risk interaction paths to re-test after style changes:

- settings menu
- file menu
- player speed menu
- player fullscreen toggle
- player playlist panel
- player input capture overlay

## Practical rule of thumb

If a cleanup makes XAML shorter but makes resource dependencies harder to reason about, it is not a good cleanup.

Prefer boring structure over clever structure.
