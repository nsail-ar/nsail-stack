# Hosts: tables, tabs, list editors, the time grid, the dashboard, load regions

The contracts of the components that host *many* things — a table's columns, a document's
tabs, an editable list's items, a day's blocks, a dashboard's cards — and of `NsLoad`, the
region any of them sits inside when what fills it had to be read.

The rules that decide *what* they show — the comfort curve for visible columns, what earns a
tab, one action cell per row — stay in [intentional-ui.md](../intentional-ui.md). The record
behind them is in [intentional-ui-cases-hosts.md](../intentional-ui-cases-hosts.md), under the same section
names; a rule marked *(cases)* has its story there.

---

## `NsTable`, `NsTh`, `NsTd`

**The status column paints its text with `NsStatusText`** — the opt-in `--ns-status-color`
channel, never a class the cell writes ([styling.md](styling.md)); the row wash is the separate
`.ns-row-danger` class (the core doc's *state versus condition*).

**Two rows are told apart by the LINE between them and by the hover tone — never by an
alternating tint.** No table in the house sets `Striped` and `BrandTheme` carries no banding
colour: a zebra adds a third tone to every list and then has to be subtracted from everything
drawn on a row — the wash a condition paints, the hover that has to still read over it, the
hairline it stood in for. The line is the vendor's own per-row border (`--mud-palette-table-lines`,
`BrandTheme.Lines`), and the vendor drops it under the last row itself. A table's FRAME —
one per table, the enclosing sheet's where there is one — is [styling.md](styling.md)'s.

**A row wears the pointer only where clicking it does something.** `cursor-pointer` and the
vendor's own `mud-table-row-clickable` hang off `OnRowClick` alone, in every mode — a grid that
wires no row click draws its rows under the plain arrow. Most grids answer nothing on the row
itself, and a pointer on one of those promises a door that is not there; having tried it once, the
reader stops trusting the pointer on the grids that do open. The hover tone is NOT part of that
promise — it is one of the two things that tell two rows apart (above) and stays on every grid,
clickable or not. **On a WASHED row that tone is the wash deepened**, never the neutral one: the
vendor's hover REPLACES a row's background, which blanked the condition channel on the one row
being read and dropped the status word onto a ground it had not been measured against, so
`.ns-row-*` hands `--mud-palette-table-hover` its own severity instead
([styling.md](styling.md)).

**A collection that offers a way IN names itself, and the name's bar is the only seat Agregar
has.** `Title` draws that bar above the grid — heading left, add control at the right edge, one
baseline — and unset there is no bar at all, which leaves the control nowhere to live: a grid
wiring `NewRow` with no `Title` is refused by the Architecture suite rather than shipped with no
way in. There is no second position to drift to; an Agregar under the rows is the shape this
rule replaced, after it had already split the house in two (cases). **It is the family's rule,
not the table's** — `NsListEditor`'s bar is the same `.ns-collection-bar`, so the two collection
editors cannot come to look like different components. A page that adds from chrome of its own —
a tab strip's Agregar over the collection — turns the bar's control off (`AddControl="false"`)
and calls `Add()`; the bar, and the heading, stay.

- **`Empty` is what the table says instead of the generic "no records"** — the register's own
  words for having nothing yet, the same parameter `NsDashboard` carries. Unset, the default
  sentence stands. It rides the table rather than the page because the table is what owns *has
  the read answered*: a screen drawing its own empty state beside the grid would print it while
  the first page is still in flight, which is the "a read that failed is not a read that came
  back empty" rule broken.

