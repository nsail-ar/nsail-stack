# Branding and theming

What a brand is, who supplies it, and why the client never resolves it twice.

The rule a page lives by — **change the `Brand`, never the vendor theme** — stays in
[intentional-ui.md](../intentional-ui.md). The record is in
[intentional-ui-cases-shell.md](../intentional-ui-cases-shell.md) under *Branding and theming*.

---

## The model

Branding is a vendor-agnostic, **serializable** object in `NSail.Components` (`Branding/`):
`Brand` — `Name`, `DefaultDark`, two `BrandTheme`s (`Light`/`Dark`); `BrandTheme` — per-scheme
`Logo`, the accent and its states, the surface ladder, text, `Lines` and status colors, **hex
strings only**. `IBrandProvider` (async) supplies the active brand, the async contract existing
so a provider can resolve branding per organization. **Components never read Mud palette types
from app code.** Default look: dark, a warm near-black surface with orange `#F68E1E`.

**A brand is picked in exactly two colours — `Chrome` and `Accent`.** Everything else is either
a scheme constant or derived.

- **`Primary` is the stored name of the `Accent`.** Above the Data entity — `BrandTheme`, the Sdk
  model, `EditModel`, the editor — the colour is `Accent`; `BrandingHandler` maps at the edge, so
  no column moved for the name. `Secondary` and `Tertiary` are not in `BrandTheme`, the Sdk or
  the editor: a brand is two colours. In Data they are abandoned columns, carried over untouched
  on save so an old row still satisfies the `NOT NULL` it was written under.
- **Chrome does not derive from the accent** — mixing a yellow accent into a dark surface reads
  brown. Nothing derives from the accent but the accent's own states.

**The palette is one fact in one place, and that place is `BrandTheme`.** The surface ladder, the
ink — `TextDisabled`, the refused rung, with the two above it — `Lines`, `RowHover`, the
four status colours and the ink each of them is read in when it is FILLED are **scheme
constants**: they are not stored, they do not travel, and no editor offers one. The Sdk model carries the accent, its three states, `TextOnAccent`, `Chrome`
and the chrome's two inks; the Data entity carries `Primary`, `Chrome` and the two abandoned
columns. `SessionBrandProvider` starts from `BrandTheme.DefaultDark()`/`DefaultLight()` and lays
the row's picks over it — so **an install that saved a brand under an older palette shows the
current one on its next paint, with no UPDATE to run**, and there is no second copy to drift.
`BrandPaletteTests` (Architecture) keeps it one copy.

**A severity at full strength is a band, so its text is read against the fill and never against
the card** — `TextOnError`/`TextOnWarning`/`TextOnSuccess`/`TextOnInfo`, one per severity per
scheme, handed to the vendor's four `*ContrastText` slots. Left unset the vendor draws every
filled face in its own white, which on the dark scheme's `Info` is 2.21:1 — a block of colour
with a ghost of text on it. Each is near-black or white, whichever clears AA on that fill: **no
one ink serves all eight faces** (light `Success` and light `Error` read on white and every dark
fill reads on black), and no luminance RANKING picks them either — `#ef5350` ranks dark, which
would put white on it at 3.49:1. They are constants and not a `BrandTone` derivation because the
fills are constants too: there is no free pick here to derive against, and a palette value that
moves past the floor has to turn a test red rather than quietly drag its ink along.

**The same four constants read as a WORD are a criterion they do not meet, and the answer is a
step beside them rather than a move of them**: set as text on a card, light `Warning` read
3.32:1 and light `Info` 3.70:1, so the status channel leans each one into the scheme's ink
instead — `--ns-status-*`, `BrandTone.ToneLean`, the rule in [styling.md](styling.md). Re-tuning
the constants would repaint every filled face above to fix a word.

**In dark, elevation is light and not shadow**, so the ladder is spent on tone: `Background`
`#161513` sits a real step under `Surface` `#232120`, and a card reads as lifted off the canvas
without a border. In light the ladder inverts — white has nowhere left to rise — so the canvas
sinks below the surfaces and border-plus-shadow does the separating.

