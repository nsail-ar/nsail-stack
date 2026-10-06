# Layout components and styling

How the layout components are parameterized, where a class may live, and how a rule measures a
*container* instead of the window.

The rules that decide the shape of a screen — no stylesheet tree shadowing the DOM, components
lay themselves out, pages never write div soup, responsive is measured per surface — stay in
[intentional-ui.md](../intentional-ui.md). The record is in
[intentional-ui-cases-shell.md](../intentional-ui-cases-shell.md) under *Layout and styling rules*; a rule
marked *(cases)* has its story there.

---

## Grid, stack, growth, scroll

- **Grid vs Stack**: `NsFormGrid`/`NsFormGridItem` is the grid for forms — a flex-wrap band
  where each item declares its **width nature, never a fraction** (the core doc's *Form
  density*); there are no column spans and no `.ns-col-*` rules. `NsStack` (`Horizontal`, `Gap`,
  `Margin`, `Align`, `Justify`, `Grow`, `Scroll`) is for 1D flows; both axes share `NsAlign`.
  **`Grow` is a field's other honest width — open-ended text (a name, a description, a search)
  fills the rest of the line.**
  - **The content width is a cap the container decides**: 14rem, or half the line less the gap
    where half a line is narrower than that, so two content-width fields always share a row in a
    448px aside and nothing moves on a surface wide enough for two at 14rem. Under two fields at
    the growing basis — the phone — the cap lifts and each field owns its row. `--ns-field-width`
    (`.ns-field-narrow` 6rem, `.ns-field-money` 10rem, `.ns-field-wide` 20rem) is a field's *own*
    width and is never capped: it is what a label too long for half a line asks for, and
    `.ns-field-money` is the other direction — a **block** of money figures read together (Cerrar
    Caja's four) at the same 10rem `.ns-money-slot` calls a price's own, so four share one line
    instead of three sharing it and the fourth taking a row the content under it needs. A lone
    importe among fields of other kinds stays at the family's 14rem.
  - `.ns-field-pair` raises a `Grow` item's basis to 32rem — two blocks of fields (a job card's
    eyes) pair only on a line that seats both as rows, and stack below it; `.ns-heading-row`
    gives such blocks' headings one height (a checkbox row's), so the fields under them start
    level.
  - `.ns-money-slot` holds a price at 10rem beside the article it prices in a horizontal
    `NsStack`, and `.ns-field-slot` is the other half of that row — a field laid out by hand
    reading the same `--ns-field-width` the grid's items read, so `ns-field-wide` means 20rem
    wherever a field is placed (a job card's Armazón, which spans the whole card's row and is
    not the whole card's business).
  - `.ns-select-gutter` mirrors a selecting `NsTable`'s own checkbox column width, by hand, on
    a persistent add row laid out beside the table rather than inside it — so the row's fields
    start under the table's own columns instead of its leading edge. Hidden below the width
    `NsTable` itself stacks at (`Breakpoint.Sm`), where there is no checkbox column left to
    mirror. `.ns-row-submit` marks a submit act that shares that same row with labeled fields
    instead of sitting in a form's own footer, so `align-items: flex-start` would otherwise
    leave it floating on the fields' label gutter rather than reading as their row partner.
    A **boolean control** is the same mismatch and takes the same answer: it draws neither the
    label gutter nor the outline its row partners draw, so `NsRecordState` and `NsCheckBox` mark
    their own control (`ns-record-state`, `ns-form-check`) and the grid centres that cell. The
    mark is on the component, not on the grid — a row of nothing but booleans keeps its
    flex-start — and the selector reaches a DIRECT child only, so a check nested in a stack
    beside an `NsHelp` owns its line already and is left alone.
  - A field cell whose content draws nothing (`StoreSelect HideLone`) collapses.
- **`Gap` and `Margin` read off one `NsSize` scale** — room *between* children and room *around*
  the block, asked for the same way, so a page that wants a logo to breathe writes `Margin="Lg"`
  rather than a spacing class.
- **`Grow`** makes a component fill its flex parent: default **true** on page-level fillers
  (`NsContainer`, `NsCard`, `NsTable`), **false** on local arrangers (`NsStack`).
- **`NsContainer` stacks by default** — two blocks a page renders in it sit one above the
  other; a caller that wants a row asks with `Horizontal`, the same word `NsStack` uses, and
  no caller does yet (cases).
- **`Scroll`** (`NsStack`, default false) makes the stack the scroll owner of what it holds.
  Pairs with `Grow` — grow to the surface, scroll inside it.

---

## Cards, panels and the scroll owner

- **Card chrome**: `NsCard` carries the vendor's elevation 1 and **no border of its own**.
  Elevation's own ring IS the edge: `BuildElevations` casts a 1px 6% white ring at every level,
  which in dark reads as the lit top edge and in light falls away over a white surface leaving
  the shadow to do the work. With the canvas sunk a real step below the surface in dark
  ([branding.md](branding.md)), a border on top of that ring would be a third edge saying what
  two already say.
  - **ONE FRAME PER TABLE: a table inside a sheet is flat on it.** A `MudTable`'s root is itself
    a raised sheet — `Surface` for a fill, the house radius, elevation 1 and so that same ring —
    so an `NsTable` placed inside an `NsPaper` or an `NsCard` (every lines editor, the Venta's
    Artículos) framed one thing twice. Inside one it drops its fill, its radius and its lift and
    the enclosing sheet is the edge. **Derived from where the table stands, never asked for**:
    the two sheets carry `ns-paper`/`ns-card` for exactly this, so no page decides it — a
    parameter would be nine call sites agreeing and a tenth forgetting. **A table standing alone
    keeps its own frame**, which is what makes a list page's grid read as a card: `NsPanel`
    paints nothing of its own (`--ns-panel-surface` is the canvas), so the table's sheet is the
    only one there.
  - **A card's ink is `TextPrimary` and its title's glyph steps down to the muted rank** —
    `.ns-card-header .mud-card-header-content .mud-icon-root` → `TextSecondary`: the word is the
    heading and the glyph names what the card is about behind it, so a home full of headings has
    one voice leading each, and the window title's own glyph reads the same var at its own slot
    (`.ns-title-icon-slot`). The rank is *stated* even though the vendor's default resolves the
    same today — a rank nobody wrote is a rank a vendor bump moves in silence.
  - **A card header is ONE ROW: the title at the head, every act end-aligned at the tail** — the
    card's own and the dashboard's door alike, at the same box. The door is drawn by `NsCard`
    from a cascaded `CardDoor`, in the vendor's own header-actions slot and opening on `Main`, the
    one target every door carries ([actions.md](actions.md), [hosts.md](hosts.md)); **nothing is
    positioned absolutely against a card**: the door is a button among the card's own, and it
    wears its own intention's ink.
  - **A card with no title draws no header at all** — `NsCard` renders the box only while
    `Header` (or `Door`) is non-null, so a titleless card never pays for a near-empty row above
    the content for its lone act. The act rides the content instead, as the sibling of whatever
    it acts on inside a horizontal `NsStack` (never inside an `NsFormGrid` — that band wraps,
    which would drop the act under the fields), so the row stays at the fields' own height and
    never owns a row of its own (a job card's delete beside its Receta/Medida line).
  - **A glyph inside the content is a different slot and inherits none of that rank**: a mark
    that reports a state takes the status channel's own tone (`NsIcon Severity`,
    `.mud-icon-root.ns-status-text`), which is what a card says a warning with instead of a block
    of colour behind its words. The tone is restated against `.mud-icon-root`, which paints every
    icon the vendor's action ink at the same specificity.
- **`NsPanel`** is what a page rendering inside a surface uses instead: the same
  Header/Content/Footer anatomy, chromeless — content grows and scrolls, the footer an
  end-aligned action row pinned at the bottom. **One padding source per surface**: each host pads
  its own content.
- **`NsExpander` is a line that is always on screen and the detail behind it** — `Summary`,
  `Detail`, and a chevron that is the whole of its API: no `Open` parameter, because a
  disclosure nobody opened is closed. **The detail opens ABOVE the summary and out of the flow**,
  which is what makes it the control for the strip at the foot of a surface: opening downward
  there would open off the screen, and taking room in the flow would grow the one row a phone can
  least afford. It is a block of its own width — 22rem, growing for content that needs more and
  capped at the viewport — never the strip's, which is whatever the acts beside it leave. **The
  detail stays in the document while it is closed** (hidden by `display:none`, so it leaves the
  accessible tree): what the summary states is derived from what the detail holds — a total off
  the block of figures behind it — and a field torn out of the document takes its registration in
  the surrounding form with it, the trap `NsTabs` answers with `KeepPanelsAlive`. `NsHelp` is the
  opposite answer to the opposite case, and correct there: a paragraph nobody asked for yet is
  not in the document at all.
- **`.ns-disclosure`** (`ns-mud.css`) is the other disclosure: **a field revealed UNDER its line,
  in the form grid's own flow**, for a field that has to be read beside the one above it — where
  the expander's sheet would cover exactly what is being pasted next to it (ARCA's Clave privada
  (PEM) under its certificate). It is not a component: the line is an ordinary `Inline` `NsButton`
  carrying its own word with `Expanded` — the state a disclosure trigger owes is `aria-expanded`,
  which that parameter is — beside an `@if` that renders the field's `NsFormGridItem`, and the
  class is only the chevron's two states and the sentence case a face that is read rather than
  pressed needs. Choose it over `NsExpander` wherever the detail is a field in a form and nothing
  above the line derives from what it holds; closing it withdraws the field from the document, so
  the screen clears what was typed rather than let an invisible value ride the next save.
