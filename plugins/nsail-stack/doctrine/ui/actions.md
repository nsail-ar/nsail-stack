# Actions, links and the contribution mechanism

The contracts of the action components and of the outlet an app enriches a kit's screen
through.

What an action *means* — the golden rule (navigates → `NsLink`, executes → an action
component), the emphasis ladder, where an act sits on the page — stays in
[intentional-ui.md](../intentional-ui.md). The record behind these rules is in
[intentional-ui-cases-controls.md](../intentional-ui-cases-controls.md) and
[intentional-ui-cases-hosts.md](../intentional-ui-cases-hosts.md), under the same section names;
a rule marked *(cases)* has its story there.

---

## One button: intention in `As`, text in `Breakpoint`

**There is one button in NSail. `As` says what the act MEANS and nothing else, and whether the
text shows is the breakpoint's word alone:**

| `As` | Means | Chrome |
|---|---|---|
| `Default` | the ordinary act — **the enum's default, so a button with no `As` is already it** | grey fill |
| `Main` | the screen's one accent, **one per screen** | full-strength accent fill |
| `Important` | notable without being the screen's — a card's own act, a dashboard card's act | `--ns-accent-soft` fill |
| `Inline` | the flat rung with no box of its own — with a glyph and no word it IS the icon button | text/bare glyph |
| `Danger` | the destructive act | error fill |

**No value names a look, a size or a state**, and there is no `InlineMain`: a screen whose
principal act is a glyph writes `Main` at `Never`. How each fill is painted is
[styling.md](styling.md), *A button's fill*.

**Icon-only is a STATE of the label, not a rank** — a control with an `Icon` and no visible
label — so every intention has one and none owns it. It is reached two ways, and they are one
state downstream: `Breakpoint="NsBreakpoint.Never"` (the `Label` stays the name — tooltip and
`aria-label` — and is not drawn), or no `Label` handed over at all. A container query below the
caller's own breakpoint reaches the same shape for as long as the query holds. What it looks
like is the intention's business:

- **`Inline` in that state is the flat glyph** — the vendor's bare icon button, which is what
  a grid row's actions, a title bar's utilities, the header X, `NsHelp` and `NsHint` wear.
- **`NsLink` alone also draws a third `Inline` face no other primitive has**: a bare `<a>`
  carrying both an `Icon` and a word, the two laid out as one row (`ns-link-iconed`) rather
  than a box. An `Inline` link handed an `Icon`, a `Label` and any `Breakpoint` but `Never`
  draws it — that combination IS the face, which is the whole of what reaches it. It takes no
  part in the icon-only state above; `Breakpoint` has no effect on it at all (below), and the
  touch floor does not reach it either — it has no box to grow.
- **Every intention with a fill is a SQUARE**, carrying its own fill and shadow — never a
  rectangle: the vendor's button carries a 64px floor and side padding for a label that is not
  coming. The geometry is [styling.md](styling.md)'s.
- **`Important` and `Default` are one shape and one shadow; only the fill differs** —
  accent-soft against grey. **A filled button is never flat.**
- **`Disabled` is a STATE and it belongs to no intention**: every rank wears the same refusal —
  the scheme's neutral ink, the inert fill under the filled faces, no lift — and **nothing
  about it is the accent** ([styling.md](styling.md)). A page never asks for it by `As`; it
  hands over `Disabled`, or lets the form decide (`NsSubmit`).

### One height under a finger

**Where the pointer is coarse, every action face that draws a BOX is at least 44px high** —
labelled or icon-only, in a title bar, a toolbar or a footer, whatever its `As`. So a worded
act and the glyph beside it measure the same wherever a finger has to hit them, which is the
complaint the rule answers: a phone's title row came out three different heights.

