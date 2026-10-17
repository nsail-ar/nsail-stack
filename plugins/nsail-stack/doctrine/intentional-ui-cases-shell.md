# Intentional UI — cases: navigation, wizard, localization, branding, layout

Part of [intentional-ui-cases.md](intentional-ui-cases.md): the why behind the navigation menu,
wizard, localization, branding and layout/styling rules. **Nothing here is a rule** — the rules
are [intentional-ui.md](intentional-ui.md) and the [`ui/` shelf](ui/README.md).

---

# Navigation menu

## Why a menu leaf names a page type

A written `Path` was the last route string outside a `@page`: changing a page's template left the
menu pointing at a dead URL with nothing failing at compile time. The trade: a page missing from
the route table throws when the menu renders — loud and at startup instead of silent and later.

## Why the menu asks the page instead of declaring the permission twice

`NsNavMenu` reads the destination type's own authorize attributes, combines them through
`IAuthorizationPolicyProvider` and asks `IAuthorizationService` — the same three steps, in the
same order, `AuthorizeRouteView` takes to render the page itself — so the entry and the page
agree by construction. Rejected shortcut: reflecting `[Authorize<TMessage>]` and calling
`PreAuthorize` directly — a second gate, free to drift from the first.

**The entry declares one permission where the page provably cannot answer** (nsail#1800): two
doors onto one page, and a group, which the page gate never asks about at all. A portal customer
holds every read behind Trabajos, Contactología and Contabilidad about themselves, so those three
passed their pages' gates and the drawer was the counter's. `NavMenuItem.Permission` is ANDed with
the page's, and `PageGate.Permits` asks it through the same `IAuthorizationPolicyProvider` the page
goes through — the shortcut above stays rejected. Rejected alternatives: a staff marker message
minted to carry the distinction (permissions.md already limits that shape to a screen with no
message at all); stacking a second `[Authorize<T>]` on the pages, which closes the ROUTE a portal
session legitimately reads; and `Visible = false` rows, which are data and cannot answer per
session.

## Settings groups

Domain first, system last. The main nav never folds automatically: a nav group is the domain's
map (a Compras rendering flat was what showed it).

**What a section is BUILT from goes to Configuración, and the kit is what moves it.** Agendas,
Feriados and Prestaciones are set up once and read by every turno after, so they belong with
Reservas; the move lives in `SchedulingSettingsContributor` and `Scheduling/Menu.cs`, not in
either product's arrangement, so every app composing the kit inherits it — Optical reads that
group as Contactología and Therapy as Agenda, a string each and not a second tree. The main nav
works the same way: Optical mounts `AddSchedulingMenu()` and its drawer group is the kit's
`Scheduling` entry, and the three answers that make it Contactología (the glyph, the word,
Calendario nested under it) are the INSTALL's stored arrangement rather than the product's code,
so a shop takes any of them back without a release ([ui/navigation.md](ui/navigation.md)). The
settings tree's `NavMenu.SettingsScheduling` is still a string: that entry is the product's word
over the kit's, the drawer's is the shop's. No app-side arrangement for Settings exists, and the
fold still decides whether a section earns its level: three loose Medical leaves became
**Medicina**, and Plantillas beside Mensajes sin Ubicar became a group because two leaves are what
a group waits for — **Mensajería**, holding Teléfonos as well.

**A settings group is a bucket, and a peer kit joins it by naming it.** The phone defaults are
Directory's screen and the group is Channels': `Group = "SettingsChannels"` is the whole join —
the string seam Integrations already carries between kits that reference each other in
nothing — so no project reference is added and both products read the tree at once. A joining
item declares the GROUP's weight, which also lands it after leaves contributed at zero, and it
declares no `GroupIcon`: the group's owner names the glyph, and an install without Channels folds
the group away and leaves Teléfonos its own.

## The group of one disarms — and keeps its name

The objection to a flat Compras was never the level, it was the name: `Merge`'s fold hands the
place to the child, so Compras came back reading Órdenes de Compra. Flattened at the contributor
— the purchase-order entry moves to the first level *as Compras* — the name survives. **The
section's name is what may not be lost.** The fold stays off for the main nav for exactly that
reason, and on for settings, where the group is a folder rather than a place.

Corollary: no app relocates Accounting's `SupplierAccounts` under Products' `Purchasing` group to
make Compras read as a group of two; Cuenta corriente de Proveedores lives where the kit that owns
it puts it, once.

---

# The wizard

`NsNext`/`NsBack` were designed and rejected: not every step is a form, so a component that
submits one cannot be the sequence's vocabulary. The Punto de venta step is the idempotence
contract in one line — the first Next creates, Back reloads what exists, the next Next renames
the same row. Optical's `OnboardingGate` (reading `OnboardingSettings.Completed`) is the
`IRouteGate` reference.

## A claim is a fixed point, not a veto

Two gates with one screen each would bounce a visitor between their destinations forever.
Letting a gate that answers its own page outrank every redirect was rejected: the claimant
silenced every gate behind it, so a provisional visitor rendered the install's wizard because the
wizard claimed `/onboarding` first. `Weight` decides instead — the first gate asked is the one
that decides — and a claim only means the gates lighter than the claimant are content to leave
the visitor here.

---

# Localization

## Menu entries and titles are Title Case

A title with no translation falls back to English verbatim ("work order" rendered raw on a
Spanish screen), so a title is never as invisible as running text — hence titles follow the menu
entries' Title Case. It is applied per string in `strings.json`/`strings.es.json` across kits and
apps, not by a mechanism — `GetTitle()` still just resolves a key.

---

# Branding and theming

`NsSetup` persists the `Brand` and the `ThemeSettings` it painted into
`PersistentComponentState` (keys `NSail.Brand`/`NSail.Theme`, the imperative
`RegisterOnPersisting`/`TryTakeFromJson` pair) and restores them synchronously in
`OnInitialized`, before the first client render. No host registration is needed —
`AddRazorComponents` and `WebAssemblyHostBuilder.CreateDefault` both already provide
`PersistentComponentState`.

---

# Layout and styling rules

## Why `NsContainer` stacks by default

`NsContainer` once applied `d-flex` with no direction, and a bare `d-flex` is a row — measured,
`SignInPage`'s form card at 75px instead of 412 the moment any provider contributed a button
block. Every page had been protecting itself by wrapping its whole content in a single `NsStack`,
which kept the row invisible. The container stacks; a row is asked for. The axis flip cost no
caller its height because every direct child of an `NsContainer` carries its own growth — which
is why a class-list test (`NsContainerDirectionTests`) is adequate evidence here where a CSS case
needs a measurement.

## Why `NsSubmit`'s label defaults to pinned (`Breakpoint = Always`)

A drawer at the default `Md` is 480px, the `NsPanel` inside it ~448px after padding, and the `sm`
container query asks for 600px — a submit taking `NsButton`'s `Sm` default is not *sometimes*
collapsed in an aside, it is **always** collapsed, and the save button is the one control that
must never be a guess. The general lesson: a `Breakpoint` is only meaningful if some surface the control lives in
actually reaches it — check the width the *container* can have, not the window.

## Why collapsing is two classes, and why C# can't just swap the component

MudBlazor spaces a start icon `-4px` left and `8px` right — left alone it pushes the icon off
centre. So the label hides (`d-c-*` on the label span) **and** the control gets
`ns-collapse-{breakpoint}` so the stylesheet can zero the icon's margin; both classes come from
the same `Breakpoint`, computed together in `NsResponsive`, so they cannot drift apart. Swapping
to a real `MudIconButton` when narrow is not available: choosing the component happens in C#,
which sees only the viewport, never the container width this mechanism exists to respect.

## The bubble a collapsed control whispers

Its text is `attr(aria-label)` — the same string the hidden span carries, so the two cannot
drift — gated by the *same* container query that hid the label, which makes the double cartel
unrepresentable rather than merely avoided. A vendor tooltip cannot do this: it portals out of
the container and cannot be gated on the container's width. A Turno dialog at ~960px rendering
its verbs as bare glyphs forever is why every portaled surface declares its own `ns-container`:
a container query with no `container-type` ancestor matches nothing, and the mobile-first
`d-c-none` half wins at every width.

**The switch is a custom property, and that is specificity, measured.** The bubble's own rule
carries a guard the gates cannot — `[aria-label]:not([aria-label=""])`, so a control nobody named
draws no empty padded box — and a guard is weight: it makes the definition one class-unit heavier
than every `:hover` gate, so `display: block` on a gate loses to the definition's own
`display: none` and the bubble paints nowhere, at any width. The gates therefore name no
`display`: they set `--ns-whisper: block` on the CONTROL and the pseudo-element inherits it. Two
rules that never name the same property cannot race, so the next guard added to the definition
cannot silence them again. **A pseudo-element's computed style is browser-only** —
`getComputedStyle(el, '::after')`, with the pointer on the control: a render test sees the class
hooks and the `aria-label` and nothing about which rule won, which is how a bubble nobody had
ever seen kept a green suite.

**It hangs DOWN, except where there is no room.** The bubble is painted outside the control, so
the nearest scrolling ancestor — `NsPanel`'s content box — clips whichever side has no room: a
card header's square sits at the TOP of that box and loses a bubble drawn above it, a control at
a page's own bottom edge loses one drawn below *and* grows the page a scrollbar for as long as it
shows (nsail#2197) — CSS alone cannot ask whether there is room, so it never asked. `ns.js`'s
`placeWhisper` does, on the same hover and focus `--ns-whisper`'s own gates already answer: it
measures the control against `window.innerHeight` (the bubble's own height is fixed — it never
wraps — so the margin it checks for is read off the stylesheet, not guessed) and marks the loser
with `data-ns-whisper-above`, an attribute ns-mud.css's own direction rule reads at the same
selector plus one. Down is still the general case and the side `NsTooltip` is placed on anyway;
a page's bottom edge is simply the one place a control answers "no room" every time it is asked.

## The chord beside a button's word is its tooltip, never a drawn badge

A drawn badge tried the opposite construction once and the house ruled it out (nsail#2272,
replacing the dev notes nsail#2207 shipped it under): **a decoration drawn on the control
itself is in that control's accessible NAME**, which is not the reading `textContent` is — the
name computation folds `::before`/`::after` in where `textContent` leaves them out. So the
chord badge, built as a node of its own beside the word, named a hero worded "Guardar" "Guardar
Ctrl+Enter" — and every role query asking for that word exactly stopped finding a button that
was on screen (nsail#2259: ten browser walks, each timing out on a control the evidence
screenshot shows).

The chord therefore rides THE WHISPER instead (`ns-chord`, above) — and not off `aria-label`
itself: that attribute IS the accessible name, and the ruling's own words are "the accessible
name of a button stays its word", so folding the key into it would be nsail#2259 again, one
layer up, badge or no badge. `ns-chord`'s `::after` reads a SECOND attribute, `data-ns-chord`,
`Word (Keys)` — the control's one pseudo-element stays the only bubble there is, `aria-label`
keeps carrying the bare word, and whichever caller sets one sets the other, since a control
with no visible text falls back to content for its name the instant `aria-label` is missing.
This is not the double cartel the whisper's own gate exists to forbid: that rule is one string
shown twice, and the key is a second fact the visible word never carried, which is why
`ns-chord` whispers even where the word stays on screen — `NsSubmit`'s hero, the worded
`NsClose`'s Cancelar, a chromed `NsButton` claiming a chord of its own. Their word is a string
via `NsSubmit.Label`/`NsClose.Label` (`ChildContent` cannot give one back) or `NsButton.Label`
directly; the claim in `aria-keyshortcuts` is unchanged, so a reader announcing the chord once
is still exactly once. `NsLink` and `NsPageLink` carry the same `Chord` for the door a screen
offers (F2 on a record's Editar), and the worded ANCHOR takes an `aria-label` only there: an
anchor's name folds generated content exactly as a button's does, so a bubble it draws is a
name to outrank — one that draws none has nothing to outrank and keeps carrying no second copy
of its own text.

**A search box is the one control that draws its key.** It has no word to hover, so there is no
bubble to carry one: `NsSearchField` paints the first key it claims inside the box at the
trailing edge (`.ns-search-key`, `aria-hidden`, and only while the box is empty), which is also
the convention every application that spends a chord on a search box follows. Never in the
PLACEHOLDER — a placeholder is an input's name FALLBACK, so a key written there lands in the
accessible name, nsail#2259 one layer up, and it moves a string every browser walk reaches the
box by.

## Ctrl+K is the drawer's own search, and "?" the door beside it

Both chords are the FRAME's and not a page's (nsail#2207): a screen that draws no header of its
own still answers them, and a phone, where the drawer is closed, answers neither — the box and
the door are both inside it, and a chord no live control declares does nothing.

- **Ctrl+K lands in the menu's search** (`NsSearchField.Map`, planted by `NsNavMenu`), and there
  is no second palette: the menu is the one list of where a session may go, permission gate
  included, so a palette built from the route table would be a second source to keep in step
  with it. Enter in that box opens the first entry still shown, which is literally that entry's
  own link (`.ns-nav-entry`, `ns.js`). What the box reaches is therefore what the menu lists — a
  screen reached FROM another one is reached by going there first.
- **"?" opens a sheet of the claims on screen** (`NsKeySheet`, hosted through
  `DialogManager.Open`, so Escape and the X close it through the guard everything else goes
  through). Its lines are read off the DOM at the instant the door is pressed (`nsapp.chords`) —
  each line the declaring control's own word, already in the install's language — and the three
  groups are derived from WHERE that control stands: inside `.ns-cells` it is the grid's, inside
  a `form` the form's, otherwise the frame's. So no control declares its group, none can declare
  the wrong one, and there is still no table of shortcuts for anybody to keep in step with a
  screen.

## F2 and F3 are the only function keys taken

They are the desktop convention the same hands come from (Excel, Explorer), and they do what no
character chord can: answer from inside a box being typed in. WCAG 2.1.4 is about a chord
somebody could be in the middle of TYPING — which is "/" and "?" and nothing else — so the gate
in `ns.js` keys on whether the chord IS a character (`character` in `CHORDS`) rather than on
whether it carries a modifier. F1, F5, F6, F11 and F12 stay the browser's.

F3 joins "/" on a page's search and is the key that box DRAWS, "/" being the silent alias that
answers only while nobody is typing. F2 is claimed by the Stack's own row pencil — one
declaration covering every grid in the house — and the row that answers is the row the keystroke
was made in (`chordTarget` asks the grid row before the form around it): every row publishes the
same key, so anything else would press the last row's.

## The grid's arrows move the cursor; Left and Right are the caret's

Beside the Enter policy and for its reason — which row the cursor is on is a property of the
keystroke, read off the DOM when it is pressed. Up and Down make the same move Enter makes
without its commit: going to look at the row above is not confirming the one below it. Left and
Right are not in the policy at all, because inside a cell being typed in they belong to the
caret (the ARIA grid pattern hands the arrows to the control in edit mode) and Tab is what
crosses cells. Delete presses the row's own Delete (`.ns-row-delete`), with whatever confirmation
that button already asks for, and only while the cursor is NOT in a box — there the key is the
character the hand meant.

## Container queries: the measured facts

- `container-type: inline-size` stops the element sizing itself from its content — safe on
  `NsPanel`/`NsCard` (flex columns taking width from the parent), exactly what would break a
  `fit-content` element. Pick the node deliberately.
- The stacked mobile layout outranking hidden columns is specificity, measured (WorkOrdersPage's
  saldos): `.mud-{bp}-table .mud-table-cell { display: flex }` (0-2-0) beats `.d-c-none`
  (0-1-0), so a hidden column reappeared as a card line. Fixed at the specificity the card needs,
  not by fighting the vendor's selector: `.ns-table.mud-sm-table .mud-table-cell.d-c-none`
  (0-4-0), off by default once stacked, and a matching `.d-c-{bp}-table-cell` selector at the
  same weight nested in the same `@container` thresholds — written second, so it wins exactly when
  its own condition matches.
- `MudTable.Breakpoint` is pure vendor CSS (`mud-sm-table` + a 960px media query), so *when the
  table stacks* stays viewport-bound — porting those rules to `@container` means owning a copy of
  vendor CSS. That copy is taken only for the case that has no other answer: a table with a row
  OPEN, whose inputs never hide ([cases: hosts](intentional-ui-cases-hosts.md), *The open row
  stacks where its container cannot seat it*). A table nobody is editing stacks by viewport alone.
- No fallback needed: container queries ship in every browser since 2022–23; a browser without
  them ignores the rule and leaves labels visible.
