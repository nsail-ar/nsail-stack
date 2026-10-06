# Intentional UI

How UI is designed in NSail: components expose user intent, never UI-framework concepts, so a
screen reads like application meaning, not visual configuration.

**This file holds the rules a screen is built by; every rule here is binding.** Two companions
hold what building a screen does not need:

- **Why a rule is what it is** — rejected designs, measured vendor facts, the mechanisms that
  make a rule hold — is [intentional-ui-cases.md](intentional-ui-cases.md), under the same
  section names; a rule marked *(cases)* has its story there. Read it before proposing to
  *change* a rule, never before building a screen.
- **A component's API** is the [`ui/` shelf](ui/README.md), one page per thing you place:
  [fields](ui/fields.md), [actions](ui/actions.md), [forms](ui/forms.md), [hosts](ui/hosts.md),
  [styling](ui/styling.md), [navigation](ui/navigation.md), [menus](ui/menus.md),
  [wizard](ui/wizard.md), [settings](ui/settings.md), [guide](ui/guide.md),
  [branding](ui/branding.md), [localization](ui/localization.md), [surfaces](ui/surfaces.md).
  Read one when you place the thing it describes, not before.

---

## Core rule

A component API describes what the control means, what the user is trying to do and what kind
of data is edited or shown — never how it looks or which vendor variant it uses. Design around
intention, not visual variants (Primary, Filled, Outlined, Color, Variant):

```razor
<NsSubmit />
<NsTextField @bind-Value="model.Email" />
<NsMoneyField @bind-Value="model.Price" />
```

Bad (vendor leakage): `<MudButton Variant="Filled" Color="Primary" />`. Many field components
carry the intent in the type name (`NsMoneyField`, `NsDateField`, `NsPasswordField`).

---

## `As` is the semantic pivot

`As` is a semantic declaration, not a styling knob — visual emphasis derives from it, never the
other way around. **`As` values may differ only in chrome (variant, color, type); a value that
needs its own parameter or wiring is not an `As`, it is a component.**

**The golden rule: if it navigates it is `NsLink` (a real `<a>`); if it executes it is an
action component (`NsAction`/`NsSubmit`/`NsClose`). No gray zone: actions have no `Href`,
`NsLink` has no `OnClick`.**

Six components share the pivot — `NsButton`, `NsAction`, `NsLink`, `NsPageLink`, `NsSubmit`,
`NsClose`; their `As` values are in [ui/actions.md](ui/actions.md).

- **The primitives (`NsButton`, `NsLink`) stay pure chrome; derivation lives in a wrapping
  component (`NsAction`, `NsPageLink`), never as extra parameters on the primitive.**
  `NsSubmit`/`NsClose` stay their own components (cases). A primitive may still take a word
  about ITSELF — `Disabled`, `Expanded`, `Submits` — never a source to derive its click, icon or
  label from.
- **An act drawn under an `NsForm` is a way IN unless it declares it changes nothing the form
  holds**, so a form that refuses writes withholds it and no screen reads the cascade itself
  (`ActionItem.Writes`, [ui/actions.md](ui/actions.md)). **A primitive carries no such word and
  is never WITHHELD**: a bare `NsButton` is wired by the screen that drew it, and a link goes
  somewhere. It is greyed, though — by a form the screen disabled, and by a form that is merely
  saving only where it declared `Submits`, which a link never does.
- **Colour on an image is an `As`, not a class or a style.** `Class="text-primary"` or a
  `style=` tinting an image is the smell this pivot prevents ([ui/styling.md](ui/styling.md)).
- **Naming hazard**: members of the statically imported enums (`NsSize`, `NsAlign`, `NsAs`,
  `NsTextAs`) must stay unique across the enums, against `Surfaces`' own members **and** against
  ordinary code-behind identifiers — never name anything `Title` or `Close` on a type that
  composes with them; `NsPartial.GetTitle()`/`GetAction()` are methods for that reason (cases).
  - **`Main` is taken twice** (`NsAs.Main`, `Surfaces.Main`), so both are written in full
    (`As="NsAs.Main"`, `Target="@Surfaces.Main"`).
  - **`NsAs.Send` collides with `NsPartial.Send`** (every page's way to call the Mediator), so
    it is written in full too (`As="NsAs.Send"`). Every other `As` value is a bare name.
  - **`NsBreakpoint` is NOT statically imported** — `Xs`…`Xxl` would collide with `NsSize`'s —
    so a page writes `Breakpoint="NsBreakpoint.Never"` in full.
- **Emphasis: at most one high-emphasis element per context.** A screen with no dominant next
  action carries no `Main`, and that is correct. In an action row it sits last (before the close
  X). On a list page it is the New act; the search submit is `<NsSubmit As="Filter" />`. Never
  expose `Variant`, `Color`, `Size`, `ButtonType` in pages. Grow `NsAs` on demand.
  - **`As` says the intention and nothing else**: `Default` (the ordinary act — grey, filled,
    and the enum's default, so a button with no `As` already is it), `Main` (the screen's one
    accent), `Important` (the accent's soft wash), `Inline` (the flat rung), `Danger`; `Submit`,
    `Filter` and `Send` are `NsSubmit`'s own ([ui/actions.md](ui/actions.md)). **No value names
    a look, a size or a state.**
  - **"One `Main` per screen" counts what is VISIBLE, not what is in the file.** A page with
    five tabs draws one, so each tab may carry the accent of the screen actually on view; two
    `Main` in one `.razor` that are never on screen together are fine, two in the same tab are
    not. The same reading covers a wizard's steps and a surface that swaps its content.
  - **A card's act is `Important`** — four dashboard cards with four full accents means none
    leads; the accent goes to the screen's own `Main` or to nothing. The fill is
    `--ns-accent-soft`, mixed from the tenant's accent, never a stored hex
    ([ui/styling.md](ui/styling.md)). **Only the wash separates `Important` from `Default`** —
    one shape, one shadow, and **a filled button is never flat**. **Exception: a lone card that
    IS what the visible tab draws** is the whole screen, so its footer act may carry that
    screen's `Main` (precedent: `WhatsAppSettingsPage`'s Sincronizar) — what competes for the
    accent is what shares a render.
  - **Whether the text shows is `Breakpoint`'s word alone** (`NsBreakpoint`: `Always`,
    `Xs`…`Xxl`, `Never`), never a second flag — the one exception is the footer's own phone
    collapse below, which outranks whatever word a footer button's `Breakpoint` said.
    **Icon-only is a state of the label, not a
    rank**: a control with an `Icon` and no visible label — at `Never`, never given a `Label`,
    or collapsed by a container query. `Inline` in that state is the flat glyph a grid row's
    actions wear; **every intention with a fill is a SQUARE at the house's icon-action box,
    with its own fill and shadow — never a rectangle** ([ui/actions.md](ui/actions.md)). A
    screen whose principal act is a glyph writes `Main` at `Never`; there is no `InlineMain`.

