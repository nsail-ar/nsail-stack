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

**It hangs DOWN, and only a footer's rises.** The bubble is painted outside the control, so the
nearest scrolling ancestor — `NsPanel`'s content box — clips whichever side has no room: a card
header's square sits at the TOP of that box and loses a bubble drawn above it (half a word, on
the dashboard), a footer act sits on the surface's bottom edge and loses one drawn below. Down is
the general case and the side `NsTooltip` is placed on anyway; `.ns-panel-footer` is the one
exception, because it *is* the bottom edge.

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