- **`.ns-footer-action-row`** (`ns-mud.css`) is the opt-in for a footer whose own group of acts
  does not fit beside a summary it carries: under the narrow-footer breakpoint it drops that
  group to a line of its own rather than a shared label shortening or a mid-group wrap
  (WorkOrderPage). Opt-in, so a footer that never wraps its own group (Nueva Venta's one-line
  footer) is untouched.
- **`NsPaper` is the bare sheet a SECTION sits on** — `Outlined`, `Elevation`, `Class`, and no
  Header/Content/Footer anatomy: what a lines editor or a step's optional block rests on where a
  card would claim the surface. It composes its `Class` over its own base like every other
  arranger.
- **One overflow owner per surface** — `NsTable`'s container, `NsPanel`'s content, a grown
  `NsCard`'s content, an `NsStack` with `Scroll` — and the shell around them declares none
  (cases).
- **A scroll box INSIDE a surface wears the house's own bar** (`.ns-scroll-thin`): thin, the
  theme's line colour, no track. The shell's own scrollbars stay the platform's.

---

## A button's fill

What each `As` means is [actions.md](actions.md); this is how it is painted.

**A filled button is never flat.** `Default` and `Important` are one shape and one shadow and
differ only in the wash — grey against `--ns-accent-soft`, mixed from whatever accent the tenant
picked and never a stored hex ([branding.md](branding.md)) — and the one full-strength accent a
screen may spend is `Main`'s, one per screen. The vendor has no soft-accent variant, so
`NsButton`/`NsLink` paint `Important` with the `ns-important` class over the vendor's filled one;
a page still names only an `As`. A command and a link at that intention must look identical,
which is why both write the same class rather than each deciding.