**The body floor is held on every rung of that ladder, the canvas included.** `Background` is a
ground text is set on and not only what the cards stand on: `NsPanel` paints nothing of its own
([styling.md](styling.md)), so every word inside a chromeless panel — a screen's muted figure, an
explanatory paragraph, a totals label — is read on the canvas and on nothing else. Both inks
above the refused rung clear 4.5:1 on the canvas, the card and the raised sheet, in both schemes,
and `BrandThemeContrastTests` sweeps the three; measuring the surfaces alone is how a muted rank
under the floor once reached three screens at once. The refused rung is the exception and its
comment in that file says why — it owes a CEILING, which the canvas's extra step would break.

---

## `BrandTone`: what is derived

What is derived is derived by `BrandTone` (`NSail.Tones`, in `NSail.Types`), *the* helper — one
implementation with three entries, never two parallel ones:

- `BrandTone.Accents(accent)` answers the states: hover, pressed, focus and `TextOnAccent` —
  dark text over a bright accent, light over a deep one, which is what keeps a yellow brand
  readable. **Disabled is not among them**: a refusal is a state of a CONTROL, not of the
  accent — every button, glyph and menu row wears it and only a handful are ever the brand's
  colour — so it is a scheme constant, `BrandTheme.TextDisabled`, on the ink ladder's third rung.
- `BrandTone.Ink(chrome)` answers the two inks the band is read in, from the chrome's own
  luminance: dark ink on a light chrome in EITHER scheme, so the content's white text never
  crosses onto a pale bar.
- `BrandTone.AccentInk(accent, ink, grounds)` answers the accent worn as a WORD — the soft rung's
  label, a link, the active tab, the chapter a reader is on — on every ground that word is drawn
  on (below).

**It lives in the Stack because a paint has to reach it too**: a helper only the API edge can
call leaves every later derivation a stored column or a CSS blend, and a CSS blend cannot see how
bright a colour is. `NSail.Tones` and not `NSail.Colors` — that name shadows QuestPDF's `Colors`
inside every `NSail.*` namespace.

**Derived at save, never at paint**, for what IS stored: the accent's *states* are derived at the
API edge rather than in a column, because they are a pure function of the stored accent and four
more columns could only drift away from it. **`AccentSoft` — `NsAs.Important`'s fill — is derived
later still**, in CSS (`--ns-accent-soft`, `color-mix` over `--mud-palette-primary`): a tenth of
whatever accent the row picked, so it can never freeze one tenant's colour into another's
buttons, and mixed against `transparent` so one value reads on a card, a raised sheet and the
canvas alike. **The status channel's toned readings are derived there too** (`--ns-status-*`,
`color-mix` over a severity and `--mud-palette-text-primary`): the ink the lean goes toward flips
with the scheme, so one declaration serves both and neither needs a constant of its own.

---

## The accent as ink

**The accent is a GROUND — a fill, a glyph, a slider — and never a word.** Painted as text it is
the brand at full strength on a surface the brand had no say in: the active tab's label read
2.33:1 and the Manual's open chapter 1.87:1, which put the one word on each screen that says
where the reader is at the bottom of the legibility ladder. So **every place the accent has to be
a word reads one token, `--ns-accent-ink`** — the soft rung's label, a primary link, the active
tab, the wizard's current step, the guide's open chapter — and a rule that sets `color` from
`--mud-palette-primary` is a defect (`AccentInkTests` sweeps the stylesheet for one; the single
exception is `.text-primary`, which tints an `NsImage`, and an image is a mark).

**That ink is derived in code and not in CSS, which is the one thing it does not share with the
status channel's lean above: a severity is a CONSTANT, so a fixed fraction of it can be mixed in a
stylesheet, and an accent is a free pick. It is NOT `AccentHover` either.** Over the light scheme the house
accent's wash is `#faecdc` and its hover `#f79c39`, which reads 1.85:1 on it. A CSS blend (55%
accent, 45% the scheme's ink) fails the other way, because it never asks how bright the accent is:
4.53:1 for the house orange, 3.3:1 for a pastel pick, 2.97:1 for black over the dark scheme. **A
tenant's accent is a free pick, and a free colour is never an illegible one**, so the derivation
branches on the pick's own luminance, which `color-mix` cannot do. `BrandTone.AccentInk` leans the
scheme's own ink toward the accent as far as 4.5:1 holds **on every ground it is given** and no
further — less for a pastel, none at all for a colour that does not parse.