- **A figure that belongs under a column goes in `FooterRow`, the table's own `tfoot`; a fact
  with no column behind it goes in `Footer`, outside the grid.** `FooterRow` takes one `NsTd`
  per column, each carrying the same `For` expression as the `NsTh` above it, so the figure is
  placed, named and hidden by the column's own declaration — under its header in column mode, a
  card line named by its own column once stacked, gone with the column at a width that drops it.
  A labelled strip after the table has neither: its desktop alignment under the last columns is a
  coincidence of two right alignments, and stacked there is no column left to align to at all, so
  it reads as a band of bare numbers. Both slots render whether or not `Title` is set, and a
  table that declares no footer row renders no `tfoot`.
  - **The cell with no `For` and no `DataLabel` is the one that NAMES the row** — Totales. In a
    body row that same silence is the action cell (below); in the foot there are no acts, so it
    keeps a word's own geometry instead of a botonera's.
  - **Leave an EMPTY `NsTd` where a column has nothing to total**: it holds the column's place in
    column mode and draws no card line stacked. The tick column and the edit column are the
    table's own and it seats them in the foot itself, so the counts cannot drift.
  - **A lines editor's foot keeps its strip.** A discount, a surcharge, a running difference are
    facts no column of the grid states, so a `tfoot` has nothing to align them to.

### Columns and widths

