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

## The bubble a control whispers

**One element, in the browser's top layer, placed from `ns.js`.** The whisper is the house's only
tooltip: a single `div.ns-whisper` created once, shown as a `popover="manual"`, positioned from
the trigger's own `getBoundingClientRect()`. It is not generated content on the control and it is
not a vendor tooltip, because the three rules it has to keep are each a fact only the document can
state and none of them survives being drawn inside the control it names:

- **Never clipped, never under what is beside it.** Content drawn outside a control's box is
  clipped by the nearest scrolling ancestor (`NsPanel`'s content box) and painted in the control's
  own stacking context, which is under the panel next to it. A card header's square lost a bubble
  drawn above it; the counter's `Facturar (Ctrl+Enter)` was cut off at the bottom edge of its
  panel (nsail#2540). The top layer is above every dialog, drawer and vendor popover with no
  `z-index` to outrank theirs.
- **Never moves a scroll.** An absolutely positioned child extends its scroll box's scrollable
  area for as long as it shows — which is the page gaining a scrollbar on hover (nsail#2197). A
  top-layer element is in no scroll box at all.
- **Only after a steady dwell.** `WHISPER_DELAY` in `ns.js` is 2.5 s and is the whole of the
  bubble's timing, for every control in the house: a name that opens on the way past is noise.
  It closes on leave, press, key, scroll and resize, and the keyboard takes the same dwell —
  `:focus-visible` is what tells a keyboard's focus from the one a press leaves behind, so a
  button still focused after its own click does not re-open its name over what the click opened.

**The text is declared, the opening is derived.** `data-ns-whisper` carries the bubble's whole
text — the name, and the key where there is one (`NsButton`, `NsLink`, `NsSubmit`, `NsClose`,
`NsChip`, `NsTooltip`); the two faces the VENDOR draws and nothing of ours can put an attribute on
— a menu's icon face, a lookup's end adornment — are read off `aria-label` by their own two
selectors. Whether the bubble opens is then one question asked of the RENDERED control: `ns.js`
compares the text against the control's `innerText`, case-folded, and stays shut where the word is
already on screen. **That is what makes the double cartel unrepresentable**, and it costs no gate:
a label a container query hid, one the footer's emphasis ladder hid and one that was never written
are all the same empty `innerText`, so no breakpoint is restated in CSS and nothing is kept in step
by hand between the two languages. A control the caller pinned with `Breakpoint="Always"` declares
the same text as one that collapses — the markup says what there WOULD be to say, and nothing
about when.

**A refused control whispers too**, and the stylesheet owes it one line: the vendor drops hit
testing on a disabled button and on a disabled chip (`pointer-events: none`), so the pointer never
meets the control at all and the trigger lookup walks up past it — which leaves the one face with
nothing BUT the bubble to say what it is (a row's grey pencil, the X under a frozen form) silent.
`[data-ns-whisper]:disabled` takes that back. It is safe because the refusal is the ATTRIBUTE's,
not the CSS's: a disabled control receives hover and never a press, a focus or a click. The one
response that would have outlived both is the vendor's ripple, a document-level `pointerdown` over
whatever carries its class, so **a refused face declares no ripple**.

What is left to `ns-mud.css` is the look, once (`.ns-whisper`), read from the vendor tooltip's own
variables so the bubble is the tooltip the house always had to the eye. A render test sees the
declaration and no more: whether the browser paints it where it should is a browser's question,
and it is the product's own browser suite that answers it.

## The chord beside a button's word is its tooltip, never a drawn badge

A drawn badge tried the opposite construction once and the house ruled it out (nsail#2272,
replacing the dev notes nsail#2207 shipped it under): **a decoration drawn on the control
itself is in that control's accessible NAME**, which is not the reading `textContent` is — the
name computation folds `::before`/`::after` in where `textContent` leaves them out. So the
chord badge, built as a node of its own beside the word, named a hero worded "Guardar" "Guardar
Ctrl+Enter" — and every role query asking for that word exactly stopped finding a button that
was on screen (nsail#2259: ten browser walks, each timing out on a control the evidence
screenshot shows).

The chord therefore rides THE WHISPER instead (above) — and not off `aria-label` itself: that
attribute IS the accessible name, and the ruling's own words are "the accessible name of a button
stays its word", so folding the key into it would be nsail#2259 again, one layer up, badge or no
badge. `data-ns-whisper` carries `Word (Keys)` while `aria-label` keeps carrying the bare word,
and the bubble is drawn nowhere near the control, so there is nothing on the element for a name
computation to fold in at all. This is not the double cartel either: that rule is one string shown
twice, and the key is a second fact the visible word never carried — which is why a chorded
control whispers where the word stays on screen (`NsSubmit`'s hero, the worded `NsClose`'s
Cancelar, a chromed `NsButton` claiming a chord of its own) and a chordless one with its word on
screen says nothing. Their word is a string via `NsSubmit.Label`/`NsClose.Label` (`ChildContent`
cannot give one back) or `NsButton.Label` directly; the claim in `aria-keyshortcuts` is unchanged,
so a reader announcing the chord once is still exactly once. `NsLink` and `NsPageLink` carry the
same `Chord` for the door a screen offers (F2 on a record's Editar), and the worded ANCHOR carries
no `aria-label` at all: its word is its whole name, with no generated content of its own to
outrank.

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
  own link (`.ns-nav-entry`, `ns.js`) — or, where nothing stands at all, the one row the app
  offers in its place (`NsNavMenu.NoMatch`, `.ns-nav-nomatch`; ui/navigation.md). What the box
  reaches is therefore what the menu lists — a screen reached FROM another one is reached by going
  there first — plus what a module answered the term with (`INavMenuSearch`, under the matches
  and marked as entries: the records a session is looking FOR, which the map was never the list
  of) — plus, for a question neither lists an answer to, whatever the app put at the end of it.
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
keystroke, read off the DOM when it is pressed. Up and Down make the move Enter makes, either way
and stopping at the ends: a grid is not a ring, so only Enter writes the line that is not there
yet. **Leaving an open row is closing it, and the row's own Confirmar is what may** — a host that
opens one row at a time greys every other pencil while one is open, so a move that skipped the
commit would have nothing to move onto, and closing a row by any second mechanism is exactly
what this does not do. Left and Right are not in the policy at all, because inside a cell being
typed in they belong to the caret (the ARIA grid pattern hands the arrows to the control in edit
mode) and Tab is what crosses cells. Delete presses the row's own Delete (`.ns-row-delete`), with
whatever confirmation that button already asks for, and only while the cursor is NOT in a box —
there the key is the character the hand meant.

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