**A REFUSED act is not a rank, and nothing about it is the accent.** Built from the accent's own
disabled tone under the vendor's fill, it read as `Important` — and louder than the live
`Default` beside it. Both halves are the scheme's instead, and neither can reach a pick: the ink
is `BrandTheme.TextDisabled`, the ladder's third rung under `TextPrimary` and `TextSecondary` and
the only one with a CEILING as well as a floor — legible, and visibly under the muted rank
([branding.md](branding.md)) — and the fill is `--ns-inert-soft`, the scheme's own INK at half
the vendor's strength, mixed against transparent like `--ns-accent-soft` so one value reads on a
card, a raised sheet and the canvas alike. The vendor's `box-shadow: none` on a disabled filled
button is left standing and wanted: *a filled button is never flat* separates two LIVE ranks,
and a refused one losing its lift is a third signal saying what the other two say. The rule
aims at the filled face alone — the flat rung and every icon button carry no fill to quiet, and
are refused by the ink they share with it.

**A filled button with no visible text is a SQUARE** at the house's icon-action box
(`ns-square`) — `min-width: 0`, equal padding, the house's corner radius, its own fill and
shadow — never the vendor's 64px-floored rectangle. No number is written at either end: the
padding is the vendor's own icon-button padding and the glyph is the app's own `Md` step, so the
square and the flat glyph beside it come out the same size by construction. **Whether there is
text at all is `Breakpoint`'s word alone** (`NsBreakpoint`: `Always`, `Xs`…`Xxl`, `Never`), never
a second flag on the control — so the same square is reached by `Never` outright and by a
container query taking the label away, and the `ns-collapse-*` blocks restate that geometry
rather than inventing a second one. It carries `flex-shrink: 0`: a squeezed square is a
rectangle again, and a toolbar's search box is the thing on that row that gives — a comfort
width (`.ns-toolbar-search`), not a content one, and once it is spent the row wraps rather than
squeezing a worded button into two lines ([intentional-ui.md](../intentional-ui.md), the page
name never gives way). The flat rung has no fill to shape, so its wordless face stays the bare
glyph.

