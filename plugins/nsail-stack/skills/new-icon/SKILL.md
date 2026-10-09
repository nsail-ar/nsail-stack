---
name: new-icon
description: Add an icon to an NSail icon catalog (NsIcons, a kit's like ProductsIcons, or an app's like OpticalIcons) from a Material Symbols path. Use when a screen, menu entry, button or field needs a glyph no catalog carries yet, instead of reaching for Icons.Material.*, or when a pasted icon renders as nothing or a dot. Covers the 20px asset, the 960-grid viewBox transform, catalog choice, one drawing per entry and icon sizing.
---

# Adding an icon

Icons are named through catalogs, never `Icons.Material.*` in kits or apps
(intentional-ui.md). Three kinds of catalog exist and all of them grow on demand:

- **`NsIcons`** (`NSail.Components.Mud`) — the generic UI vocabulary: `Search`, `Add`,
  `Directory`, `People`, … An icon belongs here when it means a UI gesture, or a concept the
  whole house speaks rather than one domain: `Send` and `Receive` are here because anything
  leaves and anything arrives.
- **`{Kit}Icons`** (the kit's Shared project) — what a kit speaks and no single product owns.
  Two unrelated reasons for one: a domain concept every product that composes the kit says
  the same way (`ProductsIcons.LowStock`), or a **vendor's own mark** (`AppleIcons`,
  `GoogleIcons`, `MetaIcons`, `WhatsAppIcons`) — a brand's drawing is not a Material Symbol,
  so nothing below about the 20px asset or the 960 grid applies to it: it is pasted in the
  host's own `0 0 24 24` space with no transform.
- **`{App}Icons`** (the app's Shared project) — the domain vocabulary of one product:
  `OpticalIcons.Prescription`. An icon belongs here when only that product speaks the
  concept.

**One drawing, one entry**, across every catalog in the tree: an entry pasting a symbol
another entry already declares, or holding markup another entry already holds, fails a
product's Architecture sweep (`CatalogGlyphTests`). Two entries for one picture compare
unequal, which leaves every rule a board states about its glyphs as deep as the byte and no
deeper. Name the entry that exists — a kit's, if the app references the kit — and add a second
drawing only for a second meaning.

The catalogs are plural because `NsIcon` is the component that renders one (naming.md).
Most icons reach the screen through a control's `Icon` parameter; `<NsIcon />` is for the
rest.

## The value is SVG markup, not a vendor constant

Each entry is the **SVG path markup itself**: a Material Symbols path at the **20px
optical size**, pasted in — the asset at

```
https://fonts.gstatic.com/s/i/short-term/release/materialsymbolsoutlined/{symbol}/default/20px.svg
```

and the entry declares which symbol it is, in the line above itself (`Material Symbols
<c>move_to_inbox</c>`), because that line is the only place a reader or a rule can learn what
the markup draws.

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

**Gotcha**: the **24px optical size** is the 960 grid too, so it renders — heavier. Nothing
fails, and the glyph reads a stroke thicker than the card beside it. The optical size is not
visible in the markup; only the URL it was fetched from says which one it is.

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