**Every ground at once, because which one is worst is itself a function of the pick**: a wash of a
bright accent darkens a light surface and lightens a dark one. `BrandTheme.AccentInk` hands over
the three rungs of the ladder and the accent's soft wash over each of them — `--ns-accent-soft`
mixes against `transparent`, so the label stands on the wash over whichever rung the button sits
on. The vendor's 6% hover, which is the ground under the active tab and the open chapter, needs no
entry of its own: it lies between a bare rung and the tenth below it, and the ink is never between
the two. **A row under the POINTER is not one of them**, unlike the status channel's grounds above:
nothing in the house sets a word in this ink inside a grid row — a `RowEditor` cell's field asks
for no visible label at all ([hosts.md](hosts.md)) — and widening to it would cost the default
brand two more steps of its own hue on every tab in the app to answer a screen that does not
exist.

**On the chrome it is a second derivation, `BrandTheme.AccentOnChrome`.** The band is picked
freely while the content's surfaces are fixed, so a card's answer would land on a chrome of any
luminance; the drawer leans from `TextOnChrome` and reads against the chrome and the wash it marks
a row with. `ns-mud.css` names it `--ns-rail-accent`, with the band's own ink as the fallback, and
the entry a reader is on — nav and guide rail alike, one gutter, one pair — wears it.

`NsTheme` emits both for the scheme that is lit, and `ns-mud.css` only reads them. The floors are
swept, not sampled: `BrandThemeContrastTests` walks the accent cube in both schemes. On the
chrome, a floor cannot be promised over two free picks at once — a mid-luminance chrome whose own
`Ink` does not clear it has nothing legible to lean from — so what is promised there is that
marking the row never reads worse than the band's plain ink.

---

## Chrome

**Every ground is chrome, and chrome turns with the reader's scale.** The nav rail paints from
the drawer palette slot (`--mud-palette-drawer-background`, `ns-mud.css`), which is `Chrome` —
light in the light scale, dark in the dark one. So nothing renders on a ground that stands
outside the theme, and `BrandingBar` picks its logo variant from the scale alone: a host has no
ground to declare and no override to pass.

**`NsAppBar` takes no `Color`.** `MudAppBar`'s `Color.Primary` paints the header
`mud-theme-primary` — the raw brand primary at full strength, its text from `primary-text` —
which is the whole appbar palette painted over. `NsAppBar` leaves the default so the bar wears
`AppbarBackground`/`AppbarText`.

---

## Resolving and painting

**`GetBrand` returns `Brand?`, and `null` means "not known yet"** — a provider must never answer
a default `Brand` instead: the caller cannot tell that apart from a real answer, paints it, and
the real brand arriving after is the flash.

**The prerender hands the brand over; the client does not resolve it a second time.** **The
invariant is *the client's first render equals the server's last one*.** `NsTheme.Neutral` —
colourless grey — is the last resort only, for a client no prerender handed anything to (cases).

**`NsTheme` is the only place a `Brand` becomes the palette variables** — the vendor's
`--mud-palette-*` and the one `--ns-*` a stylesheet cannot compute — because the
vendor stylesheet ships no `:root` defaults and anything drawn before it mounts paints from
unset ones. `NsSetup` mounts it above the app; a document rendered with no app around it
(`NsProblemDocument`) mounts it alone.

**Every saver announces in place; nothing reloads to repaint.** `BrandChanged` is the event —
published after a successful `CreateBranding`/`UpdateBranding` — and `NsSetup` (theme),
`BrandingBar` (logo) and `BrandingHead` (browser tab) all subscribe to it. Both `BrandingPage`
(Settings) and the onboarding `BrandingStep` publish it on save; a saver that skips the publish
leaves those three reading the old brand until something else forces a reload.
