# Intentional UI — cases: hosts, tabs and grids

Part of [intentional-ui-cases.md](intentional-ui-cases.md): the why behind the host components
(time grid, dashboard, collection editors), tabs, and grid rules. **Nothing here is a rule** —
the rules are [intentional-ui.md](intentional-ui.md) and the [`ui/` shelf](ui/README.md).

---

# Hosts

## `NsTimeGrid` — the window that removed hours

Treating `DayStart`/`DayEnd` as a window removed hours: a 21:00 appointment, and the now line
past `DayEnd`, simply stopped existing with no scrollbar to say so. Hence the always-whole-day
track: the grid scrolls itself on arrival — centred on `Now` when the range holds its day, else to
`DayStart` — through `nsapp.scrollToFraction`, because how tall an hour is on this screen is the
one thing C# cannot answer. `Now` is still handed in (a prerender and its client must agree where
the line sits) and the live component advances it a minute at a time on a timer that dies in
`Dispose`. The day names stick to the top of the scrollport, in the same layout box as their
columns, so they cannot drift by a scrollbar's width.

The price, paid on the Calendario: a frame sized in hours is a FIXED height, and the default
twelve already outgrow a laptop's content area once the legends under the grid are counted
(measured: a 704px frame in a 677px box), so the panel scrolls the grid — sticky day names,
legends and all — which breaks the one-overflow-owner rule from inside the component. `Fits`
makes the frame a ceiling: only `min-height` moves, so a grid with room to spare stands exactly
where it stood and one squeezed for room comes down to it. It is a PARAMETER, not the ambient
`ContentGrowth` the tabs read, because the answer is about the grid's own parent, not the
surface: two of the three callers sit inside something else under a growing `NsPanel` — a form
grid item, and a dashboard card whose cell would be free to squash a grid that had given up its
content floor.

## `NsDashboard`

The host marks each card's slot and hides the item whose slot stayed empty — no card opts in,
and the hole dies with the cell. `NsSubjectDashboard` exists because the ficha is to the patient
what the dashboard is to the óptica: the mechanism was scoped rather than a fourth contribution
pattern founded.

**A hidden card is dropped, a hidden nav entry is marked** — the same intent on both (kit plants,
app renames or hides). `NavMenuItem` keeps a `Visible = false` entry in the tree because
`NsTitleBar` reads `Find` for the glyph of a page reached some other way; nothing reads a dropped
card, so `DashboardItem.Merge` leaves it out and the grid cell goes with it. The other asymmetry
is the throw: on the dashboard an override is told from a card by its absent `CardType`, so a row
naming a card nobody plants is a dead instruction and says so — in the drawer the two shapes are
identical and the merge cannot tell.

## The second Agregar, and why naming the collection became the price of one

`NsTable` drew its add control in two places: on the collection bar when `Title` was set, and in
a left-aligned row *under* the rows when it was not. The second was the original and the first was
opt-in, so the house split along whichever editor had been touched since — top right on Nueva
Venta's lines, the Seña and Nueva Orden de Compra, bottom left on Editar Lista de Venta's two
grids, Nuevo Asiento, Nuevo Comprobante and two Puesta en Marcha steps. Leonardo found it from
the screen, not from the code: "agregar quizás debería ir arriba a la derecha, coherente con
otros agregar", then "quizás debemos revisar todos los agregar del sistema".

Keeping both and defaulting to the bar was refused: a default is a drift that has not happened
yet (principles.md 4 — remove the possibility, not the instance). Removing the bottom one costs
something real, though — it makes `Title` load-bearing, and a grid that forgets it loses its way
in SILENTLY, which is the worst failure there is. So the cost is paid where it cannot be
forgotten: the Architecture suite reads every `.razor` in `src` and refuses a `NewRow` with no
`Title`. The six grids that moved had the heading already — four printed it as a loose
`NsText As="Title"` line above the grid, which the bar now carries, and the two wizard steps took
the name the wizard's own rail already owns. Nothing new was translated.

The sweep that rule runs on had a blind spot worth knowing about: `RazorMarkup` dropped the
frames still open at the end of a file, and a `RenderFragment<TRow>` in an `@{}` block opens a tag
named after its type argument that never closes — so every element at a file's ROOT hung under it
and was invisible. Both Puesta en Marcha steps are exactly that shape. A reader that silently
loses a whole file is worse than one that finds nothing, which is what the floor test beside each
rule exists to catch; the floor passed anyway, because the other files carried the count.