- **44 is not a pick.** It is the touch-target floor the industry already settled — Apple HIG
  44pt, WCAG 2.5.5 *Target Size* — and 36px is a normal desktop control. So **a fine pointer
  keeps the 36px the vendor's medium button has**, and the floor is `@media (pointer: coarse)`
  alone. Not a breakpoint: a phone in landscape is still a finger, and a touchscreen laptop
  reports its mouse as the primary pointer.
- **Stated once, for every container.** A height that changed with what the face is drawn
  inside would be the per-caller branch [principles.md](../principles.md) refuses — one
  declaration in `ns-mud.css`, reaching the worded face wherever the house draws one
  (`NsButton`, `NsLink`, `NsSubmit`, `NsClose`, `NsMenu`'s labelled trigger).
- **The icon-only square keeps its own box at every width and under either pointer**: it is
  already 44px by construction (12px around the Md glyph), so the floor leaves it exactly as
  it is. It is a `min-height` for that reason — a square whose box this grew would be the
  rectangle again.
- **The flat worded anchor is out.** It draws no box at all (the `ns-link-iconed` face above),
  so there is no target to grow; the rule is worded *every intention that draws a box*.

## The six components

| Component | Behavior | `As` |
|---|---|---|
| `NsButton` | plain command button — `OnClick`/`Icon`/`Label`/`Breakpoint`, plus `Submits` for the one that drives a form's `Submit()` (below) | `Default` (default), `Main`, `Important`, `Danger`, `Inline` |
| `NsAction` | wraps `NsButton` with an `ActionItem` (`@GetAction(Delete, row, NsIcons.Delete)`): click, icon and label (`"Actions.{Method}"`) in one call. **Withholds itself inside a form that refuses writes** unless the item declares it writes nothing. **Holds `NsIcons.Progress` in the item's own glyph slot for as long as `OnClick` is in flight**, greyed out, and returns to whatever the item's own face is once the call ends — the row's re-render decides what that is, never this component | same as `NsButton`, default `Inline` at `Never` — an act that wants its word writes `Breakpoint="NsBreakpoint.Always"` |
| `NsLink` | plain navigation link (a real `<a>`) — `Href`/`Target`/`Fragment`/`Icon`/`Label`/`Breakpoint`; greyed by a disabled form, never by one that is merely saving (below) | `Inline` (default), `Default`, `Main`, `Important` |
| `NsPageLink` | wraps `NsLink` for a destination page (`TPage=`/`Parameters=`): `Href` from the route table, `Label` from the page's own `Title` | same as `NsLink` |
| `NsSubmit` | submits the form (`type=submit`, native Enter — except over an open lookup or select, where Enter picks the option and the form never sees it, [fields.md](fields.md)); greys out until the cascaded `FormTracked` reports changes (an `Untracked` form never gates) | hero (the accent), label pinned (`Breakpoint` `Always`); `Filter` = quiet search-bar look; `Send` = same hero chrome, paper-airplane glyph, for a submit that sends words rather than saves a document |
| `NsClose` | closes the hosted surface (`Surface.Close()`), self-wired; renders nothing on the default surface; refused by a form that is disabled or saving — both faces alike on every surface, the dialog's header X included, which reads the same name from outside the form's cascade (below) | `Default` (default) = the footer's worded button; `Inline` = the header X, drawn by `NsButton` like every other glyph |

**The primitives (`NsButton`, `NsLink`) stay pure chrome; derivation lives in a wrapping
component (`NsAction`, `NsPageLink`), never as extra parameters on the primitive.**
`NsSubmit`/`NsClose` stay their own components (cases). What a primitive may still take is a word
about ITSELF — `Disabled`, `Expanded`, `Submits` — never a source to derive its click, its icon
or its label from.

**`NsLink Fragment` names a place INSIDE the destination**, and it is applied to the href the
surface already resolved rather than written into `Href`: a named surface carries its route as a
query *value*, so an anchor inside `Href` would be read as part of the route — the fragment
belongs to the document the browser lands on, whichever surface draws it. Today's one caller is
`NsGuideLink` ([guide.md](guide.md)), which opens a guide chapter at one of its sections.

**A card header's acts are one row, end-aligned** — the card's own and the dashboard's door
alike, at the same box. The door is not the card's markup: the host that placed the card
cascades a `CardDoor` (`Href`, the destination's own title, and the `Target` it mints — `Main`
for every card, no exception) and `NsCard` draws it at the end of `MudCardHeader`'s row, `Default`
at `Never` ([hosts.md](hosts.md)). **Nothing is positioned absolutely against a card.**

Labels of an `NsAction` resolve as `Actions.{Name}`; `NsPartial.GetAction` derives that name from
the method group it is handed ([localization.md](localization.md)).

### An unknown parameter makes the control disappear

**`NsSubmit` and `NsClose` word themselves through `ChildContent`, never a `Label`** —
`<NsSubmit>@Translate("Actions.Close")</NsSubmit>`. Only `NsButton`/`NsLink` take `Label`, and
that split is the trap: the two footer components sit beside each other in every form, so the
wrong one is the natural typo. **An unknown parameter on an `Ns*` component is not a
compile error and it is not a crash — it is the control disappearing.** The Razor compiler
emits any attribute it does not recognize (the component might capture unmatched values; none
of ours does, `UnmatchedParameterTests`), and at render time Blazor normalizes the
`SetParametersAsync` throw into a faulted Task and routes it to the nearest `IErrorBoundary`
**without aborting the batch**. The rest of the tree ships. Inside a dialog — portaled as a
sibling of the router, so outside `MainLayout`'s `NsErrorBoundary` — nothing catches it at all
and the only report is an unhandled circuit error: a dialog renders complete and interactive
with a footer missing its submit.

What holds the line is `DialogRenderTests` — every component handed to `DialogManager.Open<T>`
is rendered in that surface, and a form that draws no submit fails by name. Dialog-hosted forms
are otherwise rendered by no test: a kit's `FakeDialogManager.Open` answers with a completed Task
and never mounts anything. (`NsPanel.Footer` is not at fault here: `NsOpenDialog` already
restores the flex chain a portaled panel depends on.)

---

## A button that submits, and a form that is saving

**A button that drives a form's `Submit()` declares `Submits`, and that is the whole of it**:
it is not pressable while that form is saving, which is the refusal `NsSubmit` already wears — a
second press there re-enters the form's `Runner` and can only earn
`InvalidOperationException("Runner is already running.")`. Presupuestar beside Crear, Recibir
beside Recibir y verificar, Borrador beside Crear y autorizar. **It greys and nothing more**:
the spinner `NsSubmit` swaps its icon for is the FORM's report, one per running form, and a
second one turning beside it in the same footer bills the same news twice — while this button's
`Icon` is still the act's own name.

**Opt-in, because standing under a form says nothing about being about the form.** An Agregar, a
tab's own act, a panel beside the fields all draw inside the same cascade, and a save in flight
is not their business. A form's word is two words and `NsForm` sends them down as two names: a
form **disabled** refuses writes and every button in it is refused with the fields
(`"FormDisabled"`), while a form that is merely **saving** refuses only what declared `Submits`
(`"FormRunning"`). `"ParentDisabled"` is the two OR'd together — what a field answers, and what
the vendor's own buttons read (cases) — and an OR cannot be taken back apart once both halves are
true: **an act that has to tell the refusal from the freeze reads the two names, never the one.**

**A link answers the first and never the second.** `NsLink` declares no `Submits` and drives no
save, so a form that is merely saving leaves it alone on every face it has — Entregar's Cobrar,
Facturar and Ver comprobante stand beside the `NsSubmit` that freezes them and have nothing to do
with `MarkDelivered`. A disabled form still greys the two faces the vendor draws, because the
emphasis ladder makes a link and a command at the same `As` one button to the eye and one of them
going quiet alone is a footer disagreeing with itself. The worded `Inline` face is a bare anchor
with no grey to draw, so it takes only the link's own `Disabled`.

**The vendor is not a second opinion.** `MudBaseButton` reads a cascading parameter of its own
named `ParentDisabled`, the same name `NsForm` cascades, so until it is shadowed every button
under a frozen form goes dead by the vendor's rule and `Disabled` is only half of what the
control prints (cases). `NsButton` and `NsLink` each cascade `false` over the vendor buttons they
draw and answer the question themselves — and `NsButton` answers it in the click handler too,
because the `disabled` attribute reaches a browser one round trip after the form starts running
and the press that beats it there still arrives at the server. `NsTitleBar`'s drawer toggle
carries the same shadow with no rule behind it: a page's title row is drawn inside whatever it
wrapped its panel in, and the way to the menu is the shell's, not the form's.

**The way out is not one of those buttons.** `NsClose` is refused for the length of the save —
the surface it would close is the one this save is about to finish — and it reads the form's word
itself rather than leaving the header X to `NsButton`'s rule and the footer Cancelar to the
vendor's, which is how the two would come to disagree. **Both faces answer together on every
surface**: an aside, a modal, a panel on the main surface, and a dialog.

**A dialog's X reaches that word through the SURFACE, because no cascade gets there.**
`NsOpenDialog` draws it in the `MudDialog`'s `TitleContent`, which the vendor renders in the
provider's tree — above the hosted component and the `NsForm` inside it — so the state travels UP
and back down instead: `NsForm` reports what it cascades to its `SurfaceContext`
(`FormRefuses` — `Disabled || IsRunning`, the footer Cancelar's own word and never the
surface-wide `HasWork`), and `NsDialogExit` re-provides it inside `TitleContent` under the same
`"ParentDisabled"` name, redrawing on the surface's own `RefusalChanged` because mutating a surface
re-renders nothing by itself. **That word has an event of its own and is not on `StateChanged`**:
it is the one thing only this X reads, a save in flight is already announced to everything that
draws a spinner as `HasWork`, and `NsForm` reports it from the after-render of the pass that
decided it — so on `StateChanged` it would redraw every page, title bar and submit on the surface
for a state change already announced, from a render provoked by a render (surfaces.md, the form
seam). `NsClose` reads one name on every surface and knows which none it is
on. **One form answers for a surface and it is the first to report**: a surface can hold a second —
a filter beside a document — while the way out is single, so a filter's own search must not freeze
the way out of the document's save. The `Surface` cascade re-provided in the same file is not the
precedent it looks like: that one originates above the X, so re-providing was the whole fix.

**Escape is the same exit's second face, and it answers the SAVE.** The vendor's
`CloseOnEscapeKey` handler closes the dialog instance directly, past `Surface.Close()` and past
every refusal drawn on the X, and with `BackdropClick` off those two are the only ways out — so
`NsOpenDialog` withdraws the key for the length of the save (`SetOptionsAsync`) and hands it back
as the opener asked for it, never as a second literal. It reaches the host as a call from
`NsDialogExit` rather than being read there: the dialog instance's own cascade does not reach
`TitleContent`. **Not the whole refusal, and that bound is a constraint**: a form standing
`Disabled` refuses writes for as long as its screen says so and the X greys with it, so a dialog
whose Escape went too would have no way out at all — there is no backdrop click and no Back. A save
ends; a standing refusal does not. What Escape does not gain either way is the unsaved-changes
question: a dialog closes through its host rather than a navigation, so neither exit asks it
([intentional-ui-cases-controls.md](../intentional-ui-cases-controls.md), *The exit, and the dialog that ends in Cerrar*).

**An act REFUSED inside a form is refused on that form, and that one is not opt-in.** `Submits`
declares that a button drives the save; this says nothing about the save and needs no word from
anybody — an act the page runs (`GetAction`) hands its `Problem` to the form it is drawn inside,
because that is where the person is looking and the field its reason names is on screen
([forms.md](forms.md), *Placing a refusal*). It lives on `NsAction`, where the press and the form
around it are known at once and every `ActionItem` in the house renders — the toolbar's command
rows included. Not on the `NsButton` underneath, because **taking the claim is what takes the last
refusal down, and only a press of an act has earned that**: the `?` beside the act is that same
button and resolves nothing (`NsHelp`). An act beside the form, one in a portaled menu, and a
handler a page draws as a bare `NsButton` instead of an `ActionItem` all keep the toast.

**A footer standing OUTSIDE the form reaches none of this**, and `Submits` is no use to it:
`NsWizard`'s Back and Next are that case, and the sequence guards them itself
([wizard.md](wizard.md)).

---

## The label, and the enum that owns it

**`Breakpoint` on `NsButton`/`NsLink`/`NsPageLink`/`NsAction`/`NsSubmit` is `NsBreakpoint`**
(`Always`, `Xs`…`Xxl`, `Never`; default `Sm`), and it is **the only thing that decides whether
the text shows** — an `IconOnly` flag would be one more knob that ends up affecting the
breakpoint anyway. There is no second knob.

- `Always` pins the label on. `Xs`…`Xxl` read as *"the label shows from this container width
  up"*: below it the control collapses to its icon alone, **and takes the same square**.
  `Never` is icon-only outright — the collapse, unconditional, with no container query written
  at all. It does nothing without an `Icon`: there would be nothing left to click.
- **Inside a footer the row outranks the word.** `.ns-panel-footer`'s own container query
  takes the label of a secondary act that has an icon under the phone breakpoint, whatever
  `Breakpoint` the page wrote, and spares the hero in both of its colours — a `Breakpoint` is
  a control's own answer and a footer at that width has no room for a line of worded buttons
  ([intentional-ui.md](../intentional-ui.md), important acts sit at the bottom with text).
- **The one exception: `NsLink`'s bare-anchor `Inline` face (`Icon` and `Label` together, no
  box) reads no `Breakpoint` at all.** That face sets no `aria-label` of its own — the word
  inside the anchor IS its whole accessible name — so collapsing it away at a container width
  would leave the anchor nameless rather than iconed. It stays on screen at every width until
  the face has a name of its own to fall back on.