**Which columns survive to which width.** Xl shows every column; md keeps the 2–4 important
ones; sm falls to the vendor's stacked form-mode and shows the phone's 2–3 — the important ones
survive (a work order's Estado is *where the order is*, core, not hideable), the less-important
hide. **The phone's 2–3 is a default, not a cap: 4–5 there is allowed with a justification** —
the burden is naming why, not staying under three. The comfort curve itself is the core doc's.

**`Breakpoint` on `NsTh`** (`NsSize`, default `None`) reads *"the column appears from this
container width up"*. A six-column grid gives up its least important columns one at a time
(Parties keeps `DisplayName` always, brings `TaxId` back at `sm`, `Kind` at `md`,
`Name`/`LastName` at `lg`). Declared once, on the header: **`NsTh` and `NsTd` never reference
each other — they meet through the localization key both derive from their `For` expression.**
`NsTable` cascades a `TableColumns`, `NsTh` declares its breakpoint under that key, each `NsTd`
looks up its own. No `<NsColumn>`, no `<NsTr>`.

- **A column with no `For` cannot be declared, so it never hides** — exactly right for the action
  column. **Do not add it an escape hatch.**
- **That same silence is what names it.** A header with no `For` and no content, and a cell with
  no `For` and no `DataLabel`, are the action column; `NsTh`/`NsTd` mark it `ns-actions` and its
  geometry lives in `ns-mud.css` rather than in any grid. A computed column with no field behind
  it labels its cells (`DataLabel`), which tells the two apart.
- **Hiding uses `display: none`**, so surviving columns take the freed width back.
- **A column the list's own filter already fixes is not hidden by a breakpoint, it is DROPPED** —
  header and cell, under the same condition. A list filtered to one person prints that person on
  every row, which is a column spent saying nothing at any width (`WorkOrdersPage` and
  `PrescriptionsPage` both drop theirs when the party is given). A breakpoint would be the wrong
  instrument: it measures width, and the column is worthless at every width the filter holds.
- **The stacked mobile layout honors the same declaration** — a hidden column's cell stays gone
  as a card line too, at the same container threshold. A card has vertical room a table does not,
  but that room is not a reason to show what the grid already ruled unimportant (cases).
- **When a table with no row open stacks is still viewport-bound** — columns degrade per
  container, and only the open row's stack answers the container too (below, cases).
- **Under 600px the pager drops its page-size label and keeps everything else.** The vendor's
  pager row is one shrink-proof width at every screen, wider than a phone's content box, so the
  arrows would end up past the edge; the select, the row counter and the arrows stay, and the row
  is allowed to wrap under them. A stacked tablet has room for the label and keeps it — the line
  is the width, not the stack (cases).

### Inline editing (`RowEditor`)

**A way IN belongs to the HOST, so the form is what takes it away.** Handing `NsTable` a
`RowEditor` is what offers inline editing, and inside an `NsForm ReadOnly` or `NsForm Disabled`
the offer is off — the `+`, the pencil, the delete and any open editor together — because every
one of those controls is drawn here rather than by the screen that supplied the fragment. The
door a page opens from chrome of its own (`NsTable.Add()`, `NsListEditor.Add()`) is shut by the
same sentence, and `NsListEditor` ORs its own `ReadOnly` into it. Both hosts read the form off
`NsCollectionBase`, so a kit's own `ReadOnly` keeps meaning exactly what it always meant — its
caller's word, answered *under* the form's — and no kit names the cascade.

**An act a kit hand-draws beside that chrome answers the same word for itself.** The editor
that hands `NsListEditor` an `ItemTemplate` and draws its own Add/Edit/Delete as `NsAction`s is
outside everything the host sees, so the act is where the read lands: an act under an `NsForm`
is a way IN unless it declares it changes nothing the form holds
(`ActionItem.Writes`, [actions.md](actions.md)). The default withholds it, the act that must
survive says so once on its own item, and no kit writes the cascade either way — the names live
on `NsActBase`, the base `NsCollectionBase` shares with `NsAction`, `NsActionToolbar` and the
menu family (cases).

**Inline editing binds to the row LIVE, and Cancel exits without restoring.** That is the
design, not a defect: an inline row is a field of an unsaved document, so the unit anyone
discards is the document, never the row. It sanctions inline editing for IN-MEMORY COMPOSITION
only. **The first screen that wants a table whose commit persists per row stops and asks how
Cancel behaves there** — reusing this behaviour would lie, because the previous values were
already saved and there is nothing a document-level discard can undo.

**Taking a row out is that same edit, so the delete control asks nothing.** The press removes the
row from the model the form submits and marks the surface dirty; nothing reaches the server until
the document is saved, and leaving without saving is the undo — a confirm there would be a
question about a keystroke. **The boundary is a press that reaches the server**: a row deleted on
its own, with no document left to save after it, is a destruction and asks first — through the
deleting message's own `ConfirmSend`, never a hand-rolled pair (intentional-ui.md). The Holidays
list is on that side of it, Ajustes de Venta is not. Both row hosts raise removal with no question
(`NsListEditor.OnRemove`, `NsTable.OnRowRemove`), so the screen that deletes per row is the one
that owns the confirm.

- **A `RowEditor` cell's field asks for no visible label (`Label=""`) and is still named.** A
  blank `Label` *hides* the label, it does not remove it: the field keeps the text it would have
  drawn as the input's `aria-label`, and that text is the column header's. Leaving `Label`
  *unset* still derives a visible label; **the two are different answers**.
- **A read column may hide by breakpoint; an edit input never**: hiding an input does not shrink
  the screen, it removes the use case — a Work Order line whose Quantity and Unit Price were `Sm`
  columns would open on a phone with nothing to type into and a Total derived from exactly those
  two. `NsTable` cascades a per-row `TableRowEdit` around the `RowEditor` fragment and `NsTd`
  drops the hide classes while it is present, so **the exception is the seam's, not a
  screen's** — no editor strips its columns' `Breakpoint` by hand, and the rows being read keep
  hiding exactly what they hid before. Stacked, the freed cells become card lines named by the
  same header, which is what "stacked if they do not fit side by side" buys.
- **Stacked, the row open for editing is a FORM: its label sits above its field and the whole row
  shares one left edge.** A row being read keeps the vendor's card line — label left, datum
  right — because a datum is read at a glance where a field is typed into; the label moves rather
  than going. It is the seam's, not a screen's: `NsTd` marks the cells of that one row
  (`ns-cell-editing`) and `ns-mud.css` shapes them, so no editor lays its own row out and every
  stacked editor reads the same way (cases).
- **A growing lookup's cell asks for its width floor from `md` up and for nothing below it.** The
  floor is column arithmetic and below `md` there is no column to do it in: the open row stacks
  (next bullet) and a card line is already the widest a field can be, so there is nothing left to
  ask for and nothing to take back (cases).
- **A table with a row open stacks where its container is under `md`, whatever the window is
  doing.** An edit input never hides, so the open row is the one thing in a grid that cannot be
  narrowed, and under `md` it does not fit any line editor in the tree: in the 688px container a
  `Lg` aside leaves, four inputs that need over 950px between them were handed 104, 74, 57 and 47,
  and "125.430,50" read "50". The shape is the phone's — same card lines, same label above the
  field — and only the QUESTION differs: the vendor's stack asks the viewport, this asks the
  container. **A table nobody is editing is untouched**: a row being read already answers a
  narrow container by hiding columns.
- **In column mode the whole table stops hiding while a row is open**, header and read rows with
  it (`ns-table-editing`). A `display: none` cell generates no box, so a freed editor row would
  otherwise own more columns than the header does and drop every input one column off. That is
  the band between `md` and a column's own threshold — under `md` the row has stopped being a row
  of columns — and it is still no phone's case: a 1000px container on a laptop hides a `Lg`
  column from a row it frees. **The reading list only stays shorter where nothing is being
  aligned**: stacked, a cell is a card line and the read rows go on hiding.

### Paging and scrolling (`Mode`)

- **`Mode` (`NsTableMode`, default `Auto`) decides whether an arriving page replaces the rows on
  screen or joins them.** `Page` is the pager. `Scroll` appends: the rows accumulate, reaching the
  bottom asks for the next page, and there is no pager row at all. `Auto` reads the viewport and
  resolves to `Scroll` at sm and below, `Page` above — the same threshold stacking answers at, so
  the phone that reads a row as a card also reads the list as one list. **Nothing about the
  server contract moves**: the same `OnQuery`, the same page asked for and answered, so no grid
  is edited to get any of it.
- **`Auto` answers `Page` until a browser has reported a width.** A bUnit render and a server
  prerender have no viewport, and MudBlazor's immediate notification classifies an unmeasured
  size as the *smallest* breakpoint — taking that answer would drop the pager out of every
  server-rendered grid before a browser ever disagreed.
- **Scrolling keeps `Cargar más` under the rows.** A first page shorter than the viewport never
  produces a scroll event to ask for the second, and a keyboard has no floor gesture at all; the
  control disappears only when the list is exhausted.
- **Reloading in scroll mode always starts the list over**, `first` or not: the accumulated rows
  *are* the position, so a search, a filter or a sort leaves no row of the previous result set on
  screen. A page still in flight is superseded (`RunOptions.Replace` plus a generation counter),
  never waited for — the append guard would otherwise swallow the reload and leave the old rows
  standing.
- **In page mode the page asked for last is the one that answers.** Every query runs with
  `RunOptions.Replace`: a search typed while the previous page is still arriving supersedes it —
  a busy Runner would refuse it, the vendor swallow the refusal, and the grid keep rows the new
  filter excludes.

### Selection

**Selection is `Selection` plus `RowKey`, and the act that uses it carries it.** Handing the
table a `Selection` (`NSail.Paging`, the same type a bulk message puts on the wire) and a
`RowKey` draws a tick at the head of every row and one in the header for the rows in hand;
ticks are kept by key, so a reloaded page keeps them. Once every row in hand is ticked and the
query answers more, a bar **under** the rows — never above, where its arrival would push the
rows under the pointer — says how many are ticked and offers "Seleccionar los N":
`Selection.All`, every row the list's own filter answers. The filter itself is not in the
selection; the bulk message carries its list message's filter beside it, so the server resolves
"all" by the same query that listed the rows (`DeletePriceListItems`, `AdjustPriceListItems`).
A new search (`Reload(first: true)`) empties the selection. The page's acts over it (Eliminar,
Actualizar) are ordinary header actions, disabled while `Selection.IsEmpty`.