## `NsListEditor`

The title bar is the same `ns-collection-bar` `NsTable`'s own `Title` renders — one class, one
rule, so the two collection editors cannot come to look like different components.

**Why the list writes into a collection it otherwise only reads.** If `NewItem` had to mint the
row AND append it — an obligation its name denies and its type (`IReadOnlyList`) cannot
express — a host that forgot would open the editor on a row the markup never iterates: the add
control greys itself over an unchanged list and nothing says why. That is silence, the worst
failure (principles.md 4), and it reached prod (Nueva Presentación could not take a single ítem).
A happy-path test would not stop the next host, which writes the same reasonable code. So the
obligation lives in the component — `Items` is `IList`, `Add` appends, `Cancel` un-appends the
row it added — and `NewItem` means what it says. `OnRemove` stays the caller's for the delete
control alone: no handler renders no control, so there is no obligation to forget.

**Why the open row places its own refusal, and on which `EditContext`.** Ajustes de Venta
answered an empty Nombre with a red "Obligatorio" in the corner and no mark on the field: the
list raised `OnCommit` through its `Runner`, and the host the report landed on was the LIST, which
claimed nothing and let it fall to the snackbar — the form that anchors a refusal never saw it.
What the form does with `ApplyProblem` is nothing form-only, so it moved into a piece both hosts
hold (`RefusalPlacement`, [ui/forms.md](ui/forms.md)) rather than being written twice; the list
shadows the field-tracker cascade and forwards up, exactly as `NsTab` does, so a field inside an
open row is still known to the form and to the tab whose badge reads it.

The messages are posted to the FORM's own `EditContext` where there is one — the row's fields
already announce `(row, "Name")` there and already read a message back from there, so the refusal
lands under the field without the row's editor becoming an `EditForm` of its own. A list mounted
outside any form gets one of the list's own, modelled on the LIST and not on the row: a context
modelled on the row would make `ValidatedModels.Covers` true for it, and every `[Required]` on a
row model nothing validates would start drawing a required mark for a rule that refuses nothing
(intentional-ui.md: the mark is derived from what refuses).

`NsTable`'s row editor has the identical hole over eleven kit screens; the piece lives in
`NSail.Components` so that story writes a placement, not a second rule.