- **Its own enum, not `NsSize`.** A size has no "always" and no "never", and `NsSize` is
  untouched — everything else in the app still measures in it. It is **not** statically
  imported (`Xs`…`Xxl` would collide with `NsSize`'s own), so a page writes
  `Breakpoint="NsBreakpoint.Never"` in full.
- **`NsSubmit` takes the same parameter with the opposite default (`Always`)** — a drawer
  form's save button must never be a guess. **`NsAction`'s default is `Never`** — a row's
  actions are glyphs.
- **A collapsed control whispers its own name on hover**, a CSS bubble gated by the *same*
  container query that hid the label. **A square at `Never` whispers at every width**: its
  label is not coming back, so there is nothing for the bubble to double-bill — and **a menu's
  icon face and a lookup's end adornment ride that ungated form too**, the row kebab included.
  **The flat rung's wordless face is the one that whispers through the vendor's tooltip
  instead** — `NsButton`'s and `NsLink`'s alike — and **a press takes that bubble down**: it
  opens on focus as well as hover, so a press that leaves the control where it found it would
  otherwise leave the name painted over whatever it just revealed. Most presses remount the
  tooltip and hide this (a link that leaves, a row action that re-renders its row); the ones
  that do not are why the exit is the component's and not the vendor's. The next hover brings
  it back.
- **A glyph that is a button is named, wherever it sits.** Not only a control the house draws:
  a vendor part the Stack configures counts too, so `NsAutocomplete` hands its end adornment a
  name (`AdornmentAriaLabel`) the way `NsActionToolbar` hands the kebab one. The test is the
  button, not who rendered it. **Two sweeps hold the rule**: every `NsButton`, `NsLink` and
  `NsMenu` handed an `Icon` writes a `Label` (`NamedGlyphTests` — those three draw a glyph of
  their own, every other face in the house being one of them underneath), and every act built
  with `GetAction` has its `Actions.{Name}` text in both languages (`ScreenStringTests`) — with
  none, `ActionItem.GetLabel`'s last resort is the handler's own C# identifier, so the glyph
  wears `MarkGiven` and whispers it. Their bound is what no static reader can see: a name a
  vendor part carries itself, a name built at runtime, a face composed as child content with no
  `Icon` beside it (an `NsLink`'s glyph, an `NsMenu`'s `Content`) — which is a face the markup
  cannot tell from a worded one — and an `ActionItem` built by hand, whose `Name` is a literal
  the caller chose rather than a handler's identifier.
- **The bubble's text is the control's own `aria-label`**, which `NsButton` and `NsLink` set
  from `Label` on every chromed `As`. A control that restates no name carries none, so it is a
  bare glyph to the pointer and anonymous to a screen reader: **`NsSubmit` names itself with
  its CONTENT and must stay at `Always`** until it is given a name it can restate.
- The honest cost: hover is a pointer's, so on touch a collapsed button is still a bare glyph
  — **it suits universally-read icons, not obscure domain actions** (cases).

---

## Contributed actions

The mechanism that lets an app enrich a kit's screens without owning them (Optical adds
"Prescriptions" to Directory's Parties grid). **The contribution point is an outlet: a component
the projection's owner founds to name the slot** — `PartyRowOutlet` in Directory's Shared —
mounted wherever that kit shows the projection. **The outlet is a slot per *meaning*, not per
screen**: one outlet can be mounted by many pages, and founding a second for the same projection
requires differing semantics (naming.md). **No string keys, ever.** The outlet exists because
the projection cannot carry UI: `PartyRow` is Sdk, and the Sdk stays vendor- and UI-free.
**Data, not handles**: an outlet parameter is something a contributor reads, never a component
reference or a refresh callback — "something changed" is an event (`PartySaved`).

### `ActionItem`

- `ActionItem` (`NSail.Components/Actions/`): `Name`, `Icon`, `Weight`, `Group`, `Disabled`,
  `Severity`, `Writes`, and exactly
  one of `PageType` (+ optional `Parameters`/`Target`), `OnClick` or `Menu`. The URL derives from
  the route table at render. **Links render as real anchors, commands as buttons** — the golden
  rule at the model level. **`Menu` is the third kind of act: offering a CHOICE** rather than a
  destination or a deed — a `RenderFragment` the host renders in an `NsMenu` anchored on this
  item's own face ([menus.md](menus.md)).
- **`Writes` is whether this act changes what the form around it holds** (default `true`): an
  act drawn under an `NsForm` is a way IN unless it says otherwise, and a form that refuses
  writes — `ReadOnly`, `Disabled`, or frozen for the length of a submit — withholds it. The act
  that must survive there declares `false` once on its own item and no screen ever names the
  cascade: `GetLink` emits it (a destination writes nothing), `GetAction` takes it as
  `writes: false` (a probe, a WhatsApp door). It is on the item and not on `NsAction` because the
  toolbar renders a contributed act and forwards no parameter of its own (cases). **A menu row
  answers the same word**, and the face that opens it answers `NsMenu.Writes` beside it — the one
  place the word is a component parameter, because a hand-drawn menu carries no item of its own
  ([menus.md](menus.md)).
- **`Severity` is the STATE the act is about, never its intention**: the status channel's own
  vocabulary (`NsStatusText`/`NsStatusDot`), painted on the glyph by the toolbar from the same
  classes a word would wear — **not an `As`**, because an act is `Danger` when it destroys
  something and never because what it reports is bad news. It is what lets one contributed verb
  say which of several states a row is in where the cell has no room for a word. Unset is the
  ordinary ink, which is every act that reports no state.
- **Link actions are gated by the destination page's own authorize attributes**, through the
  same single gate the nav menu and a lookup's create entry use, so a hidden menu entry, a
  hidden create entry and a hidden row action never disagree. **Command actions carry no gate**;
  the contributor decides with **`NsPartial.CanSend<TMessage>()`** — synchronous and optimistic:
  no policy for the key hides the control, field constraints are not read, the server's gate
  stays the one that decides, and a host with no security registered answers yes — which is
  what lets one card serve the counter and a read-only portal off one allow list.
  **It hides an act whose message the caller was never granted, and nothing else**: where the
  same message is granted to both callers and only one of them may press it — the subject is
  the reader's OWN record rather than somebody else's — the allow list cannot tell them apart
  and the act needs a gate that says so (Optical's `clients.md`, a warranty claim).
- Labels resolve as `Actions.{Name}`, then — for a link — the destination page's own title, then
  the raw name; so moving a row's `NsPageLink` into an `ActionItem` writes no string. Pass a
  `name` only where the row reaches that page for a meaning its title does not carry.

### The outlet, the contributor, the presenter

- `NsActionOutlet<TOutlet, TModel>` (self-typed) is the base: takes `Context`, resolves
  `IActionContributor<TOutlet>` from DI, applies the gate, sorts by `Weight` (ties keep
  contribution order), exposes `Actions` and `Resolving`. **Every contributor is asked before
  any of them answers**, so two slow ones cost the slower and not the sum; the answer is
  guarded by the ask it came from, so a `Context` that moves mid-flight drops the one that was
  in flight for the model that left. **No markup.** A kit's outlet is one file
  (`@inherits NsActionOutlet<PartyRowOutlet, PartyRow>`) delegating to a presenter and handing
  it **both** halves of what it resolved —
  `<NsActionToolbar Actions="Actions" Resolving="Resolving" …/>`, which
  `ActionOutletPresenterTests` holds every outlet to; the host mounts
  `<PartyRowOutlet Context="row" />`. A host that folds its own label when the outlet turns
  out empty (a drawer group with no children takes the same call) reads `HasActionsChanged`,
  fired once with whether any contributor answered — never polled, since the outlet already
  knows the moment it does (the Documento row on `TicketPropertiesPage`).
- `IActionContributor<TOutlet>.GetActions(outlet)` — reads the outlet, decides, returns. **No
  I/O per call** (outlets render per grid row); it never operates the component it was handed.
  **A static render pass awaits the ask** — the loading mark is the interactive pass's answer,
  and a prerender holds the page's HTML until the contributor answers (a 4 s contributor on the
  ficha moved the prerender from 0,2 s to 4,1 s; a grid's rows arrive after the prerender, so no
  row outlet is in it). Contributor classes are named by the contribution alone (`Actions`, or
  `Contributed` when one feature implements more than one contributor interface), live in the
  contributing feature's folder (structure.md, A contributor lives with the feature that
  contributes it), and register with `services.AddActions<Prescriptions.Actions>()` — one call
  registers every interface the class implements. **No delegate/lambda form — one mechanism.**