---

## Text and image chrome

- `NsText` has its own `As` (`NsTextAs`: `Body` default, `Title` = the h6 treatment) — a
  separate enum from `NsAs`.
- `NsRemoteImage` and `NsImage` (Assets) both carry `As` (reusing `NsAs`, never a second enum):
  `Inline` (default) inherits the surrounding colour, `Main` maps to `.text-primary`. Tinting
  an image is that parameter, never a class or a `style=` — including on the row *around* the
  image, which would tint every sibling with it.
- `NsTooltip` takes `Text` and wraps what it explains — **detail on demand, never the only
  place a fact lives**. A phone has no hover, so what rides here is the long form of something
  already rendered, and **a fact the eye needs in order to choose is drawn, never hovered**: the
  product picker's option is the article alone, and the store hangs under it at the muted rank,
  on a line of its own, only where more than one source of that article survives — what tells
  two rows apart is on the row. A tooltip carrying the only copy of a value is the smell, and no
  screen in the tree wears one.
- `NsMeter` is the bar for a reading against an allowance: `Value` and `Max` in the caller's own
  units — never a pre-computed percentage — plus `As` (`NsAs`, `Main` default, `Danger` for
  an allowance already spent). The percentage is worked out inside and clamped, so a reading
  past the ceiling draws a full bar rather than overflowing it, and the numbers the bar is drawn
  from are still written beside it: a bar is never the only place a count lives.

---

## A state's tone: one channel, three forms

**`NsStatusText` paints the word, `NsStatusDot` paints a circle where no word fits** — an
agenda block's right edge, a cell too narrow for a sentence — **and `NsIcon Severity` paints a
GLYPH** beside the sentence that says what is wrong, which is how a card carries a warning
without a block of colour behind its words. All three take `Severity` (`NsSeverity?`) and all
three resolve it through one `--ns-status-color` class, so **a screen never names a colour and
never writes a per-status class**; a page that composes its own `"ns-status-text ns-status-*"`
string is the palette-in-a-screen these exist to end. `NsStatusText` takes `NsText`'s own
`Size`, so a state is worn at whatever scale the datum is set in — a card's figure and a grid
cell's word alike. **On the glyph, null is the ordinary icon and not the neutral paint** the dot
takes: an icon that reports no state is the ink of whatever it names.