**Why a validation pass lifts every placement on the context, and why a host lets go of one when
it leaves.** Posting the row's refusal on the form's context put it where nothing could lift it:
a `ValidationMessageStore` clears only its own messages, so the form's submit — clear my store,
then `Validate()` — kept counting the row's, answered false and returned having run no handler
and drawn nothing. Nueva Venta's Completar went mute with a row open, which is the class preflight
names (#1029). So the lift is keyed on the moment, not on the owner: a validation request is the
"next submit" the rule already spoke of (intentional-ui.md), and every placement on that context
lifts its own answer when one arrives. Nothing is let through quietly — what still refuses posts
again in the same pass (each field's own problem, the annotations validator's), so the submit runs
or it draws. The worse tail is the same mechanism: a list unmounting with a row refused would
leave the form a failure with no input on screen to carry it and no owner able to clear it, so the
host's own end is where it unbinds — the instance's end is where it spends what it left behind,
the rule `NsComponent.Dispose` already follows for a dirty report.

**Why the commit pair needs a container query.** The item is a flex row that WRAPS, so at a
phone's width the five fields fold onto lines of their own while the actions stack — one flex
child among two — stays on the first line, vertically centred against them: Confirmar's top
landed inside Base's box with Habilitado further down. CSS cannot ask "did my sibling wrap", so
the fold is declared instead, at the house's own `sm` bound and on the OPEN row alone
(`.ns-list-editor-open`): a folded row's edit/delete are its trailing affordances at every width.
The measuring reference is `.ns-list-editor` itself rather than the panel around it — the width
that decides this is the list's own, and that way the fold holds wherever a kit mounts the list
(the `NsTimeGrid` precedent). bUnit lays out no box model, so the reading is pinned in a
phone-shaped browser (`ListEditorRowRefusalTests`).

And the open row is the one row that wears no `align-center`: MudBlazor's utility is
`align-items: center !important`, which no query can outrank, so a row keeping it would fold to a
column still centred — the fields block surviving only by its own `fit-content` and the refusal
strip reading as a shrink-to-fit pill in the middle instead of the row's last line. An alignment
that answers to a width cannot be a utility class, so the open row's is in `ns-mud.css` beside the
query that changes it; the folded row keeps the utility, its alignment never changing. What bUnit
can pin is the one thing the cascade turns on — which classes the open row carries
(`TheOpenRowWearsNoVendorAlignmentUtility`).

## The way in nine row editors could not have withheld

Nine kit row editors were blind to the form's writability cascade — each gating its inline editor
and its `+` on its own bare `ReadOnly`. Copying the picker family's fix (`NsPickerBase`) onto them
was refused for what a row editor actually owns: it hands `NsTable` a fragment and `NsListEditor`
a flag, while every control at issue — the `+`, the acts beside it, the pencil, the delete, the
open editor — is drawn by the host. Nine ORs of the same cascade are a second thing to keep in
sync (principles.md 4), and the tenth editor arrives without one. So the read lives on
`NsCollectionBase`, under both hosts, as ui/hosts.md states: *no caller repeats that rule*. A kit
editor's own `ReadOnly` stays its caller's word and is answered under it.

**What the host read reaches, and what it does not.** It reaches every way in the host itself
draws: the bar's `+` (`NewItem` or `OnAdd`), the `Actions` beside it, the pencil `ItemEditor`
turns on, the delete `OnRemove` renders, and any open row editor. It does not reach an act a kit
hand-draws in its own `ItemTemplate` or header — that markup is the kit's. `LocationsEditor` and
`AttendeesEditor` are that shape: each hands `NsListEditor` an `ItemTemplate` only and draws its
own Add/Edit/Delete as `NsAction`s outside the host's chrome (`AttendeesEditor`'s add is a
`PartyLookup`, a picker over a field, and closes on the field's own read). `ChannelsEditor` is the
same shape with no host at all. How a kit-drawn act reads the cascade is answered by the act
itself (next section).

Extracting the pair `NsFieldBase` and `NsPickerBase` already duplicate is not available: the
three chains meet only at `ComponentBase` (`NsFieldBase` does not descend from `NsComponent`), so
one shared base would sit under every component in the app — a cascade subscription per component
for a rule three families need. The cascade's NAMES stay the Stack's in all three, which is what
the rule against a kit writing `"ParentReadOnly"` is for.

## Why an act declares that it writes, instead of the form deciding for it

The three editors above are the same defect one layer out: the read reaches everything a host
draws and nothing a kit draws beside it. A fourth `ReadOnly` OR in each editor is the shape just
refused, so the read went where the control is — **an act drawn under an `NsForm` is a way IN
unless it declares it changes nothing the form holds** (`ActionItem.Writes`, default `true`).

**The blunt gate — every act under a form's cascade goes dark — was refused, not on taste.**
`NsForm` cascades `Disabled || IsRunning`, so the gate fires on every submit of every screen, not
just on read-only screens. The census of acts under a form is small and three of them are not
ways in — `StorageSettingsPage`'s `TestTarget` probes and writes nothing back, `ChannelsEditor`'s
`Contact` opens WhatsApp, and a toolbar's view links go somewhere. All three would blink away
once per save, forever.

**The default is withhold, not stay-live** — principles.md 4 spent deliberately: with the default
the other way, the tenth editor arrives without the word and is the bug again. Erring toward
withholding hides a button on a screen nobody has yet; erring toward live leaves a write
reachable on a form that refuses writes. And it **withholds rather than greys out** —
`NsCollectionBase`'s own rule, for the same reason: an act holds no value of its own to grey out,
and both states mean there is no way in.

**The word rides `ActionItem`, never an `NsAction` parameter.** `NsActionToolbar` renders a
contributed item as `<NsAction Action="@item" …/>` and forwards nothing else, so an act reaching
the screen through an outlet could not exempt itself if the word were a component parameter.
`NsPartial.GetLink` emits it `false` — a destination is not a write — and a hand-built link inside
a form declares the same (Therapy's two contributions to `AppointmentModelOutlet`). **The toolbar
filters, a bare `NsAction` withholds itself**: the toolbar drops what it withholds where it drops
what authorization refuses, BEFORE `MaxVisible` and the kebab are computed, or the visible slice
spends a slot on nothing and a kebab opens on an empty list.

The shared read is `NsActBase`, and `NsCollectionBase` sits on it — one copy of
`"ParentReadOnly"`/`"ParentDisabled"` for the chain that draws ways in. `NsFieldBase` and
`NsPickerBase` still do not join it: those chains meet this one only at `ComponentBase`.

**The vendor reads `"ParentDisabled"` too** (measured against MudBlazor 9.10): `MudBaseButton`
declares a cascading parameter of that exact name and ORs it into its own disabled state. The
name is the vendor's convention, not a coincidence — so every `NsButton`, `MudButton` and
`MudIconButton` under an `NsForm` answered a cascade NSail provided for its fields, and
`NsButton.Disabled` was only half of what the control printed. It is why a form freeze seemed to
reach Presupuestar one round trip late — while reaching every unrelated footer button at the same
time. `NsButton` and `NsLink` each cascade `false` over the vendor buttons they draw and decide
for themselves — a disabled form refuses them with the fields, a merely saving one refuses only
what declared `Submits`, which a link never does ([ui/actions.md](ui/actions.md)). **A vendor
cascade sharing a name with ours is a decision leaking across the boundary**, and it is stopped
at the boundary, not documented: stopping it at `NsButton` alone left Entregar's Cobrar, Facturar
and Ver comprobante dead for the length of the save — not greyed, gone, because MudBlazor draws a
plain `<button>` once it draws a disabled state and the address goes with it.

The census of what draws a vendor button in the Stack is six components, and every one a form's
cascade can reach answers it on purpose. `NsButton`, `NsLink` and `NsTitleBar`'s drawer toggle
shadow it — the toggle unconditionally, since the drawer is the shell's and no form owns the way
to the menu. `NsSubmit` and `NsClose`'s worded face leave it standing, right for both: a form that
refuses writes or is saving must refuse a submit and a way out of the surface that save is about
to finish. `NsAppBar`'s hamburger is the sixth, mounted by no layout and outside every form.
**A new `Mud*` control drawn anywhere a form's cascade can reach owes one of those two answers, never silence.**

What no shadow can fix is a control the cascade never reaches. `NsClose`'s header X was that
case, closed by the dialog's plumbing: `NsOpenDialog` draws the X inside the `MudDialog`'s
`TitleContent`, which the vendor renders in the provider's tree — above the hosted component, and
so above its `NsForm`. Nothing could be shadowed or re-provided there: the surface context
re-provided beside it originates *above* the X, the form's refusal below it. So the word goes up
through the one object both ends hold and comes back down as the same cascade — `NsForm` reports
to `SurfaceContext`, `NsDialogExit` provides `"ParentDisabled"` inside `TitleContent`, and the
button's own rule is unchanged ([ui/actions.md](ui/actions.md), `HostDialogSurfaceTests`).

## Why a menu declares for its face and a row for its act

The census above was of `NsAction`s and missed the third chrome an `ActionItem` has a mouth in:
`NsMenuItem` took the same item and read nothing of it. A kit drawing its own `NsMenu` inside a
form was the live case — `OpticalJobCard`'s Comparar writes the very field the `NsSelect` beside
it greys out — on a form that had merely refused writes, not an unmounted read-only screen.

**The row's answer needed a bridge, not a read.** A menu's body is the vendor's popover content,
rendered under the popover provider, a sibling of the router, so *nothing* `NsForm` cascaded
reaches a row — measured, not assumed; the same hole the row's own surface fills with
`RootSurface`. `NsMenu` re-cascades the two names it read at its own position — the shape
`NsOpenDialog` uses for `RouteTable`: **the host standing on both sides of a portal carries the
bridge, never the component being reused**. A field a kit seats in a menu body reads the truth
for free from the same three lines.

**The face could not be derived, so it declares.** Nothing renders until the trigger is used —
load-bearing for a menu whose body has to be read for — so *is anything left in here* has no
answer when the face is drawn, and a face over an emptied list is a button that opens nothing.
`NsMenu.Writes` is the same word `ActionItem` carries with the same default, so a kit writes
nothing and the trigger goes with the rows it would have opened. **The overcount is deliberate**:
a menu mixing a write with a read is withheld whole unless it says `Writes="false"` — the cheap
side of the same trade `ActionItem.Writes` makes.

**The toolbar declares `false` on both menus it draws, and that is not an exemption.** Its items
passed the form's word where they passed authorization, before `MaxVisible` — so every kebab row
is one this form allows, and asking the face again would delete the overflow of a read-only row
that still has links to offer, plus the contribution that declared it changes nothing. Filtering
before counting also keeps `ActionItem.Group`'s divider honest: it is drawn over what survived,
so nothing separates nothing.

---

# Tabs

## What "keeps the panel alive" actually renders

Measured against MudBlazor 9.10.0:

- `KeepPanelsAlive` defaults to **false** — an inactive panel's whole subtree is unmounted: the
  field leaves the DOM *and* `EditContext`, its validation message stops existing, and a submit
  the user cannot see a reason for goes through. `NsTabs` sets it `true`.
- With it on, every panel mounts at first render (the inactive tab's content runs
  `OnInitialized` once, up front) and **which one shows is a class, not an attribute**: the
  showing wrapper carries `.mud-tab-panel-active`, the others nothing. A rendered panel has no
  `style` of its own — the vendor's stylesheet draws `.mud-tab-panel:not(.mud-tab)` as
  `display:none` and `.mud-tab-panel-active` as `display:contents`, which is why `NsTabs`' flex
  column reaches the content. Read the class: bUnit applies no stylesheet, so nothing about the
  hiding is visible in the DOM except the class that triggers it.
- **Not `.mud-tab-panel-hidden`** — that class exists in MudBlazor's stylesheet with exactly
  `display: none` and would beat the active rule, but MudTabs never applies it on this path; a
  test asserting the class passes vacuously.
- `mud-tab-panel` is on the **header** buttons too — scope to
  `.mud-tabs-panels > .mud-tab-panel`, which is also why the vendor's own rule carries a
  `:not(.mud-tab)`.
- `NsTabs` owns no visibility CSS; `ns-mud.css` has no tab rule at all.

## Why a tab derives its problem mark instead of the page wiring one

`RefusalPlacement`, which is what `NsForm` reports through, puts every reported issue into the
`ValidationMessageStore` bound to the host's `EditContext` — the store
`DataAnnotationsValidator` writes to — so
`EditContext.GetValidationMessages` is one read covering client validation and the server's
problem alike. A tab only needs *which fields are mine*, which `IFieldTracker` carries: the field
never names a tab, the tab never names a field, and no page wires a badge. `Icon` and `Disabled`
came off `NsTab` for having no consumer; the parameter surface is pinned
(`NsTab_ExposesOnlyLabelAndContent`) so growing it on speculation has to break something first.

## Which screens bind the tab to the query

`ProfilePage` (an identity provider's callback has to land on Cuentas), `UpdatePartyPage`,
`UpdateOrganizationPage` — the screens something comes *back* to. The compression defect behind
the inherited-scroll rule was Mi Perfil's Accounts tab: a panel wrapper declaring scroll inside a
chain that resolves to no height does not scroll, it compresses.

---

# Grids: column importance, the action cell, and state

The comfort curve reads per width — the phone shows the optimal 2–3 and a wide desktop up to 5;
**4–5 on the phone is allowed with a justification** — the burden is naming why. The genuine
7-critical-columns grid is dealt with when a real one appears, not anticipated.

## The action cell's hole, and the Roles zigzag

Markup beside the outlet left the slot reserve stranded mid-cell — a hole before the lupa, and a
zigzag on the Roles grid when two row shapes each aligned themselves. Hence: the cell
right-aligns, the toolbar packs to its own trailing edge, and a page's own row actions travel as
`ActionItem`s into the same toolbar.

## The pager row at phone width

Measured: every box in `MudTablePager`'s toolbar is `flex-shrink: 0`, so the row has one width
at every screen — 392px with the Spanish caption — while a 428px phone's content box is about
380; `.mud-table-pagination` carries `overflow: auto`, turning the difference into a horizontal
scroll nobody looks for. The page-size caption is a plain `div`, never a `<label>` bound to the
combobox, so dropping it loses nothing to a screen reader. The threshold is 599.98px, not the
960px the table stacks at: the defect is width, and a stacked tablet has the room. The test pins
the DOM shape those selectors depend on, so a vendor that moves the captions is caught.

## The stacked open row is a form

Seen on Nueva Orden de Compra on a phone. The vendor draws a stacked cell as one flex row —
`justify-content: space-between`, the label as a `::before` on `data-label` — and
`.mud-input-control` is `flex: 1 1 auto`, so a field takes whatever the label left and starts
where the label ends: `Producto`, `Descripción`, `Cantidad` and `Precio unitario` on four
different left edges in one card, the two-line label pushing its field further than the rest.
That row is the only place in a stacked grid where the vendor's shape fails, because a datum is
read at a glance and a field is typed into: four boxes on four edges is the form the thumb has to
hit.

Two shapes answer it — labels given one width so the fields line up, or the label moved above its
field — and the second landed: a label width is a guess a longer column name or a second language
invalidates, and the wrapping label is exactly the case the guess gets wrong. **The label moves;
it never goes.** The row being READ keeps the vendor's card line unchanged.

Scoped to the open row's own cells (`ns-cell-editing`, rendered by `NsTd` off the same
`TableRowEdit` cascade that drops their hide classes) rather than to the table's
`ns-table-editing`, which says only that some row is open. The load-bearing declaration is
`align-items: stretch`: in a column-direction flex box the vendor's own `center` would shrink
every field to its content width and centre it, so the shared edge comes from `stretch`, not from
the direction. One rule in the Stack is one result in the four stacked editors that carry line
rows — `PurchaseOrderLinesEditor`, `SaleLinesEditor`, `VoucherLinesEditor`, `PriceRulesEditor` —
measured in a phone-shaped browser at 375px (`StackedEditorRowTests`); bUnit can pin the class
contract, never an edge.

## The open row stacks where its container cannot seat it

Nueva Orden de Compra in a `Lg` aside: Producto 104px, Descripción 74, Cantidad 57, **Precio
unitario 47**, where "125430,50" read "50". Editar Orden de Compra, at `Xl` (1160px), read the same
field at 149.

**The arithmetic was over before it started.** In the 688px container that aside leaves, the six
cells read 192/122/106/96/70/100 — of which 232px is the vendor's dense cell padding
(`padding: 6px 24px 6px 16px`, four cells' worth before the row says anything) — while the four
inputs need over 950px to show the values they hold. Every word the tree had was redistribution:
a preference moves width from one column to another, a floor demands it
([cases: controls](intentional-ui-cases-controls.md), *Why the open list is floored at the field
and capped at a measure*), and neither invents any. Returning the surface to `Xl` would have
undone the point of the smaller aside.

**What fits is the shape the phone already reads** (*The stacked open row is a form*): the open
row as a form, label above field, every field on its own card line — 654px of box in the same
aside. What kept it off this screen is that the vendor's stack asks the VIEWPORT (`.mud-sm-table`
under its own `max-width: 960px`) while hiding asks the container, so a 720px panel on a 1600px
screen fell between the two. So `ns-mud.css` asks the container as well, under
`@media (min-width: 960px)` — a wide viewport, where the vendor's stack is not already the
answer — and only while a row is open (`ns-table-editing`). A table nobody is editing is
untouched: a row being read already answers a narrow container by hiding columns.

- **The gate is an overlap, not a complement.** The two regimes meet at exactly 960px, where both
  fire and say the same thing. Complementing `max-width: 960px` with `min-width: 960.02px` would
  leave a fractional gap where neither does — the trap the `ns-table-editing` rule above the
  block already documents for its own pair.
- **The vendor's base is copied** — display, the `::before` on `data-label`, the dense stacked
  padding, the body's top line and the row's separator — because `.mud-sm-table` is the
  viewport's word and nothing may put it on. That is the vendor-CSS-copy cost named in
  *Container queries: the measured facts* ([cases: shell](intentional-ui-cases-shell.md)); the
  alternative is a row that cannot be typed into.
- **One pair of hide/re-enable rules, not four.** Inside a container under 960px the `md`, `lg`
  and `xl` thresholds cannot match, so only the `sm` half is reachable; the other three would be
  rules no width can reach.
- **Per-row stacking is not available.** A `tr` that is not a table-row is wrapped in an
  anonymous table-row and cell, which lands in column 1 and takes that column's width — the whole
  table is what stacks, in the vendor's rules and in these.

What the column-mode rule keeps is the band between `md` and a column's own threshold: a 1000px
container hides Nuevo Comprobante's `Lg` column and frees it again while the row is open
(`EditorRowColumnModeTests`). The geometry itself is in `OpenRowFitsItsContainerTests` — the
aside walked through its own door, and the other three line editors at the same container width —
and the failing reading is an input's box against its own value (`Ui.ClippedFields`): a cell
holding a shrunken field is not clipped itself, which is how a green suite kept missing it.