- **The presenter renders the whole cell**: `NsActionToolbar` shows the first `MaxVisible`
  (default 3) as icon buttons at one size, the rest in an overflow kebab, nothing for an empty
  list. **That kebab is an `NsMenu` like any other**, so the Stack names the vendor's popup in
  one place ([menus.md](menus.md)) — and it is NAMED like any other, carrying the house's word
  for an overflow, so no grid offers a button a reader meets unnamed. **That one size is the house's ordinary icon action (`Md`)**
  — the same box a page's own icon-only `NsLink` renders at any intention, so a toolbar, the
  links beside it and a card header's squares agree without any of them naming a number. A
  grid's action column compacts the *padding*, never the glyph: **a passive label must not
  outweigh the controls that act.**
- **The toolbar does not resolve or contribute; it DOES gate what the host handed it** —
  authorization, and the form's word about writing — and **both gates run before it counts**: an
  item dropped after the cap would spend a visible slot on nothing and leave a kebab opening on
  an empty list. **It reserves its slots** (`--ns-action-slots`), so the icons after it and the
  rows above and below never move (cases) — the reserve is the column's geometry, not a count of
  what rendered, so a form going read-only never moves it.
- **While the outlet is still asking, the slot its first contribution will fill carries the
  loading mark** — `NsIcons.Progress`, at the slot's own box, standing before the constant so
  the row's fixed target does not move when the verb replaces it. Nothing else waits for it: the
  host's `Leading` and `Constant` are drawn from the first frame, and an outlet whose
  contributors answer without yielding never draws the mark at all. **That mark is a different
  moment from a verb's own call in flight**: this one is the outlet still deciding whether the
  verb exists at all, before there is a face to press; once a verb is drawn, pressing it is
  `NsAction`'s own moment and `NsAction` carries its own `NsIcons.Progress`, in the verb's own
  glyph slot, for as long as that click's `OnClick` runs — the two never overlap and neither
  stands in for the other.