**Colour goes on the datum, never on the container.** A card is never tinted as a whole — a
permanently coloured tile is an alarm nobody sees by Thursday — so what carries the tone is the
number, the name or the dot beside it, and only while it has something to say: *Vencidas* reads
`Error` above zero and plain at zero. **A figure reading zero drops to the muted rank**
(`NsText Secondary`), which is what lets the figure that is not zero rise without anything being
painted at all.

**A card's figure is BODY-SIZED and leads by INK.** `Lg` is `Typo.h6` — the card title's very
treatment — so a figure set in it weighs as much as the heading above it while saying less, and a
card that draws its hero at one scale and its rows at another spends size on a hierarchy the
labels already carry. So everything a card prints — money, counts, hours, days — is the body's
size, and what makes one figure stand out is the primary/secondary pair above: plain ink for the
one worth looking at, the muted rank for the one with nothing to say. **A total row may be
emphasized by weight, never by a bigger size.** Quantified over the tree by
`CardFigureScaleTests`, which reads every `Size` a contributed card writes; a card's own title
keeps `As="Title"`, which is a role and not a scale.

- **Five readings, and they are the whole vocabulary**: info, success, warning, error, and the
  **neutral** — `Severity = null`, a state that is neither an alert nor an outcome. The neutral
  is a *paint*, the theme's **muted** ink leaned a tenth into the scheme's own (`NeutralLean`),
  never an absence: a filled circle cannot be colourless, and both forms take the same one so a
  state reads the same on either surface. Not the ink below it — the refused rung is held UNDER
  3:1 on purpose ([branding.md](branding.md)), so a live state painted in it reads as a control
  nobody may touch; and not the plain muted rank either, which under the pointer reads 4.26:1 in
  light. What keeps the neutral quieter than a toned state beside it is that it carries no hue,
  not that it reads lower: on the light card the neutral reads 5.65:1 and the `Warning` word
  5.49:1.
  **There is no plain-ink sixth**: a state wears one of the five or it is not in this channel.
- **A toned reading is its severity LEANED INTO the scheme's ink, not the severity itself.** A
  severity constant is a *fill* — picked so near-black or white can be read on top of it
  ([branding.md](branding.md)) — and a fill set as a word on a card clears nothing: light
  `Warning` read 3.32:1 and light `Info` 3.70:1 before the lean. So `--ns-status-*` mixes its
  severity into `--mud-palette-text-primary` at `BrandTone.ToneLean`, one declaration serving
  both schemes because the ink it leans into flips with the scheme. The fraction is the largest
  that still clears AA on every ground a row gives a word — the card, the raised sheet, the
  severity's own washed row, that row **under the pointer**, and a row with no wash under the
  pointer, which is the worst of them because `NsTable` hovers every row it draws
  (`BrandThemeContrastTests` grades the whole cross product; the floor is light `Warning` at
  4.59:1). The dot takes the same token, a circle owing 3:1 that a lean toward the ink only
  raises. Re-tuning the constants instead would repaint every alert, toast, chip and row wash to
  fix a word.
- **A washed row keeps its tone under the pointer.** The vendor answers a hover by replacing the
  row's background from rules no single class outranks, which blanks the CONDITION channel on the
  one row being read and drops the word onto a neutral tint; `.ns-row-*` hands
  `--mud-palette-table-hover` its own severity deepened to `BrandTone.RowWashPointed` instead, so
  the pointer deepens the wash rather than erasing it. Cascade, not specificity — the same move
  `--ns-status-color` makes against the vendor's text colour.
