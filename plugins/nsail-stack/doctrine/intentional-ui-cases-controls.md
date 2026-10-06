# Intentional UI — cases: controls

Part of [intentional-ui-cases.md](intentional-ui-cases.md): the why behind the rules on acts,
fields, lookups, files and contributed actions. **Nothing here is a rule** — the rules are
[intentional-ui.md](intentional-ui.md) and the [`ui/` shelf](ui/README.md).

---

# `As` is the semantic pivot

## Why derivation lives in wrapping components, never on the primitives

A parameter valid only in *some* combinations (`Href` xor a page type, `OnClick` xor an
`ActionItem`) means a runtime guard for the invalid ones and a primitive that only grows — tried
and reverted twice (`NsLink.Page=`, `NsAction.For=`), each with a `throw` for the disallowed
combination. A wrapping component makes the invalid states unrepresentable: `NsAction` has no
`OnClick` to collide with, `NsPageLink` no `Href`, so there is nothing to validate. Also
rejected: `RenderFragment`-returning helper methods — they forced `NsPage`/`NsPartial` into the
Mud project and hand-rolled `RenderTreeBuilder` calls for what a plain `.razor` component gives
free.

## Why `NsSubmit`/`NsClose` stay their own components

`NsSubmit` reads `[CascadingParameter(Name = "FormTracked")]`, which cascades *from* `NsForm`
downward — a page is never a descendant of its own `<NsForm>`, so only a component nested inside
the form can read it. It also self-subscribes to `Surface.StateChanged`, because mutating
`Surface.HasChanges` re-renders nothing by itself. `NsClose` needs neither, but with one of the
pair needing per-instance cascading state a shared primitive cannot reach, both stay their own
components.

## Why `GetTitle()` is a method — and why nothing near `NsTextAs` is named `Title`

A property — or a bare method — named `Title` on `NsPartial` would sit in scope at every call
site and silently win simple-name resolution over the statically imported `NsTextAs.Title`: a
string-vs-enum shadowing bug that fails with a confusing type error. Hence
`GetTitle()`/`GetAction()`, and the rule that no identifier on a composing type may collide with
the statically imported enums. The same hazard is why `NsRemoteImage` reuses `NsAs` for its
colour rather than founding an `NsImageAs`.

## The exit, and the dialog that ends in Cerrar

`NsTitleBar` renders the X (and draws none on main); `NsOpenDialog` renders the same X in a
dialog's title row and enables Escape. `BackdropClick` stays off on purpose — a misclick outside
must not discard a half-filled form. **A dialog with no acts still ends in an explicit Cerrar**:
the X covers escape, not closure — an informative dialog whose body just stops reads as broken.

**Both ways out answer the form inside.** With `BackdropClick` off, the X and Escape are a
dialog's only exits, and each must refuse on the form's terms — otherwise the X stays live over
a footer Cancelar that went grey, and a press mid-save closes a surface whose save is still
running. The X's word travels up through the surface and back down into the title row
(`SurfaceContext.FormRefuses` on its own `RefusalChanged`, `NsDialogExit`); Escape — the vendor's
own handler, which never reaches `Surface.Close()` — is withdrawn for the length of the save by
flipping `CloseOnEscapeKey` on the live dialog. **Escape answers the save, not the standing
refusal, on purpose**: the X greys for a merely `Disabled` form too, and a dialog has no backdrop
click and no Back, so withdrawing Escape for a refusal with no end would leave no exit at all.
Neither exit asks the unsaved-changes question: a dialog closes through its host, not a
navigation, so both are silent about an abandoned edit. The routed surfaces' own Escape
(`NsCloseOnEscape`) has the same gap on a surface that still has Back, and is left as is.

## Main acts and the footer

The footer rule in its crispest form: facturar is important — big, at the bottom, with text —
but not `Main`. On a document's page the *next* transition IS the dominant next action, so
making it the hero applies the emphasis rule rather than bending it.

## A field nobody will type is not a field

The case was the cotización: the exchange rate reads from Banco Nación through a service, never
a form field. Corollary: stock already lives in one currency, so a per-document currency/rate
pair is noise wherever the document cannot disagree with it. The same holds for the figure:
editable fields read "48.600,00 ARS" beside totals reading "48.600,00"; the code is never shown
when there is only one currency, so it rides only an amount in a currency other than the
install's own.

## One arrival, one loading — and the handoff that keeps it true

A user tolerates one loading; two is too many. A double loading reads as a broken app, whatever
the profiler says.

**What the content area draws while a read is in flight is NOTHING** — plus the title bar's
spinner, which every `Runner` raises for the length of a read and which is therefore already on
screen. That is the whole loading state, and it is why `NsLoad` has three states and no fourth:
a skeleton or an in-place spinner would be the second loading, and content→placeholder→content
is exactly the flash the rule above forbids.