---

## `NsTabs` / `NsTab`

- **Every tab renders; the inactive ones hide by CSS** — an unrendered tab's fields don't exist
  to validation or dirty tracking, and an error the user cannot see is the defect (cases).
- **A tab whose content holds a reported problem marks itself**, derived from the fields that
  rendered inside it, never wired per page.
- **A tab header is label text and that mark, nothing else**: no icon, no per-tab toolbar, no
  lazy-load knob. **Pages name tabs, never tab mechanics.**
- **The tab a reader is on is marked by the accent as INK and underlined by the accent itself**:
  the word reads `--ns-accent-ink` and the slider under it keeps the accent at full strength,
  which is a ground and what the accent is for ([branding.md](branding.md), The accent as ink).
- **The strip may carry acts at its far end — `NsTabs.Actions`** — the page's own acts on the
  collections the tabs hold (Nueva Venta's *Agregar*: a card on Trabajos, a line on Artículos,
  as the `Important` tone), rendered by the page only while the tab they belong to shows.
  Still never a toolbar per tab. A collection whose adds move up there draws none of its own:
  `NsTable.AddControl="false"` plus `NsTable.Add()`. **An act in the strip is not a third tab, so
  it does not wear a tab's height**: the slot hands what it holds to a box of its own
  (`.ns-tabs-actions`, [styling.md](styling.md)) which insets it inside the strip — a page passes
  plain `<Actions>`.