- **Which form the CONDITION takes is decided by the row's own padding.** A grid cell brings room
  a tint can live in; a stacked row flush inside a card does not, and five full-bleed tinted bands
  read as a tinted card — colour on the container, which nsail#842 refused. So a flush list row
  carries the condition as a 3px start-edge mark instead (`.ns-row-mark`, `OpticalJobLine`), while
  a list row that takes padding and a rounded edge of its own keeps the tint
  (`PartyWorkOrdersCard`: four separated bands, and only the ones whose condition fires). Both
  forms are the same three severity classes, so the vocabulary is one either way.
- **The channel is a LIST's, and its two edges are edges rather than gaps.** A *ficha* shows one
  document and has no column to scan, so its state is a labelled read-only field — the house
  pattern for every document page (`WorkOrderPage`, `PurchaseOrderPage`), read by its label in a
  walk, and a toned word there would be colour on a form field. And where the state is a SEGMENT
  of a joined sentence — a dashboard card's `N° 14 · Encargado · vence hoy` line — it stays in
  that sentence: one `NsText` is one `<p>`, a status cannot nest inside it, and a clause painted on
  its own reads as a highlight rather than as a state. One rule still answers what the tone IS
  wherever a list prints it (`WorkOrderSeverity.OfStatus`), which is what keeps the edges from
  becoming a second vocabulary.
- **A state in flight wears the neutral.** Work still moving is neither an alert nor an outcome,
  so it reads with history rather than beside it: on the OT grid an Encargado order still inside
  its promise and Anulado carry the same ink. That lost distinction is deliberate — the word
  itself names the state, and the colour is spent on the state(s) worth looking at: on a purchase
  order that is the one state, Recibido. Where a CONDITION widens that budget, only a status the
  condition cannot reach is neutral on the status alone: on a work order Listo — the state that
  needs someone to act — takes Success, Entregado sits with the rest of history in the neutral,
  and the two date-derived conditions the row wash already paints (vencido, por vencer) earn the
  same word too, so the channel reads four things there rather than one.
- **Theme severity variables only, never a hex** — the same rule the row wash keeps.
- **Colour is never the only signal.** `NsStatusDot.Title` is the state in words and is
  required: it renders as `role="img"` + `aria-label` + `title`, and the surface around the dot
  still says the state in text of its own. A dot that only has a tooltip is colour alone, which
  a printout and a colour-blind reader both lose.
- The dot is **`flex: 0 0 auto`**, so a title sharing its row ellipsizes against its own
  `min-width: 0` instead of squeezing the dot out of the block.

Which channel a screen is in — state versus condition — is the core doc's
[semaphore rule](../intentional-ui.md); the grid's own side of it is [hosts.md](hosts.md).

---

## Where a class may live

- `NsText` renders text with the `NsSize` scale — **no vendor typography in pages**. Spacing
  utilities (`pa-*`, `ma-*`, …) are fine anywhere; custom utilities MudBlazor lacks live in
  `ns-mud.css` (`min-h-0`, `h-full`, `flex-1`, …) — add there, not inline.
- **A component that hands a caller's fragment to the vendor gives it a box of its own first.**
  `.ns-tabs-actions` is that box for `NsTabs.Actions` ([hosts.md](hosts.md)): the vendor's tab
  bar stretches its own children to the strip's height, so the act inside it is centred and
  inset there — one rule, in the Stack, rather than a margin every page that
  passes `<Actions>` would have to repeat and could disagree on.
- Structural rules reaching inside a vendor component are scoped under an `ns-*` class the
  component applies to itself (`.ns-table` + `NsTable.GetClasses()`). Compose class strings with
  `NsClassBuilder` (base classes + the user `Class` parameter).