**The paged grid is the one exception, and it is a concession, not a pattern.** A `ServerData`
grid draws what its own query returned, so the question cannot leave `OnQuery` and the grid has
to be mounted in order to ask: its pager reads 0-0 and its own in-line progress bar runs for the
length of that first query. `ui/hosts.md` states the limit and `NsTable` carries it. What a
region still takes off a paged list is the state AROUND the read — a read that failed draws the
sender's reason and a Retry where the grid went, instead of the table's own "no records" standing
in for an answer about the network.

**An empty state is an answer about data, and so is a prompt.** "Nothing here", "pick an
agenda", "no records", a zero — each is a claim about something that came back, so a screen
that makes it before its read answers tells the user there is nothing and then takes it back.
The states are the region's: not read yet (nothing), read (the data, or the honest empty line),
could not be read (the sender's reason and a Retry). **Resolving WHICH record to read is part of
the read** — a screen that sends a lookup to pick its own subject is still "not read yet" while
that lookup is in flight, and may not draw the prompt that belongs to a lookup which came back
with none. The one state that is not a wait is a read that never happened: a control with
nothing to ask — an unset id — stands there empty rather than being withheld for good, and says
which of the two it is.

**A control is not a region, and has two of those states rather than three.** A field that gates
itself on its own read — `StoreSelect`, `TenderMethodSelect`, `ChannelSelect` — can be absent or
present, with nowhere to say "could not be read" and no Retry to offer, so a refused lookup
leaves it absent. That is the price of gating a field without a region around it; what it must
never do is draw itself over a list it does not have yet.

**The mechanism.** A client that re-asks what the prerender resolved returns an incomplete task
from the render path, and on the authorization path that makes `AuthorizeRouteView` render its
empty `Authorizing` fragment — the prerendered screen torn out and rebuilt a second later, which
is exactly the flash. So the session travels `PersistentComponentState` (key `NSail.Session`):
the prerender persists the `Session` **and the effective policies** — one payload, because a
gated screen cannot render holding half of it — and `IamAuthenticationStateProvider` adopts them
**synchronously** before the first client render. An await there costs precisely the render the
handoff exists to save.

**The registration trap**: a *service* registering a persist callback must name the render mode,
and it is `InteractiveWebAssembly` alone — an interactive-server client *is* the prerendering
scope and has nothing to be handed. A *component* registering one names no mode and must not:
.NET reads the mode off the render tree only for a callback whose target is the component
itself — a lambda throws.

**The card half** — `NsPartial.Handoff`, where a card reads, the started-subject guard — is
[ui/hosts.md](ui/hosts.md) (`NsDashboard`). The defect it answers: two round trips per card per
page load, with the card's default state on screen in between. A subject card answers the null
subject in `OnParametersSetAsync` rather than inside the read, so the guard is reachable for the
whole round trip. A card that defers has nothing to hand over and that is not a bug —
`TodayAgendaCard` renders nothing until its read answers, so it prerenders nothing; the
connection cards adopt server state the client would re-ask the same server for milliseconds
later, which is not staleness. Pinned by `CardHandoffTests`, `PartyContactCardOneReadTests`,
`DashboardHandoffTests`.

## Icons are chosen, never defaulted

The work-order page proved it: three transitions wearing the same check are three verbs the
reader cannot tell apart.

---

# Fields

## Why `MailAddressField`'s label always floats

The field needs a label long enough to say what the address is for ("Correo de envío y
recepción") beside a domain the field prints rather than types, and **both texts must read
together at rest, empty and unfocused** — a fresh "Nueva Organización" is where it matters most.
Rejected: `visibility: hidden` on the resting suffix (`ns-mud.css`, `.ns-mail-address-field ...
.mud-input-adornment-text`), which kept the label from overlapping by hiding the second text —
legible one at a time, never both. That rule and its wrapper class were removed rather than
tuned: hiding either text can never satisfy "both". The fix is `NsTextField.ShrinkLabel`
([ui/fields.md](ui/fields.md), *The label floats on the value*): `MailAddressField` floats its
label unconditionally — the mechanism `NsSelect`/`NsAutocomplete`/`NsMultiSelect` already use for
a value the field resolves rather than types — so the label never rests where the permanent
suffix prints.

**The test trap**: a rule that shrinks the suffix's box toward zero height passes Playwright's
`Not.ToBeVisible` (or a naive `ToBeVisible`) for the wrong reason — invisible for lack of room,
not undrawn. `MailAddressFieldOverlapTests` asserts the suffix's own bounding box (`Height > 8`)
alongside the non-overlap check, so a regression squeezing the adornment to nothing fails on the
measurement.

---

# Combos, lookups, filters

## Why `MinCharacters` is 0 at the vendor and the threshold lives in `NsAutocomplete`

Measured against MudBlazor 9.10.0: `MinCharacters` gates **the popover**, not just the query —
below it `OpenMenuAsync` returns without opening, so a `Lazy` field set to 1 could never show the
create entry on focus. The vendor is told 0 and `Search` returns nothing for an empty `Lazy` box
instead. Two more measured facts, both load-bearing for the permanent create entry:
`AfterItemsTemplate` renders **only** when the search returned items, `NoItemsTemplate`
**only** when it returned none, and never both — one fragment passed to both is what makes the
entry permanent rather than duplicated.

## Why the open list is floored at the field and capped at a measure

**The popover.** Measured in Chromium at 1600px on an OT: `RelativeWidth` defaults to
`DropdownWidth.Relative`, whose popover script writes `max-width` = the anchor's own width
**inline** on every open — the one width case MudBlazor 9.10.0 ships CSS for. In a line editor
the anchor is a table cell, so an option reading "CR-150 - Orgánico CR-39 1.50 (1,00)" — 353px
of text — came out three lines tall in a 191px popover, and the closed field read "CR-150 -".

`Adaptive` writes `min-width` instead and `max-width: none`: floored at the field, free above
it — one word at the tree's one `MudAutocomplete`, so it answers for **every** lookup and no
caller says anything. What bounds it is a cap on the option's own **text box**, never on the
list — the item's width stays the popover's, so the hover wash and the vendor's gutters span
whatever a wider anchor asks for (a header lookup's popover measures 516px either way). A
percentage cap could not work: the popover is shrink-to-fit around the list, and a percentage of
the box a child is inside of resolves cyclically and is dropped from that pass. The measure is
the aside's own content width, and a reading past it **wraps** rather than eliding — the whole
option still on screen, which is what a cap buys over a clip.

**The cell is a floor.** An auto-layout table sizes a column from its cells' min-content and a
vendor input contributes none, so the Artículo column took whatever the others left: 174px on a
1366px window, 100px in a 700px container, where the box showed 14px of a 353px value. The cell
asks for the grow basis — also the width at which the box inside measures 106px, a SKU's own
measure. **Only a GROWING lookup's cell** asks, read off the class `Grow` renders: every other
cell in the row holds something legible from its first characters, and `Grow` is what a lookup
declares when its value is a composed reading whose head alone names nothing. That is the
model's answer, not the markup's: an Alícuota is `TaxRateRef.DisplayName` ("IVA 21%") and a
Tercero a plain `PartyRef.DisplayName`, both legible from their heads; a Cuenta is
`AccountRef.DisplayName` = `Code — Name`, the shape this floor exists for, so `AccountSelect`
declares its own `Grow` and forwards it to the `NsAutocomplete` underneath. A fixed-width table
charges for it — the four neighbours narrow by ~3% at 1600px, measured, and nothing clips.

**The ROW answers whether it can be paid.** A `min-width` is a DEMAND: it raises the table's
min-content, so where the row has no surplus the minimum is added to the table rather than
redistributed — the table outgrows its container and the overflow is cut off by `NsPanel`'s
scrolling content box, whose `overflow-y` makes its `overflow-x` hidden. Measured on Nuevo
Comprobante: demanding the basis in its eight-column row asks 856px of an 806px table at 1152px,
and what lands past `NsPanel`'s edge is its own Confirmar and Cancelar — which is why the rule
reads the growing field's class rather than the presence of a lookup. That row carries one
floor, at Producto; its Alícuota is a bare `NsAutocomplete` declaring nothing.

**So the cell demands the basis from `md` up, and the container only says whether that is
safe.** `md` is where it is always affordable: at the smallest container past it (968px) the
widest line editor in the tree asks 856px of a 934px table and clears it by 78px — the width
`LookupListWidthTests` holds both lookup rows to, since there the demand is narrowest and a row
that could not pay loses its own Confirmar. Below `md` one number cannot tell rows apart — at a
712px container Nuevo Asiento's row has 103px of slack while Nuevo Comprobante's is 52px past
its table.

**Below `md` the open row stacks; a width preference was rejected.** A `width` preference moves
the column's max-content, never the table's min-content, so the row grants the basis out of its
surplus: Cuenta went from a 35px box to 106px against a 76px account code, Producto stayed put,
and a two-column row was pulled back from 305px to 192. Not enough: in a 688px aside Precio
unitario sat in a 56px box against 110px of "125.430,50" with every column already at its share.
**An edit row has to fit** — hiding is per container but an input never hides (ui/hosts.md), so
the open row is the widest thing a grid draws — and what fits under `md` is the stack
([cases: hosts](intentional-ui-cases-hosts.md), *The open row stacks where its container cannot
seat it*).

**Reach of the floor** (column mode, `md` and up): the product lookups in `SaleLinesEditor`,
`PurchaseOrderLinesEditor`, `VoucherLinesEditor` and `PriceRulesEditor`, the `RoleLookup` in
`AdminStep`, the `ChargeItemDefinitionLookup` in `AuthorizeWorkOrderPage`, and the `AccountId`
cell in `EntryLinesEditor` and `OpeningEntriesStep` through `AccountSelect`'s own `Grow`. A lookup
in a page filter stands in no cell and a read-only row renders text, so neither is reached;
`EntryLinesEditor`'s `PartyId` declares nothing — a Tercero reads from its head.

**The container picks the word, the row pays the bill.** Both `AccountId` cells sit in a
**712px** container at 1024px — `OpeningEntriesStep` under `OnboardingLayout` measures the same
as the shell's (measured; the wizard's container does not clear the gate). The wizard's cell
reads because of its ROW: two columns leave Cuenta 305px whatever anyone declares, while the line
editor's five leave it 121px and a 35px box. `Grow` answers WHICH cell asks; the ROW answers
WHETHER it can be paid; the container only says whether a minimum is safe to demand there. None
of them is the viewport's: the same `EntryLinesEditor` in a 720px aside or an `NsDialog` reads
its own container.

**Once the table stacks, none of these questions is asked.** A stacked cell has no column: it is
a block-level flex box in the card, and the whole line is already the widest a field can be
(at 375px the box once read "O…" in 192px of a 277px line, beside a `Descripción` that took the
whole line). Nothing asks a width below `md`, so nothing under the stacked block needs taking
back.

## Why the cascade crosses the popover and the create-link resolves once, at the leaf

`MudAutocomplete` renders its dropdown templates through the popover provider — a sibling of the
router — so a link built inside a template sees none of the `CascadingValue`s the hosting page
provided. Resolving the address *before* it crossed (`NsLookupBase` handing a finished href) was
rejected: `NsLink` resolves what it is given anyway, so every create entry was resolved twice,
correct only because a rooted main-surface resolution happens to be the identity.

The cascade crosses with the fragment instead. `NsAutocomplete` holds the hosting surface as a
cascading parameter of its own, and a `CascadingValue` provides where it RENDERS, not where it
was written — so the create entry opens with one, and the `NsLink` inside it sees the surface
the lookup stands on. The lookup hands down the raw route (`CreateRoute`) and the target
(`CreateTarget`); the leaf resolves them exactly once. A field with no cascade provides null and
`NsLink` falls back to `RootSurface`, the main surface's own answer — the fallback every
portal-rendered link relies on ([cases: surfaces](intentional-ui-cases-surfaces.md), *Why a
target is never dropped for want of a cascade*).

## Why the create entry restates its own geometry

The vendor renders the seam the entry lives in as a bare padded div, so the entry restates a list
item's own type and geometry in `ns-mud.css`: never smaller than the rows above it — an
actionable last entry outweighing the passive rows is the floor. Rejected: an entry only on "no
results" (strands the user whose match is merely absent), and a `+` beside the field (chrome the
dropdown already carries).

## The one case where creation does leave the dropdown

On a fresh install Cobrar drew a Forma with no rows and nothing else, so the operator learned
neither that a tender method exists nor where one is made. **The in-dropdown rule holds for a set
that has rows** — the entry is the dropdown's last item, the field's edge stays the lupa's or the
arrow's. For an EMPTY set the dropdown is a place nobody opens: the affordance is discoverable
only by opening a control that visibly offers nothing.

So `NsMissing<TItem>` draws the fact and the act **under** the field, in the under-field zone the
hint and the refusal share (`ns-field-missing`, `ns-mud.css`), and only while the set is
genuinely empty — a picker with rows spends no pixel on it, nor does one still reading. It is not
a second create affordance: both sentences derive from the same two keys the dropdown entry
composes (`Metadata.KeyFor(typeof(TItem))` and `Common.CreateNew`), so a concept nobody
translated draws nothing rather than a sentence with a hole. Rejected: a page-level alert (a
fourth placement, one per screen instead of one per picker) and a helper line with no link (says
which one, does not open it).

---

# Files

## The logo tab that proved one field is not two

On the Branding Logo tab, a field that rendered as a list because a second binding happened to
be supplied was one component wearing two shapes — a logo tab, where the tabs ARE the variant
split, read as a multi-file control. Hence: same parameters, and no parameter turns
`NsFileUpload` into `NsMultiFileUpload`.

---

# Contributed actions

## The toolbar's one icon size

The `Md` box's unconstrained glyph measures 1.5rem in MudBlazor 9.10, and the grid's own glyphs
are the card size too. A passive label must not outweigh the controls that act — a status chip
reading larger than the icons beside it is the hierarchy inverted, so the icon is what rises.