- `NsTab` takes `Title`, content, and an optional invariant `Name` — the word this tab answers
  to in an address, which never renders.
- **The shown tab reaches the query only when the screen asks: `NsTabs BindQuery`.** Key is the
  fixed word `tab`, so one screen binds at most one strip; a tab with no `Name` answers to its
  index. Off by default — turned on by screens something comes *back* to. Inert in a dialog.
- **Whether the panel owns a scroll is inherited, not assumed**: `NsCard`/`NsPanel` cascade
  whether their content stands on a definite height and `NsTabs` reads it — an unset `Grow`
  means "whatever my host said". A call site may overrule (cases).

---

## `NsListEditor`

The editable collection whose items are **heterogeneous** — a table promises homogeneous
columns, and different fields per item make the columns a band of blanks.

Two templates, `ItemTemplate` (only what THIS item has) and `ItemEditor` (each control labelling
itself), plus the collection chrome **built in**: title bar with its `+` — `NsTable`'s bar and
`NsTable`'s rule, above — `Footer`, `ReadOnly`.
**The lifecycle is the `RowEditor`'s to the letter** — `NewItem` (`Func<TItem>`, which **mints
and returns, nothing else**: the list is what appends it to `Items` and what takes it back out
if it is cancelled, so a host wiring only the two templates still adds and still backs out),
`OnCommit`/`OnRemove` carrying `RowEditEventArgs`, an `args.Problem` keeping the item open.
**The open row places that refusal itself**, by the house's two rules: under the field of the row
the issue names, as one strip on the row's last line for anything the row renders no field for,
never a toast (intentional-ui.md, Refusal placement). The row's fields announce themselves to the
list the way they announce themselves to a form, so a screen wires none of it — a page sets
`args.Problem` and nothing else. The next Confirmar lifts it, and so does the submit of the
document the list stands in — which **settles the row left open before it validates**: `OnCommit`
runs again on the values on screen, so Guardar confirms a row nothing refuses (the editor closes,
the document saves in one tap) and is refused by a bad one in the row's own words, under the field
they name (intentional-ui.md). A page writes nothing for that either. **Once the fields have
stacked, the commit pair reads after the last of them**, the way `NsTable`'s row editor seats
Confirmar/Cancelar in the last cell.
**`OnRemove` is the delete control's** — it renders only where there is a handler to take the
item out — and is raised on a cancelled new row as a notification (cases), with no question in
front of it (Inline editing, above). An item is a flex row that **wraps**. Reference:
`TendersEditor`.

**A second way IN rides the bar, it does not replace the first.** `OnAdd` is exclusive with
in-place editing by construction (`ShowOnAdd`), so a collection that keeps hand entry and also
fills itself from somewhere else cannot express the second door as the `+`. That door is
`Actions`, a fragment the title bar renders **before** the add control: the pull sits beside
`+` and `+` keeps the edge, so nothing a caller already wired moves. **Everything in it is a
way IN, so `ReadOnly` takes it with the `+`** — no caller repeats that rule, and a read-only or
disabled form says the same thing over the list's own word (above). No screen in the tree uses
the slot today; it and its rule are pinned by `NSail.Components.Tests`' `ListEditorHost`.

---

## `NsTimeGrid` / `NsTimeBlock`

A day range crossed by the clock, blocks placed by start and duration; **knows no domain,
computes no pixel in C#**. One column per day; below the `Breakpoint` only the focused `Day`
survives. **The track is always the whole day (00:00–24:00) inside the component's own vertical
scroll**; `DayStart`/`DayEnd` are the *frame*, never a window that removes hours. `Now` is
handed in (cases). **`Fits` turns that frame into a ceiling rather than a fixed height**, for
the grid that IS a screen's content: it comes down to the room its surface has left and scrolls
the day inside, so nothing around it ever becomes a second scroll owner. A grid mounted in a
card or among a form's fields leaves it off and stands at its frame (cases).