**Every surface owes the user one way out that is not the save button, and it lives in the
chrome — from the first frame, not from the first read.** The footer carries the submit and the
acts beside it; leaving is the title bar's X, which on an overlay the shell draws itself, and in
a dialog also Escape. `BackdropClick` stays off. **The one non-act that may ride the
footer row is the document's own FIGURE** — what the acts are about, stated once on one line,
with its working an `NsExpander` away that opens above the row, not in it (precedent: Nueva
Venta's footer summary). Never a block of figures: the row still reads on one line on a phone,
and what opens costs it no height. A form footer needs no wiring: `<NsSubmit />`, no `OnClick`,
no per-page `Close()`, no dirty flag. **A dialog with no acts still ends in an explicit
Cerrar** — the X covers escape, not closure (cases).

**A state change is a main act.** Most transitions deserve a screen of their own — the moment
carries a payload, which makes it a form on its own surface; a transition with nothing to ask
still gets a confirm, never a fire-on-click. **The verb that fires it is the page's hero**:
`Main`, at the bottom of the surface, text label pinned on. The other legal transitions stay
ordinary actions (cases).

**Important acts sit at the bottom with text; the top toolbar is for utilities.** Acts that
move money or state — facturar, cobrar, autorizar, anular — render in the footer as full-size
TEXT buttons beside the hero, secondary emphasis; output utilities — imprimir, exportar, enviar
por mail, compartir — and pure navigation are top icons. The test: if refusing the act would
need an explanation, it belongs at the bottom with a word on it; if nobody would miss it until
they needed a paper, it is a top icon (cases). **On a phone a footer act carrying an icon
collapses to that icon alone, and the hero keeps its text**: a footer row has no width for a
line of worded buttons at that size, so a container query on `.ns-panel-footer` takes the label
of every secondary act that has an icon to collapse to — outranking the `Breakpoint` the page
wrote, which is why a footer button worded on a desktop is a bare glyph on a phone and that is
not a defect. A secondary act with no icon keeps its word, having nothing to collapse to, and so
does the hero in both of its colours — the filled accent of a document's save and the filled
danger of a destructive one ([ui/actions.md](ui/actions.md), the label and the enum that owns
it).

**A field nobody will type is not a field.** A value the user would have to look up elsewhere —
an exchange rate, a tax index — never rides a document form: it lives in configuration, a
service keeps it current, and the consuming screen reads it ambient. The test: "¿alguien va a
completar esto a mano?" (cases). **Nor does a figure repeat what it cannot disagree with: an
amount in the install's own currency wears no code**; only one in another currency carries its
code ([ui/fields.md](ui/fields.md)).

**A field helper is one line** — what goes here, without wrapping on a phone. Anything longer is
explanation, and **a paragraph rides an `NsHelp`**: the circled `?` beside the control, opened
on **click** (never hover — phones have none), **not in the document until asked for**. **A
field carrying a `?` carries no `Helper`**: together they print, beside the button, the prose
the button exists to unload, and on a phone the hint wraps and leaves the control a sliver —
fold the hint into the paragraph (`HelpAffordanceTests`). An `NsText` beside the field stays for
text the user must READ rather than seek — an irreversibility warning, a conditional state
notice — because help behind a click is help nobody found.

**One arrival, one loading — never two.** Blazor's weight is paid ONCE, at the splash; pixels
that have appeared are never torn down for a second loading state. Content→spinner→content is a
defect anywhere, and so is a content-to-content flash of language, brand or gate. **Nothing the
prerender resolved is asked for a second time** — the session, the brand, and a card's read,
which goes through `NsPartial.Handoff` rather than `Send`. **Nothing paints a figure before the
read answers**: a zero shown while a query is in flight is a false claim (cases). **The splash
leaves when the screen answers, not when the runtime does**: `window.nsapp.appReady` is sent by
`NsAppReady`, which `NsRouter` places inside the routed subtree and its NotFound branch, so the
signal costs one render of the page it announces and the pixels under it are the ones Blazor
drew.

**Icons are chosen, never defaulted.** A glyph is the entry's one-word name in pictures: it says
what the thing IS — an envelope for messaging, never the catch-all gear. **No two siblings share
a glyph** — measured on each app's COMPOSED drawer and Configuración, because the merge is where
a kit's entry becomes the entry a person reads and where two kits' doors become siblings
(`DrawerSiblingGlyphTests`); a parent and its own child are not siblings. And **a module's
section glyph is never one of its cards'** — a section names the
whole module, so a card wearing it says "Productos" where it meant "Stock Bajo"
(`CardSectionGlyphTests`). What is shared is the **symbol, not the constant**: one Material
Symbol pasted a second time under another name is the same picture, so a catalog entry declares
which symbol it is and the rule is measured on that. Missing from the catalog, add it (the
`new-icon` skill) rather than borrow a neighbour's (cases).

---

## Combos, lookups, filters

- **A combo the eye can search comes full** — with few enough rows to scan, the picker opens
  populated. **The empty, search-first combo (`Lazy`) is reserved for tables where unpaginated,
  unfiltered data is meaningless** — parties, products — and nothing else.
- **Creation lives inside the dropdown, never beside the field**: one create entry, the
  dropdown's permanent last item. The field's edge belongs to the lupa or the dropdown arrow.
  **The one exception is an empty set**, where the dropdown is a place nobody opens: the picker
  then draws `NsMissing` UNDER the field — the concept by name and a link to where one is made —
  in the under-field zone the hint and the refusal share, and only while there is genuinely
  nothing to pick (cases).
- **A question the chosen row still owes is asked INSIDE the dropdown, never under the field**:
  a row the rules cannot settle alone — an article more than one supplier quotes — carries an
  arrow and opens its answers beside it; picking one picks the row. A control drawn under the
  field appears *after* the pick, re-flows the grid the field stands in, and re-opens a question
  the user thought answered.
- **A lookup's prompt and its create label are composed, never written per entity.**
- **Filters bind a model, never loose page-local fields** — keys derive from
  `{Area}.{Message}.{Member}`, so a page-local binding renders mute. Bind the list message when
  its fields fit, an intermediate filter model when they don't — a model even for one field.
- Contracts: [ui/fields.md](ui/fields.md).

---

## Files

**One file is one field; a set of them is a different field.** `NsFileUpload` holds one value,
`NsMultiFileUpload` an ordered set; they take **the same parameters**, and **no parameter turns
one into the other** (cases). The contract — one action icon per line, deletion riding the save,
the order being the value, where the bytes go — is [ui/fields.md](ui/fields.md).

---

## Packages and layering

- Semantics → `NSail.Components` (vendor-free): base classes, routing (`NsSurface`,
  `IRouteContributor`, `RouteTable`), layouts, dialogs (`DialogManager`), semantic models.
  `NsPage`/`NsPartial` live here — they return plain strings and `ActionItem`, never a vendor
  component.
- Rendering → `NSail.Components.<Vendor>`: `NSail.Components.Mud` (active),
  `NSail.Components.DaisyUI` (scaffold; not "Daisy", and there is no Bootstrap package).
- **No MudBlazor types in public NSail APIs** — no `Variant`, `Color`, `Size` parameters; map
  semantics to vendor styling inside the Mud project.
- Kit and App UI is standard Razor in `*.Shared` projects, composing `Ns*` components.
- A control belongs in NSail if it represents a repeated concept, improves semantics, reduces
  vendor coupling or improves readability. **Do not wrap controls without adding meaning; if a
  component always needs many parameters, the API is wrong** — fewer controls, fewer parameters,
  stronger defaults.

The `Ns*` catalog, and which page carries each contract: [ui/README.md](ui/README.md).

---

## Pages are deny-by-default

`NsPage` carries `[Authorize]` — every page requires a signed-in user unless it opts out with
`[AllowAnonymous]` (sign-in, sign-out), so nothing is stamped per page. This is a separate layer
from the Mediator's deny-by-default gate (permissions.md): that one protects the message, this
one keeps an anonymous visitor from seeing the page shell at all (`NsNotAuthorized` redirects to
sign-in).

---

## Surfaces (aside / modal)

**A screen fits its surface.** Fields cut off, horizontal scroll, or a form folding under the
fold are defects of the *pairing*: the page declares the surface size it needs and the host
honors it. Verifying a screen includes verifying it fits.

Named surfaces render a routable page inside an overlay while the underlying page stays mounted.
They are **URL-driven**: the surface named `X` renders the route carried in the `?X=<route>`
query parameter, so surface content is deep-linkable. The layout declares them (`MainLayout`:
`aside` → `NsDrawer`, `modal` → `NsDialog`) and the target page is a normal `@page`. **Open**
with a link that carries a `Target` (`Target="@Surfaces.Aside"`) or `Surface.Open<TPage>(...)`;
**close** with `Surface.Close()` — never `Surface.Back()`: only `Close()` knows whether this
surface's own open pushed an entry to spend, and `Back()` on a deep link leaves the site.

How the hosts translate a size, stack against the shell and carry the query seam:
[ui/surfaces.md](ui/surfaces.md).

### A menu is not a surface

**An anchored menu (`NsMenu`) is chrome, not a place**: no route, no `?name=`, no size token —
nothing deep-linkable, nothing surviving a reload, which is right for it. A surface is a
*place*, a dialog a *question*, and **a menu a CHOICE offered next to the control that offers
it**, dying when one is made.

- **A menu is the answer when the whole content is a short list of acts** — a switcher, a
  utility list, an overflow. Rows are one line each and it needs no chrome of its own: no title
  bar, no frame, no X. **A row that is not one line means it is not a menu.**
- **A modal is a brief act that DEMANDS an answer**, and it costs the whole viewport to ask.
  Anything with real fields is a modal or an aside (below); a list of destinations and verbs is
  a menu, and building it as a modal is the mistake this rule names.
- **A menu is the one home of fine print** — a version, a build. It is diagnostic data, needed
  exactly when the menu is already open, so it never earns a place on a page or in the nav.

Contract, and the rule that a row's command must outlive the menu that closed it:
[ui/menus.md](ui/menus.md).

### Addresses

- **URLs are typed, never strings.** `RouteTable` (DI, `AddRouteTable`) resolves a page's URL
  from its type. Pages call `GetUrl<TPage>(parameters)`; services inject `RouteTable`. **The
  only place a route is written is its `@page`.**
- **Route parameters are typed, never parsed.** Templates use Blazor's constraint syntax
  (`@page "/directory/parties/{Id:guid}/edit"`) and the page declares the typed parameter. A
  `Guid.TryParse` in `OnParametersSet` is the smell that the constraint is missing (cases).
- **Both ends of a URL are invariant.** `GetUrl` formats dates and times sortably
  (`2026-07-30T09:00:00`). A `datetime` route token arrives with `Kind` unspecified, which is
  what a wall clock is.
- **Query values come from the surface, never from `[SupplyParameterFromQuery]`** — a defect
  anywhere in a page, because a page in a named surface is not routed by the browser's query.
  Read with `GetQuery<T>("Name")`, write with `SetQuery("Name", value)`; a write always
  *replaces* the history entry — a tab or a filter is a view of where the user is, not a place
  Back owes them (cases).
- **Query is applied where it is read, in `OnParametersSet` — never copied into a field at
  initialization.**

### Targets

**Where a link opens is its `Target`, HTML semantics.** A '_'-prefixed value goes to the
browser (`"_blank"`); any other name is a surface from the `Surfaces` constants catalog — apps
add their own class for custom surfaces. `ActionItem.Target` carries the same semantics, and
**there is no `Surface` parameter anywhere.** Two reserved targets — link vocabulary, never
`SurfaceContext.Name` values:

- **`Surfaces.Auto`** — one surface further out than where the link renders: main → aside,
  aside → modal; from a modal the chain reuses its last step. A component hardcoding `Aside` for
  a link that escalates is the smell.
- **`Surfaces.Main`** — the underlying full page, from anywhere. Not null (`Target=null`
  already means "navigate my own surface"): plain full-page navigation, which closes the
  overlays.
- **A target is never dropped for want of a cascade** (cases).
- **A browser target leaves the surfaces behind.** The tab it opens has no aside and no modal,
  so `"_blank"` resolves to the rooted route — the address `Surfaces.Main` gives — from whichever
  surface the link renders in. That is how a link reaches something that is not a page (a PDF
  endpoint).

**The default target for any in-app link is `Surfaces.Auto`.** Exceptions are declared at the
link. A dashboard card's door is not one — it always opens on `Surfaces.Main`, a flat rule of the
host that mints it, never a target a card names ([ui/hosts.md](ui/hosts.md)). Four rules:

1. **Main is residence, not size.** A window opens straight on Main when the user *stays* in it
   — the OT, the ficha, the destinations; a window the user *passes through* (create, edit, a
   step) is an aside. "It is very large" and "the grid behind adds nothing" are symptoms;
   dwelling versus passing is the criterion.
2. **A modal never opens another modal.** Any surface chain that reaches a modal ends there — so
   a window with lookup-creates or link-modals inside is never itself a modal. A modal's own
   yes/no is not this: `Confirm`/`ConfirmSend` and `DialogManager.Open<T>` (*Dialogs, saving,
   closing*) are ephemeral and unrouted, never a second `?modal=`. What is forbidden is content
   that needs another *place* — a real page, reachable and editable on its own — and that need
   disqualifies the surface from being a modal; the fix is an aside, never a stacked modal.
   **The converse holds: a create page that terminates the chain may be a modal with real fields
   and tabs, opened over an aside** — `CreatePartyPage` from Nueva Presentación's party lookup is
   the shape. Its own lookups are `QuickAdd` and its sub-editors are host dialogs, so nothing
   inside it needs a second place and the chain stops at it. Field count, tabs and size are not
   what disqualify a modal; a routed lookup-create inside it is. An aside opening a modal is not
   a modal opening a modal.
3. **A modal is a decision gate; an aside is a document that coexists.** Real fields do not
   decide it — Autorizar (`AuthorizeWorkOrderPage`) has editable fields and is still a modal,
   because confirming or overriding its computed coverage is the one committed act the screen
   exists for. What decides is whether the user resolves a single decision and leaves, or works
   in a form that coexists with the grid behind it. Attention is bought with brevity of the
   *act*, never with size.
4. **A surface is a place, so Back closes it.** Opening an aside or a modal is a navigation: it
   takes its own history entry, Back closes it and leaves the opening screen standing, and the X
   or a successful save spends that same entry rather than stacking another. Two cases differ,
   neither a caller's choice: **moving the page inside an already-open surface replaces** — a
   second row clicked into the same aside is one place, not a stack — and **a surface reached by
   a pasted URL or a reload closes by replacing**, having no entry of its own to spend. **The one
   caller-declared exception is `NoHistory`, already declared**: the satellite create — the
   lookup's inline create entry (`NsAutocomplete`) and the empty picker's door (`NsMissing`),
   marked inside those two components, never at a call site, a lookup wrapper or `NsPageLink` —
   opens a window that feeds a value back into the form underneath and that the save then
   closes; its own entry would leave that finished window one Back away from the form it fed.
   Anything else asking for `NoHistory` is a design question, not a parameter.

### Dialogs, saving, closing

- **Dialogs host the same contract — the same component, not a lookalike.** Surfaces are
  *places* (routed, deep-linkable); dialogs are *questions* — ephemeral UI opened
  programmatically (`DialogManager.Open<T>`), the only way to edit state not persisted yet.
  **There is no dialog-specific context**: results flow through the component's own
  `EventCallback` parameters, never through `Close`. Reference: `LocationsEditor` +
  `LocationForm` (cases).
- **React to data changes — publish a UI event, never hook navigation.** The producer publishes
  after the operation succeeds (`Mediator.Publish(new PartySaved { ... })`, message in
  `*.Shared`); the page subscribes with `NsPage.Subscribe<PartySaved>(handler)`.
- **Master/detail on the main surface: save keeps the page open.** Closing on every save is the
  ERP anti-pattern: an edit page re-fetches and repopulates, a create page navigates *in place*
  to the entity's edit route. **Only the title bar's X closes.**
- **In a popup, a successful save closes the surface** — aside, modal and host dialog alike,
  never wired per page. The only opt-out is `KeepOpen`, for a page that genuinely is a multi-add
  loop ([ui/forms.md](ui/forms.md), cases).

### The document

- **The record's liveness is form data, never chrome.** `IsEnabled` — and any flag meaning "this
  record is active" — renders as a regular field, `NsRecordState`, in the editor's own
  `NsFormGrid`, which is what marks the surface dirty on toggle; the label is `Common.Enabled`,
  once. **The component speaks adjective (`Enabled`), the model speaks predicate
  (`IsEnabled`).** Flags that are genuine data (`AllowsNegativeBalance`) work the same way — not
  a special case, the only case (cases).
- **Tabs partition a document's facets — the facets decide, not the verb.** An editor whose
  document has real facets tabs them instead of scrolling forever: first tab the document's
  identity, every other tab a facet. **The test for a facet: related, and saved together** —
  committed by the same Submit; what persists through its own messages is its own section or
  screen. Three fields do not earn a tab; a facet with its own editor component does; create vs
  update makes no difference. Contract: [ui/hosts.md](ui/hosts.md).
- **Form density — a field is content-width or it grows, never a grid fraction.** Two honest
  widths, no third: **content-width** (the default) for *bounded, known* content — a date, a
  document number, a CUIT, an amount, a short select — several flowing onto one line and
  wrapping when the container narrows; **`Grow`** for open-ended text — a name, a description, a
  search — filling the rest of the line. `NsFormGridItem` has three modes: content-width,
  `Grow`, and `Full` (owns its line, wins over `Grow`). Nothing pads a lonely field. **Content
  width is a CAP, half the line**, so two content-width fields share a row wherever the
  container can seat two (the 448px aside included), filling it with the grid's gap between
  them. Below the width where a pair stops reading (two fields at the growing basis — the phone)
  a field takes its own width back and owns the row. **A label that cannot read at half a line
  earns a WIDER nature, never a shorter word**, and a field that declares its own width means it
  at every width. Exemplar: `PartyIdentityEditor` (cases).
- **A form only submits a model it loaded.** The read is the form's half (`OnLoad`), twin of its
  submit: a screen never builds an empty model "for now" and hands it over — a blank standing in
  for unreadable data is one click from overwriting it. A failed read renders the refusal and a
  retry instead of the fields — no fields, no save. **A document that does not exist yet is not
  a failure**: the blank IS the data, and the form is editable ([ui/forms.md](ui/forms.md),
  cases).
- **Unsaved changes are the form's business, not the page's** — `NsForm` reports its edits and
  the surface guards the exit. **Filter/search forms declare `<NsForm ... Untracked>`**
  ([ui/forms.md](ui/forms.md), cases).

### Chrome, size and scroll

- **Size**: the page declares the size it needs (`SetSize` in `OnCreated`) and every host
  translates the same token, so **a page renders at a consistent size as main content or inside
  a surface**. Most pages declare nothing ([ui/surfaces.md](ui/surfaces.md), cases).
- **On the main surface the page's own title bar IS the chrome; the shell has no app bar at any
  width.** `NsTitleBar` draws the title, the glyph/spinner slot and the utility actions —
  **wayfinding and utilities on top, acts at the foot**, completing the emphasis ladder. **An
  overlay's chrome is the shell's**, drawn by its host at once, with the X the main surface never
  draws: the page's own bar announces there and draws nothing, so the row stands before the first
  read answers instead of waiting inside the form for it ([ui/surfaces.md](ui/surfaces.md), the
  announce seam). **A title belongs to the page, never to a frame**: nothing above the content
  names a screen the page did not name itself. The way back to a hidden
  drawer is the one thing no page can own, so **the hamburger rides at the head of the title
  row, only below the drawer's own breakpoint — and once per screen**: the first title row on the
  surface claims it and a second draws none, so a panel that is a section of the screen is named
  with a heading rather than a second title row ([ui/surfaces.md](ui/surfaces.md), the announce
  seam). The title bar also announces the page
  (`SurfaceContext.Announce`), so any chrome that must name the screen reads it off the surface,
  never off the route. `NsFullLayout` follows the same rule wherever it has a drawer to hide
  (the wizard's progress rail included): no app bar, the content names itself, and its bare
  centered frame (sign-in, no drawer) draws nothing above the content. No layout mounts
  `NsAppBar`. **Pages are unchanged**: search, filters and content actions
  (`NsPageHeader`'s toolbar — FILTROS, NUEVO) belong to the content below them
  ([ui/surfaces.md](ui/surfaces.md), cases).
  - **The page name never gives way.** Where the title and the utilities beside it do not both
    fit, **the row wraps** — the utilities take a line of their own at the same trailing edge —
    and the title keeps its own content width; it is never elided and never shortened. Both of
    the bar's faces hold it, the main surface's and an overlay's. A title row that shrinks its
    heading to a zero floor so the chrome can keep its full width has the trade backwards.
  - **Nor does a tool, so the toolbar row under it wraps too.** Where the tools do not all fit,
    **the row grows a line** and each control keeps its own content width, still at the trailing
    edge: a row that cannot wrap has only shrink left, and a shrunk worded button wraps its own
    LABEL instead, with the glyph parked between the two lines. The search box is still the one
    thing on that row that gives — it declares a comfort width, not a content one
    ([ui/styling.md](ui/styling.md)) — and the row takes the line once that is spent.
  - **The drawer owns the brand wherever it is on screen, and the session too**: a sticky chip
    at its foot — avatar, name, current organization — anchors the session menu. Sidebar is
    identity, the title row is context: the nav drawer runs the full viewport height, shows the brand
    at the top of its header, and **wears one palette in both themes — a dark gray derived from
    the tenant's brand primary, never a configured colour** (`.ns-nav-drawer` in `ns-mud.css`).
    **Dark/light moves the content area's Background, Surface and TextPrimary and nothing
    else — the chrome stays put.** **Top-level nav groups are separated by a divider**, the
    rail's own hairline; nothing is divided inside a card, where spacing already says it. **The
    active entry wears the brand's own hue** — Secondary/Tertiary were refused: where the user
    *is* is the brand's business, and those stay accents for data (states, badges). It wears it
    as the band's INK (`--ns-rail-accent`) and not at full strength: a free pick painted as a
    word on a chrome the brand had no say in is the least readable word in the gutter
    ([ui/branding.md](ui/branding.md), The accent as ink). **Below the drawer's breakpoint the drawer — and so the brand — is behind the
    hamburger**: one place owns it and no viewport shows it twice. The brand's other place is
    the splash.
- **Containers belong to layouts, not pages**: the app layout wraps `@Body` in one
  `<NsContainer>`, and the drawer and the dialog are their own chrome. **Pages never render an
  `NsContainer`.**
- **Pages rendered inside a surface use no root card** — the drawer/dialog is already the
  surface. Their skeleton is **`NsPanel`**, the same Header/Content/Footer anatomy without the
  chrome ([ui/styling.md](ui/styling.md)).
- **Only what is marked scrolls, and something is always marked** — one overflow owner per
  surface; the shell around it declares none (cases).

### Base chain and lifecycle

`NsPage` → `NsPartial` → `NsComponent` → `ComponentBase`: a routed page, a *piece* of a page,
and the plumbing under both — **a widget that only renders parameters stays on
`NsComponent`/`ComponentBase`** ([ui/surfaces.md](ui/surfaces.md)).

**Page lifecycle**: `NsPage` seals `OnInitialized`/`OnInitializedAsync`. Pages use
`OnCreated()`/`OnCreatedAsync()` — after the wiring, before the first render, with `Surface`
available, so size declarations and `Subscribe` calls go there. `OnParametersSet*` and
`OnAfterRender*` stay normal Blazor.

**`ConfirmSend(message)` is the confirm-then-send gesture**: asks with the message type's own
string (`"{Area}.{Message}.ConfirmMessage"`, falling back to `Common.ConfirmSend`) and sends
only on acceptance. **No per-page confirm strings, no hand-rolled `Confirm` + `Send` pairs.**

**The message writes its own question, and no `ConfirmSend` in `src/` reaches the fallback.**
"¿Confirmás esta acción?" names neither the act nor the document, which makes it the confirmation
people accept without reading. So every message a screen confirms carries its own
`ConfirmMessage` in both languages, **naming the act and capitalizing the entity, and
never the record** — "¿Anular esta Orden de Compra?", "Delete this Product?". **The record is
absent because there is nowhere to put it**: `ConfirmSend` resolves that one key and passes no
arguments, so the question is a single string with no placeholder, and the act is fired from the
record's own screen or its own row — what it takes is on screen behind the dialog. A hand-rolled
`Confirm(text)` guarding something that is not one message's send is a different gesture: the host
owns those words and may name the row they are about. That one sentence is the whole question
where the act takes only what it names; **a second sentence is owed where it takes something
else** — `Calendar.RevokeCalendarFeed` adds that the published URL stops answering and cannot be
restored, `Iam.DeletePolicy` that everyone it grants access to loses it. The generic string stays
in the Stack for a kit outside this repo that adds a confirm before it adds its text; inside this
tree `ConfirmMessageStringTests` resolves every message that can reach a `ConfirmSend` out of the
compiled screen — a call choosing between two owes words for both — and fails on one with no
text of its own.

**A kit screen belongs to every product that composes it.** A story changing what a kit's
Shared screens render updates each documenting product's guide in the same story.

---

## Refusal placement

Where a "no" is drawn, and what it may move: **nothing transient, everything spatial, and the
same rule at every screen size.**

**A refusal is drawn where the user is looking, and it is never silent.** A form on screen never
answers with a toast the user must connect back to it: the form handles the report. The text is
whatever the sender localized (`Problems.{Code}` / `Problems.{Code}.{Source}`) — **the Stack
invents no wording**; a rule that reads badly is fixed in its own kit's strings.

**The snackbar is dead for refusals.** Three placements are the whole vocabulary, none a popup
over a popup:

- **anchored** — an issue naming a rendered field, under that field;
- **badged** — an issue anchored on a field the active tab is not showing marks its **tab
  header**;
- **at the foot** — everything else, as ONE strip, seated in the row the form's actions occupy
  where a panel footer claims it, after the content otherwise.

**The foot is the host's, not the page's: a row editor places its own refusal.** An
`NsListEditor` row refused on Confirmar draws under the field of the ROW the issue names, and
anything the row renders no field for as one strip on the open row's last line — never at the
form's foot, and never a toast the user has to connect back to the row they just confirmed. The
same two rules, measured from the host the user is actually looking at ([ui/hosts.md](ui/hosts.md),
cases).

**A refusal stands until the attempt that could replace it, and every attempt lifts the ones
under it.** The row's next Confirmar lifts the row's, and the document's submit lifts the row's
too: one validation pass re-answers the whole document, so what still refuses re-posts in that
same pass. A row left refused never makes Guardar go mute — the submit runs or it draws its
reason, and a collection that leaves the screen takes its refusal with it (cases).

**Being *rendered*, not being a real property, is what anchors an issue**, and a screen that
renders its members as fields needs no wiring for any of it ([ui/forms.md](ui/forms.md), cases).
**A member edited through composed controls rather than a field is the one exception, and it
costs one line**: the editor places an `NsFieldRefusal` and the page writes its expression
against the model member, or the refusal falls to the foot as "Valor inválido" — the placement
the kit's own manual promises it is not.

**No screen draws one of the three itself — including a refusal it decided ITSELF.** A "no"
reached before any round trip — a rule over two values, a check the server would also make — is
handed to the form on submit (`SubmitEventArgs.Problem`), which places it by the same rules and
clears it on the next submit. Early and server refusals are one markup, one placement, one
moment: **Save**, never the keystroke. An alert a page renders in its own footer is a fourth
placement and costs that row height every time it appears (34px→50px, measured — cases).
`Abort()` is the mute twin: it refuses the submit silently, which fits a "no" the user just gave
(a declined confirmation) and never one they have yet to read.

**An act the PAGE runs inside a form is refused on that form.** A probe, a state transition, a
door drawn beside the fields — the handler is the page's, the refusal is still the screen's, so it
takes the same three placements: anchored where its reason names a field the form renders
(Almacenamiento's Probar naming Proveedor), at the foot where it names none. **Standing inside the
form is what decides it** — no act declares anything and no page wires a report — and it stands
there until the next press of an act, since the one that works says nothing at all: a press that
sends nothing takes nothing down, and **what `Guardar` was refused for is never a probe's to
clear**. An act with no form around it keeps the toast, and so does one on the far side of a portal
(a menu row): there is nothing on screen for a refusal to belong to
([ui/forms.md](ui/forms.md), [ui/actions.md](ui/actions.md)).

**Nothing reserves, and what arrives is read in full.** A field keeps no line for a message, so
an unrefused form spends no air on the possibility; a message that arrives takes the room its
sentence needs, **wrapping at rest** — no hover, focus or tap, at every width, because the
surface where every field is narrow has no pointer. A message clamped to its field's width
loses the half that says what to fix. **The refused row grows, and that is the price**: downward,
the fields sharing the line keep their tops, nothing above the row moves (cases).

**A read that failed is not a read that came back empty, and never renders as one.** "No
records", "Empty", a zero — each is an answer ABOUT the data, so only a read that reached the
data may give it. Content from a read declares an `NsLoad` around itself: the failure draws in
the content's own place, with a Retry that re-runs that read alone, in the sender's own words.
**A load failure is never a toast** (it fades and carries no retry) and never the app-wide
splash: `NsPage` runs `OnCreatedAsync` through the `Runner`, so an unconverted screen still costs
a reported `Problem` rather than the whole app ([ui/hosts.md](ui/hosts.md),
[ui/surfaces.md](ui/surfaces.md)).

**The snackbar still offers, and an offer is not a refusal.** `DialogManager.Offer` is the one
non-blocking thing that does not fade: it stays until the operator takes or closes it, carries
exactly one action, and is for news no screen asked for — a release that landed while the tab was
open (messaging.md). `NsSetup` raises it, so it survives navigation and sits above every page.
A message the user's own act produced still follows the three placements, and `Notify` stays a
toast that fades and can be missed.

**One refusal earns an act, and only one.** A save refused because the row moved underneath
(`Conflict` — data.md's optimistic concurrency) **keeps every value the user typed** and adds a
single Recargar. Every other refusal is answered by fixing the form, so none offers to throw the
screen's state away.

**The refusal the field can answer itself is answered before the send** — the same
`[Required]`/`[MaxLength]` attributes speak under the field without a round trip and are
evaluated again by the pipeline for every caller (messaging.md), which lands the server's
refusal in the same places as the client's.

**The required mark is DERIVED from the same declaration, never written at the placement.** A
member whose `[Required]` can actually refuse marks itself wherever it is bound, so no screen
marks three required fields and leaves the fourth bare. The `Required` parameter is only for what
an annotation cannot express — a requirement that holds only sometimes, and a sentinel
`RequiredAttribute` cannot see (on a non-nullable value type, its whole default). Both ways: a
rule only the handler knew moves onto the message rather than being marked by hand, and a field
the wire does not refuse is not marked ([ui/fields.md](ui/fields.md)).

---

## Navigation menu

A kit offers and the app decides — a menu in `Add<Kit>Menu()`, a home's cards in
`Add<Kit>Dashboard()`; the app mounts each or leaves it unmounted and writes that surface itself.
**The kit plants and the app arranges, on the drawer and the home alike**: having mounted one, the
app renames, reorders or hides an entry or card by naming it and only the fields it changes,
which is why the app's own contributor is registered last. How to write a menu and how a drawer's
lines are placed: [ui/navigation.md](ui/navigation.md); the cards' host: [ui/hosts.md](ui/hosts.md).

- **Entries order by frequency of use** — the daily screen above the monthly one, inside every
  group. Not alphabetical, not by kind, not by the order features landed.
- **Settings groups: domain first, system last.** The trade's own material — products,
  workshops, insurers, practitioners — at the top; cross-cutting machinery — integrations,
  appearance — at the tail.
- **A section with one screen is an entry, not a group of one.** The contributor writes the
  first-level entry itself, with the section's name and the screen's route; the group is born
  when a second screen lands under it. The kit flattens its own contribution, so every app
  mounting it inherits the flat menu ([ui/navigation.md](ui/navigation.md)).
- **A line in the drawer is one somebody asked for**, placed as an item by whoever composes the
  menu — never deduced from the surrounding markup ([ui/navigation.md](ui/navigation.md)).
- **A leaf names its destination page, never a URL** — `PageType = typeof(PartiesPage)` (cases).
- **A leaf disappears when the session could not open the page it names**, and the page says so;
  **a group goes when every child went**, so a permission never leaves an empty accordion
  (cases).
- **In the settings tree, a group that folds down to exactly one child does not earn a level**
  (`NavMenuItem.Merge`). **The main nav never folds automatically**: the fold hands the place to
  the child, label and all, and a nav section's name is the domain's map — Compras must stay
  Compras, not become Órdenes de Compra. The main nav flattens by shape at the contributor
  instead, which keeps the name (cases).
- **Icons are named through catalogs, never `Icons.Material.*` in kits or apps.** `NsIcons` is
  the generic UI vocabulary; each app adds a domain catalog (`OpticalIcons.Prescription`).
  **Grow both on demand** — the `new-icon` skill carries the procedure.

---

## Contributed actions

An app enriches a kit's screens without owning them (Optical adds "Prescriptions" to Directory's
Parties grid) through an **outlet**: a component the projection's owner founds to name the slot.
**No string keys, ever**, and **data, not handles** — an outlet parameter is something a
contributor reads, never a component reference or a refresh callback; "something changed" is an
event.

**Link actions are gated by the destination page's own authorize attributes**, through the same
single gate the nav menu and a lookup's create entry use, so a hidden menu entry, create entry
and row action never disagree. **Command actions carry no gate**: the contributor decides with
`NsPartial.CanSend<TMessage>()`, and the server's gate is the one that decides
([ui/actions.md](ui/actions.md)).

---

## The wizard

`NsWizard` hosts a sequence of contributed steps. **The step map is the app's knowledge, never
the Stack's**; **the chrome owns the way forward** — a step never draws its own Next, Back or
submit; **every step handler is an idempotent upsert**, because Next is pressed twice whenever
Back was pressed once; and **steps are kit-pure**, reaching every other kit by message.
`WizardContext`, `IRouteGate` and the frame: [ui/wizard.md](ui/wizard.md).

---

## Localization

Strings resolve through `StringManager` (`NSail.Localization`), fed by the `IStringSource` each
module registers. Where a string is written, how a key is derived and how the culture is chosen:
[ui/localization.md](ui/localization.md).

- **Keys are derived, not written**: `{Area}.{Type}.{Member}` via `MetadataProvider.KeyFor`
  (metadata.md) — a field from its binding expression, a title from its page type, an action
  from its method name. **An explicit `Translate` is a decision, not a derivation**; aim for
  **one label string per concept**.
- **No guessing**: every language has a dictionary, including `en`, and gaps fall back to
  English. **A key missing everywhere renders as the key itself** — untranslated strings are
  visible, never silently humanized.
- **Entity names are capitalized inside a label**, in every language: "Editar Rol", "Nueva
  Persona", "Edit Role" — only the entity, not the qualifiers ("Nueva Organización hija").
  Running text keeps sentence case.
- **Menu entries and page/window titles are Title Case**, articles and short connectors
  lowercase, in every language: "Órdenes de Trabajo", "Sumas y Saldos" — never "Órdenes de
  trabajo"; a one-word entry is simply capitalized, and `GetTitle()` follows the same rule as a
  `NavMenuItem.Name`. **These two are the only exceptions to sentence case.**
- Explicit `Label`/content wins over derived keys. **A blank `Label` is not "no label" — it is
  "do not draw it"**: the derived text becomes the input's accessible name.
- Culture is **negotiated, never imposed**: the user's saved preference, else the browser's
  culture when the install carries that language, else the install's default — the same order
  on server and client, so no screen flashes from one language into another (baseservices.md).

---

## The guide

**A kit with user-facing behaviour worth documenting carries its own guide chapter**, through
`IGuideContributor` — the `ISettingsContributor` shape applied to help. An app's guide screen is
exactly the chapters of the kits it composes: registration IS the contribution, nothing filters,
so a kit an app never composed has nothing to hide. The chapter's title is a localization key;
**its prose is a markdown file the module ships beside the app, never a string in an
assembly**, fetched when the chapter is opened. Where the screen lives and how the menu reaches it
is the product's call.

- **A chapter has an address and a section has an anchor** — `/guide/{chapter}#{slug}` — and an
  index rail beside the content lists both, the onboarding progress rail's shape. **On a phone it
  collapses into the drawer, exactly as the nav does**: the page announces its index to its
  surface (`SurfaceContext.AnnounceIndex`) and the drawer draws it above the app's own map.
- **The door to the guide is `NsGuideLink`, a button, and `NsPageHeader Guide` is how a screen
  adds one**: the header places it top-right and it opens the guide *beside* the screen
  (`Surfaces.Auto`), never in a browser tab — a half-filled form is never left behind.
  **`NsHelp` is not that door**: it stays a field's own two-line whisper in a popover.

The contribution model, the index, the anchors and the renderer's grammar:
[ui/guide.md](ui/guide.md).

---

## Settings

**The default for a repetitive field lives in Settings** — a field asked on every document with
mostly the same answer (currency, fiscal period, fiscal condition) defaults from a `*Settings`
POCO, never hardcoded and never by growing the entity. The control that means the concept reads
the setting itself (`CurrencySelect`, `FiscalPeriodSelect`), so no screen repeats the lookup.

The contribution model, the scopes and the storage: [ui/settings.md](ui/settings.md).

---

## Branding and theming

Branding is a vendor-agnostic, serializable `Brand` in `NSail.Components`, supplied by
`IBrandProvider`. **Components never read Mud palette types from app code — change the `Brand`,
not the theme.** Default look: dark, warm grays with orange `#F68E1E`. **The prerender hands the
brand over; the client does not resolve it again** — a brand arriving after the paint is the
flash the one-arrival rule forbids.

The `Brand` shape, the meaning of a null answer and the neutral fallback:
[ui/branding.md](ui/branding.md).

---

## Layout and styling rules

**No stylesheet tree shadowing the DOM tree.** The whole app is ~85 lines of CSS on purpose.
Styling is utility classes *in the markup*, where element and layout are read together — never a
parallel hierarchy of semantic selectors that goes stale when a layout moves. Custom CSS earns
its place only when a utility cannot express it: a structural rule reaching inside a vendor
component, or what the browser only offers in a stylesheet.

**Components lay themselves out.** A page never needs a `<style>` block or a wrapper div with a
CSS hack to make an `Ns*` component behave — the fix belongs in the component or in
`ns-mud.css`.

- Pages arrange content with layout components, never raw `<div class="d-flex ...">` wrappers —
  flex-utility div soup only inside the Stack's own components or as a last-resort escape hatch.
  `NsFormGrid`/`NsFormGridItem` is the grid for forms, `NsStack` the 1D flow; parameters in
  [ui/styling.md](ui/styling.md).
- **No vendor typography in pages** — `NsText` renders text with the `NsSize` scale. Spacing
  utilities are fine anywhere; a custom utility MudBlazor lacks lives in `ns-mud.css`, never
  inline.

**Responsive is measured per surface, not per window.** A media query — and every
`d-{breakpoint}-*` utility, `MudTable.Breakpoint` and `IBrowserViewportService` — measures the
*viewport*, and a 960px aside on a 1920px screen is narrow. The fix is CSS container queries and
the `d-c-*` utilities that ride them ([ui/styling.md](ui/styling.md), cases).

**The one exception is chrome that answers to the drawer, and it is the Stack's, never a
page's.** Whether the nav drawer is docked or behind the hamburger is a *window* fact — the
vendor's `Breakpoint.Md` — so what trades places with the drawer flips on exactly that edge:
the title row's hamburger that stands in for it while it is hidden, and the guide's index rail
that moves into it. That decision is written once, in
`NsResponsive.ShownFromWindow`/`ShownBelowWindow`, which ride MudBlazor's `d-{bp}-*` utilities so
they cannot drift from the drawer's number. **Markup that differs by window width goes through
those two and nowhere else**; measuring the window in C# (`IBrowserViewportService`) is only for
what CSS cannot decide — a vendor *parameter*, so far only `NsDrawer`'s variant
([ui/styling.md](ui/styling.md)).

**A control's label may collapse to its icon — the one responsive knob pages get**: `Breakpoint`
on `NsButton`/`NsLink` reads *"the label shows from this container width up"*. It suits
universally-read icons, not obscure domain actions — on touch a collapsed button is a bare glyph
([ui/actions.md](ui/actions.md), cases).

**Columns declare importance and breakpoints hide the rest.** Declared once, on the header
(`NsTh`), against the *container*; the stacked mobile layout honors the same declaration. The
mechanism, and the action column that names itself by silence: [ui/hosts.md](ui/hosts.md).

### Grids: column importance, the action cell, and state

- **The comfort curve for visible columns** — the count showing *at a given width*: **1** smells
  wrong (it should have been a list); **2–3** is optimal; **4** tolerable; **5** the limit;
  **6+** means something must hide behind a breakpoint. The curve says *how many* survive at
  each size, the importance declaration *which* ([ui/hosts.md](ui/hosts.md)).
- **One action cell per row, and that cell is ONE `NsActionToolbar`** — transition verbs and all,
  never a second cell for verbs, never a strip of hand-written icons around the presenter.
- **The constant goes LAST and is always visible**: the lupa where the row has a ficha, the lápiz
  where it does not, neither where it has neither — **a destructive verb is never promoted to
  the fixed edge**.
- **Three verbs visible, the rest in the kebab** — the cap that makes contribution scale, so a
  kit contributing a rare action never widens a row ([ui/actions.md](ui/actions.md)).
- **State paints its text; condition washes the row.** Two channels, apart by default: the
  status column is quiet text whose colour carries the state's tone — **no chip**, which
  decorates every state equally loudly — while the row wash (`.ns-row-danger`) is reserved for
  CONDITIONS the row's own data settles (overdue, ready). They may deliberately OVERLAP where a
  condition earns a second reading close up — the OT grid's word carries the same
  vencido/por-vencer/Listo tones as its wash (`WorkOrdersPage`, and the person's own card, which
  lists the same orders) — but that is a widening a screen argues for, never a default, and what
  argues for it is a LIST: one order on a ficha has no column to scan and declares its state as a
  labelled field. **Shared vocabulary is not shared PRECEDENCE**: each
  channel answers its own question, so a row can read Listo on the word while its wash calls
  the promise overdue. **Where a word does not fit, the same tone rides a dot** — an agenda
  block's right edge — added BESIDE the words the surface already prints, never in their place:
  colour is never the only signal. One vocabulary for both forms; no screen owns a palette
  (`NsStatusText`/`NsStatusDot`, [ui/styling.md](ui/styling.md)). **A row's VERB may carry the
  same tone** where the cell has no room for a word — `ActionItem.Severity`, the state the act
  is about — still that one vocabulary, never an `As`: an act is `Danger` because it destroys
  something ([ui/actions.md](ui/actions.md)).