- **A thread's two sides are `.ns-bubble` + `.ns-bubble-ours`/`.ns-bubble-theirs`**, on the
  `NsPaper` each message sits on: the edge, the corner tail and the tone are one rule each in
  `ns-mud.css`, and the page names the side and lays out nothing (`TicketPage`,
  `AssistantPage`). What was said keeps its own line breaks there and a word the speaker did
  not choose (an id, a run of json) breaks inside the bubble rather than push past it. Ours is
  `--ns-accent-soft` — the very wash `NsAs.Important` wears, so the install's own side carries
  the tenant's accent at the one strength the house spends — and theirs is `--ns-bubble-soft`,
  the scheme's own ink at the same sixth, which is a tone and not a colour and so says nothing
  about the contact. Both mixed against transparent, for `--ns-accent-soft`'s own reason. A
  third class, `.ns-bubble-note`, stands on neither side: it stretches the row instead of
  aligning to an edge, wears the warning wash since a note is for the staff and never for the
  person it is about, and grows no tail — it did not travel a road.
- **A handful of facts said as a list is a real `<ul class="ns-bullets">`**, so they are
  announced as a list and not as one sentence that wrapped — the browser's own marker kept, its
  indent and margin corrected to the one the house's markdown lists wear, and the gap between
  items the surrounding `NsStack`'s. `.ns-list-editor` is the other `<ul>` in the house and the
  opposite reset: an editor's rows are not bullets.

---

## Container queries

A media query — and every `d-{breakpoint}-*` utility, `MudTable.Breakpoint` and
`IBrowserViewportService` — measures the *viewport*, and a 960px aside on a 1920px screen is
narrow. The fix is CSS container queries: `NsPanel` and `NsCard` carry `ns-container`
(`container-type: inline-size`), and the `d-c-{breakpoint}-{value}` utilities mirror MudBlazor's
`d-*` at MudBlazor's own breakpoints against the container's width. Same mobile-first shape:
`d-c-none d-c-sm-inline` is "hidden, shown from `sm` up". **Only the values in use exist** — add
one when a control needs it (cases).

**Every surface that leaves the page's tree declares its own container.** A container query with
no `container-type` ancestor matches *nothing*, and `d-c-none` then wins at every width, hiding a
collapsed label **forever**. A dialog is portaled out of the layout and a modal is a fixed box
over it, so both content boxes carry `ns-container` themselves. **The aside declares nothing on
purpose**: what it hosts is a routed page composing `NsPanel` (cases).

### The window, where the window is what is being asked about

The exception the rule above allows, and its whole extent: **whether the nav drawer is docked or
behind the hamburger is a window fact** (`MudDrawer`'s `Breakpoint.Md`, `NsMainLayout`), so
anything that trades places with the drawer flips on that same edge and on no other — chrome that
stands in for it while it is hidden (the title row's hamburger), and content that moves *into* it while
it is (the guide's index rail). Two helpers say it, and a window decision is written with them or
it is not written:

| Helper | Class pair | Reads |
|---|---|---|
| `NsResponsive.ShownFromWindow(NsSize)` | `d-none d-{bp}-flex` | gone below the width, shown from it up |
| `NsResponsive.ShownBelowWindow(NsSize)` | `d-flex d-{bp}-none` | shown below the width, gone from it up |

They ride MudBlazor's own `d-{bp}-*` utilities, whose media queries ARE the vendor breakpoints the
drawer docks at, so nothing here restates a number. **The pair is one decision written twice** —
used together on the two faces of one slot, so neither face is ever on screen with the other. What
CSS cannot decide — a vendor *parameter*, `NsDrawer` picking its variant — still measures with
`IBrowserViewportService`, and that is the only reason left to.

**One media query in `ns-mud.css` is not a design measuring the window but a rule staying out of
the vendor's way**: the stacked-by-container block for a table with a row open is wrapped in
`@media (min-width: 960px)` because below that width MudBlazor's own stack is already the answer,
and two regimes answering for one table is what the wrapper prevents (cases). It names the
vendor's own number for the same reason the helpers above do.