**A day that qualifies as a whole — closed, borrowed — is `Tints`**: `DateOnly` →
`NsTimeTint(Color, Label)`, drawn as the column's wash rather than as a block, so it takes no
lane, widens no frame and takes no click. It wears a background block's dashed edge in its color
so it reads as a stretch of time; the label sits at the hour the frame opens, and the wash itself
answers the pointer on bare closed time with the day's name, while every block — positioned,
with a z-index — stays above it and keeps its own click and hover.

**A block draws only the lines it can draw WHOLE, and always draws the first.** A block's
height IS its time, so it is the one box that cannot be clipped anywhere convenient: what a
half-hour block on a dense grid has room for is one line, and a second one rendered into it is
sliced through its glyphs. So the block owns its own line height rather than inheriting the
theme's leading, wraps the caller's content in a box capped at a whole number of those lines
(`.ns-time-block-lines`, `round(down, 100%, line)` in `ns-mud.css` — the grid computes no pixel
in C# here either), and its `min-block-size` is one line plus its own frame so the first line is
never the one dropped.

- **`ChildContent`'s ORDER is the caller's declaration of what it will lose**, since what a
  short block gives up is its trailing lines, last first. The first line is the one that must
  matter — a time and what was booked, never a detail.
- **Anything a block might not draw lives somewhere a click can reach**: `Tooltip` and whatever
  `OnClick` opens. Hover is not enough on its own (a touch device never hovers), which is why
  the Hoy card's dropped `Confirmada · Lagos 1034` is still on the status dot, in the block's
  colour and the legend under the grid, and in the actions dialog.
- **The rule is the block's, so it is the same rule on every grid**: the Calendario keeps every
  line it has room for and loses only what it could never have drawn whole, and free-interval
  (`Background`) and overlay (`ReadOnly`) blocks pass through it unchanged.

---

## `NsDashboard`

The host rendering the cards each module contributes, gated per card and ordered by weight; the
contract stays vendor-free (`IDashboardContributor`, `DashboardItem`). **A card that renders
nothing takes its cell with it** — no card opts in. **`Empty` is what `NsDashboard` shows when
the gate left it no card at all**, rendered only after the gate has answered — an empty state
shown in front of cards that were about to arrive is the one-arrival rule broken.

**The per-card gate is two checks, not one.** `DashboardItem.CardType`'s own authorize
attributes answer "may this session ever send this card's message" — coarse, because it cannot
see field constraints. A card whose actual send is narrower than that (a portal's own party, an
org-wide read a constrained grant can never satisfy) names a `DashboardItem.Eligible` —
`Func<SecurityManager?, Session?, bool>`, evaluated against the real `SecurityManager` and
`Session` `NsDashboard` resolves for the render — and `Allowed` drops the card when it answers
false, same as the coarse gate. A card with no `Eligible` is exactly as allowed as before; null
means the coarse gate's word is final.

**A `DashboardItem` with a `PageType` gets a door, and the door is the CARD's, not the grid's**:
the host resolves the address and the destination's title, gates them by the page's own
authorize attributes, and **cascades a `CardDoor`** — `NsCard` draws it at the end of its
header's row ([actions.md](actions.md)). A card's markup says nothing about being on a
dashboard, and nothing is positioned against a card's box.

**Every door opens on `Main`, with no exception a card can declare** (`CardDoor.Target`). A
dashboard card is a screen the session stays in, never one it passes through — the rule is the
link's own, written in [intentional-ui.md](../intentional-ui.md) under *Targets* — so
`NsDashboard` mints the same target for every card rather than leaving a knob for one to
override.

**Cards are merged by `Name`, not concatenated** (`DashboardItem.Merge`), so an app that mounted
a kit's cards **reorders, resizes, hides or re-gates one by naming it and only the field it
changes**. Which fields take the last value and which the first, and the registration order that
drives it, are the drawer's rule, written once in [navigation.md](navigation.md) — as is the
mount rule: a kit calls `AddDashboard<T>` from its own `Add<Kit>Dashboard()`, apart from
`Add<Kit>Components`, because **the kit offers and the app decides**. `AddDashboard<T>` registers
under whichever contract the class implements.

`NsSubjectDashboard<TSubject>` is the same host **about one row**, and **`TSubject` names which
dashboard**, so two subject dashboards cannot collect each other's cards. It merges the same way:
both hosts go through `NsDashboard.Allowed`, which merges before it gates. **It calls `Allowed`
with no `SecurityManager`/`Session`**, so an `Eligible` on a subject-dashboard item is answered
by the permissive default (`security is null || session is null || …`, every existing `Eligible`
lambda's own guard) and never actually narrows anything there — today no subject-dashboard
contributor declares one; a card that means to needs its own field check inside the card, the way
a card without `Eligible` at all already has to.

**A card reads through `NsPartial.Handoff`, never `Send`** — the prerender resolves the read and
persists the answer, the WebAssembly client adopts it, and the page load costs one query per card
instead of two. A subject card passes its subject as the scope. **The handoff is take-once, so
where the card reads matters as much as how**: a card with no subject reads once on mount —
inside `NsLoad`'s `OnLoad` where it has a region, in `OnInitializedAsync` where it does not — and
a subject card keeps `OnParametersSetAsync` but guards on the subject a read has STARTED for,
claimed before the await. Both hosts rebuild the parameter dictionary on every render, so
anything else asks twice anyway (cases). **A card shows nothing until its read has answered**: a
zero, an "empty" or a "clear" painted while the read is in flight is a claim, and a false one —
the region below is what holds that for a card that has one, and the header holds the cell
meanwhile. Held by an architecture test (`CardHandoffTests`), whose exemption list is one card
that defers deliberately (cases).

**A card RE-READS when what it shows is saved somewhere else** — `Subscribe<TEvent>` in
`OnInitialized`, reloading its own region (`TillCard` is the shape). A card is the surface that
goes stale invisibly: the save happens on another screen, usually an aside or a modal opened
over the dashboard, which leaves the main route and the card standing with no return trip to
re-read on. Card by card and never one general `ISaved` on the host: each card hears the events
of **what it shows**, so one save wakes the two cards it moved and not twenty. It **re-reads**;
it never takes the new value out of the event's payload, which is a second source of truth for a
figure the books already own. Held by `CardSubscriptionTests`, whose exemption list is for a card
no door in the repo can move — with its reason beside the name.

**Where the door lives in another kit, the event belongs to the kit whose ledger moved.**
`DebtorsCard` is Accounting's and may not reference Sales or Optical, yet a cobro reaches the
books through Cobrar Venta and Cobrar OT as well as Nuevo Cobro. What those doors publish is
Accounting's own `BooksPosted` — payload-free, because what changed is a balance nobody holds —
beside their own `SaleSaved`/`WorkOrderSaved`. `StockChanged` is the same shape for Products.
A door that cannot be trusted to remember is better reached through a seam it already has to
pass: `TillGate.Through` publishes `BooksPosted` for every act that settles against a drawer,
so the four cobro doors and the next one say it without knowing they do.

---

## `NsLoad`

The region around content that had to be READ — a list, a card's figures, a counter. Three
states and no fourth: **not read yet** (nothing), **read** (`ChildContent`), **could not be
read** (the reason and a Retry, in the content's own place). So a table, a card or a counter
inside one exists only when a read produced it, which is what keeps "no records" an answer
about the data instead of an answer about the network.

- **`OnLoad` is the read, and the region runs it** — on a `Runner` belonging to that one read,
  which is what turns a `BusinessException` into a `Problem` and everything else into
  `SystemProblem.Unhandled()`. The failure is reported handled there, so nothing else answers
  it: **a load failure is never a toast** (it fades, and it carries no retry), and never the
  app-wide splash.
- **Pass `args.CancellationToken` to whatever the read sends** (`ReadEventArgs`). A read the
  region has superseded — a filter moved, a Retry pressed on top of a slow one — is cancelled,
  and the token is the only thing that stops it costing a round trip. It cannot corrupt the
  region either way: each read catches its own refusal in its own object, so the one that was
  replaced has nowhere to draw a failure over the rows the newer one brought back. **A replaced
  read's refusal reaches nobody** — not the region, not the surface, and not the host's
  `OnProblem`: a screen told "could not read" about an answer the region threw away acts on a
  failure that is not on screen, which is the same lie the region exists to stop
  (`NsLoadPagedListTests`).
- **The Retry re-runs `OnLoad` alone.** Nothing around the region reloads, and the page is not
  re-created.
- **`Reload()` is the same act from the screen's side** — a filter moved, a `*Saved` event
  arrived, a row was deleted. A screen keeps an `@ref` and re-reads through it, so its own
  re-reads and the user's Retry are one path with one failure surface.
- **Nothing to read is not a failed read.** A load that legitimately finds nothing — an entity
  that does not exist yet, a search nobody matched — returns normally and the content renders
  its own empty state. Only a throw is a failure (the same distinction as the form's
  blank-by-design model, [forms.md](forms.md)).
- **A read that never happened is not one that came back empty either.** A load that returns
  early because it had nothing to send with — no active organization on a card scoped to one —
  produced no figures, so the content may not print any: it says which of the two it is
  (`DailySalesCard`). Zeros for a day nobody asked about read exactly like zeros for a day that
  had none.
- **The words are the sender's**, resolved through `ProblemManager.GetMessage` — the Stack
  invents no wording, so a reason that reads badly is fixed in its own kit's strings.
- A failed read **replaces** the content rather than sitting above it: a filter that could not
  be applied leaves the previous filter's rows on screen, which is its own lie.

**Not every screen reads through one yet**: a screen that still loads from
`OnCreatedAsync`/`OnParametersSetAsync` gets the floor `NsPage` provides, not a surface of its
own. **A screen being touched for any reason converts**; a list page and a dashboard card
are the two shapes that earn it first.

- **A card's read inside `OnLoad` is still a `Handoff`, not a `Send`** — the region owns the
  three states and the retry, the handoff owns which side asks. `NsLoad` reads once, from its own
  `OnInitializedAsync`, so the two answer different halves and neither replaces the other: pass
  `args.CancellationToken` by name (`Handoff(message, cancellationToken: args.CancellationToken)`
  — the second positional parameter is the scope). The Retry re-reads for real: the prerendered
  answer was taken on the first pass and there is nothing left to adopt, which is what a retry
  should mean.
- **A PAGED list keeps its `OnQuery`: the pager asks for the page and the region reads it.** A
  grid with a server query renders what that query returned, so the read cannot move out of
  `OnQuery` — what moves is where it happens. `OnQuery` writes down the question and calls
  `Reload()`; `OnLoad` sends what is written down and nothing otherwise, which is what keeps the
  arrival to one round trip: a region that read the first page itself would have the grid ask for
  it again as it mounted. The question is spent by the read for the same reason — a Retry
  re-renders the grid, and a fresh grid asks for its page as it mounts. The screen's other
  re-reads (a filter, a `*Saved` event, a deleted row) stay on the table's own `Reload`, which is
  the only path that reaches the grid's query at all; the Retry is the door back from a failure,
  where there is no grid left to ask (`FittingMeasuresPage`, `FittingMeasuresPageRegionTests`).
  **The grid is on screen from the moment it ASKS, not from the moment its answer lands.** The
  flag that routes a re-read is raised in `OnQuery`, beside the question, and lowered only when
  the region reports a refusal — raised after the read instead, it leaves the length of a round
  trip in which a re-read takes the region's door, supersedes the read in flight and finds the
  question already spent: no rows, a mounted grid nobody asks again, and every later gesture
  repeating the no-op. The flag is the one thing on these screens that can go stale, so what
  lowers it has to be exactly the refusal that took the grid away — the rule above, that a
  replaced read's refusal reaches nobody, is what this recipe stands on
  (`NsLoadPagedListTests`).
  **Whether the rows arrived is a browser's word**: bUnit never flushes the page's render inside
  the grid's own query, so a converted grid looks rowless there either way
  (`FittingMeasuresListReadingTests`). What a unit test can measure is the read and the question
  — one read on arrival, one on the Retry, and a re-read dispatched into the window that still
  ends holding its rows.
