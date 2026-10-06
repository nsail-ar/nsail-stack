---
name: new-icon
description: Add an icon to an NSail icon catalog (NsIcons or an app catalog like OpticalIcons) from a Material Symbols path. Use when a screen, menu entry, button or field needs a glyph no catalog carries yet, instead of reaching for Icons.Material.*, or when a pasted icon renders as nothing or a dot. Covers the 960-grid viewBox transform, catalog choice and icon sizing.
---

# Adding an icon

Icons are named through catalogs, never `Icons.Material.*` in kits or apps
(intentional-ui.md). Two catalogs exist and both grow on demand:

- **`NsIcons`** (`NSail.Components.Mud`) — the generic UI vocabulary: `Search`, `Add`,
  `Directory`, `People`, … An icon belongs here when it means a UI gesture, not a domain
  concept.
- **`{App}Icons`** (the app's Shared project) — the domain vocabulary:
  `OpticalIcons.Prescription`. An icon belongs here when only that product speaks the
  concept.

The catalogs are plural because `NsIcon` is the component that renders one (naming.md).
Most icons reach the screen through a control's `Icon` parameter; `<NsIcon />` is for the
rest.

## The value is SVG markup, not a vendor constant

Each entry is the **SVG path markup itself**: a Material Symbols path at the **20px
optical size**, pasted in.

**Every entry carries its own viewBox mapping**:

```
<g transform="translate(0,24) scale(0.025)">
```

Symbols ships a `0 -960 960 960` box, while every control that renders an icon — a
button's `StartIcon`, a nav link, a field adornment — emits its own
`<svg viewBox="0 0 24 24">` and never asks. Nothing in the chain can set the viewBox
once, so the glyph maps itself: scale by 24/960 (= 0.025), translate back into the
visible quadrant. Paste new icons in the same shape.

**Gotcha**: a path copied from the **24px grid** (instead of the 960 one) run through
that transform scales down to an invisible dot. If the icon renders as nothing, this is
why.

## Sizing is set once, not per icon

`.mud-icon-size-{small|medium|large}` in `ns-mud.css` (16/20/24px) defines the size
steps — never per control, because each control renders its own icon and none of them
consults `NsIcon`. Material's own 20/24/36 are drawn for touch and read as illustrations
beside 14px text; do not reintroduce them.

## The exception

`NsIcons.Progress` is hand-drawn rather than a Symbols glyph: it stays in the `0 0 24 24`
space the host provides (no transform) and its markup animates itself (`.ns-spin`), so a
busy state fits any slot that takes an icon. It is the model for any future self-animating
entry, not for ordinary glyphs.