- **A page's own row actions are `ActionItem`s handed to the same toolbar** — `Leading` for its
  verbs, `Constant` for the row's fixed target, built with `NsPartial.GetLink<TPage>` or
  `GetAction`. **Never markup beside the outlet.** **The cap is the cell's**: `MaxVisible` bounds
  host verbs and contributions together, the constant outside it. The page still decides when the
  contribution point exists — a create form doesn't mount an outlet, so contributors never see
  unsaved models.

---

## The action cell of a grid row

The rules — one action cell per row and it is ONE toolbar, the constant last, three verbs
visible — are the core doc's. How the cell composes:

- The page hands its fixed verbs in as `Leading` and its constant as `Constant`; the outlet,
  where the row has one, passes both through to the same toolbar. **The cell right-aligns and
  the toolbar packs to its own trailing edge** (cases).
- **The constant never counts against the cap and never folds into the overflow**, because
  anchoring it there is what gives muscle memory a stable target while the variable verbs grow
  leftward.
- `MaxVisible` (3) bounds the whole cell: the host's `Leading` first, ordered among itself by
  `Weight`, then the contributions in the order the outlet resolved them, and whatever exceeds
  the cap falls into the overflow. That is the valve that makes contribution scale — a kit
  contributing a rare action never widens a row. The kebab only exists when there is a
  remainder, and it counts as a slot.
- **`MaxVisible="0"` is the honest end of that range, not a trick**: the cell becomes the
  constant and the kebab, `[✎][⋮]`, and the reserve still comes out right because the constant
  was never inside the cap. It is what a list says when **one** verb is worth a box of muscle
  memory and everything else — the page's own and every contributor's — is worth a menu row
  (Personas). The cap is the host's, so it reaches the toolbar through the outlet's own
  `MaxVisible` pass-through; a contributor never sets it.
- **`ActionItem.Group` is affinity, and the kebab is ruled where it changes**: the toolbar
  draws an `NsMenuDivider` between two *consecutive* overflow items that do not share a group,
  never at either end of the menu and never when the whole menu shares one. It groups nothing
  and sorts nothing — `Weight` is still what puts a family in one run, and the rule only
  reports what the order already says.
  - **The type is any enum the caller owns** (`group: Affinity.Standing`). The Stack names no
    affinity of its own — an affinity is domain vocabulary — and typing it as an enum instead
    of an int is what keeps a page from writing a number nothing explains. `NsPartial.GetLink`
    takes it as `group:`.
  - **Unset is "the rest"**, which is the whole reason contributions need no change: a kit that
    knows nothing about the host's families lands on the far side of the rule by declaring
    nothing.
