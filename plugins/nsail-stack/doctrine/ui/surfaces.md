# Surface seams (Stack-internal)

How the surface machinery is wired: what a host translates, how the hosts stack, what a
component announces, and the seams a Stack component is allowed to ride.

**A page needs none of this.** The page-facing rules — open with a `Target`, close with
`Surface.Close()`, declare the size you need, read the query with `GetQuery` — live in
[intentional-ui.md](../intentional-ui.md). Read this page when you are building or changing a
Stack component that touches the surface. The record is
[intentional-ui-cases-surfaces.md](../intentional-ui-cases-surfaces.md); a rule marked *(cases)*
has its story there.

---

## The base chain

`NsPage` → `NsPartial` → `NsComponent` → `ComponentBase`.

- `NsComponent`: async and surface plumbing (`Runner`, `CancellationToken`,
  `Using(IDisposable)`).
- `NsPartial`: application services (`Mediator`, `Dialogs`, `Strings`, `Metadata`, `Routes`) and
  helpers (`Translate`, `Subscribe`, `GetUrl`, `Confirm`, `ConfirmSend`) — the base for kit
  components that are a *piece* of a page.
- `NsPage`: routed-page wiring (`[Authorize]`, `NavigationManager`, surface reset, `Navigate`).
- `NsPickerBase`: the other branch off `NsPartial` — a kit control that stands where a field
  stands (`*Lookup`, a hand-rolled `*Select`) and answers to the form's writability the way a
  field does ([fields.md](fields.md)).
- `NsActBase`: the branch off `NsComponent` everything that draws a way IN takes, and the one
  place the form's writability cascade is named for that chain (`NsAction`, `NsActionToolbar`,
  and `NsCollectionBase` under it) — an act under a form that refuses writes is withheld unless
  its own `ActionItem` declares it writes nothing ([actions.md](actions.md)).
- `NsCollectionBase`: `NsActBase`'s own branch, taken by the Stack's collection chrome
  (`NsTable`, `NsListEditor`) — the hosts that draw a way IN to a collection and therefore
  answer that word once, for every way in drawn through their own chrome
  ([hosts.md](hosts.md)).

**`OnCreatedAsync` runs through a `Runner` of the load's own**, so a read that throws there is a
reported `Problem` and never an exception the layout's `NsErrorBoundary` turns into the
app-wide splash. Of its *own*, not the page's: the page's `Runner` is what every click runs on,
and a `Run` that starts while another is in flight throws — a load that held it would fault the
first click landing on a slow backend, which is the splash again. That is the *floor*, not the
answer: what it buys is a toast, which fades and carries no retry. A page whose content is
worth a failure surface of its own declares one — `NsLoad`, [hosts.md](hosts.md).

**A `Runner` whose component has ended runs nothing**, and says nothing. The component's own
disposal cancels what it had in flight, and an act in flight routinely keeps going for a line
past that — a save's refetch, after the refusal that tore the page out from under it. A `Run`
asked for there gets the same answer a cancelled one gets: no act, no `Problem`, because there
is no screen left to put either on. Thrown instead, it escapes the continuation into
`NsErrorBoundary` and the person reads an error page about a screen that is already gone.

---

## Opening and matching

`NsSurface` renders the route carried in its `?<name>=<route>` query parameter, resolved through
`RouteTable.Match`. The three ways in, all equivalent:

```razor
<NsLink Href="@(GetUrl<UpdatePartyPage>(new { id = row.Id }))" Target="@Surfaces.Aside">
Surface.Open("aside", route)
Surface.Open<UpdatePartyPage>("aside", new { Id = id })
```

**Every one of them lands in browser history through `SurfaceContext.Follow`, which is the only
door.** Three transitions, and the caller influences one:

- **closed → open** (the name's key absent, now present) **pushes** — a surface is a place, so
  Back closes it — unless the opener passes `NoHistory`, which replaces. `NoHistory` is a
  parameter on **`NsLink`** and is set inside the two Stack components that compose the satellite
  create — `NsAutocomplete`'s inline create entry and `NsMissing`'s empty-picker door — and
  nowhere else; `NsPageLink` does not forward it and no lookup wrapper sees it.
- **open → open** (a link pointing an already-open name at another route) **always replaces.** No
  flag changes it, or a back-stack would grow by one per click inside one open surface.
- **open → closed** (`Close()`: the X, Escape, the backdrop, a successful save) **pops** the entry
  the matching open pushed, and **replaces** when it pushed none.

**Which open pushed is `SurfaceHistory`'s** — a scoped service, registered beside `RootSurface` in
`AddRouteTable` and handed to every `SurfaceContext`. It cannot be a field on the context: the
open is written by the surface UNDERNEATH, and the overlay's own context is keyed by the route
inside it (`NsSurface`) and rebuilt whenever that route moves, so the object that opens and the
one that closes are never the same. **That store starting empty IS the deep-link and reload
fallback** — nothing recorded, so the close rewrites the address and Back never leaves the app;
there is no separate detector and none is needed.

**The destination surface is named by the caller, never diffed out of the address.** `Follow`
takes the `Target` its href was resolved against (`GetHref`), because a query key appearing on the
same path is not enough to say a surface opened — a filter or a tab written by a link reads
identically. `Surfaces.Auto` is resolved through the same escalation `GetHref` uses, so an
aside→modal escalation is a closed→open on `modal` and pushes.

**The pop is `history.back` over JS interop, and it stays guarded.** Blazor restores the history
position before it asks the location-changing handlers and leaves it restored if one refuses
(`blazor.web.js`, `onBrowserInitiatedPopState`), so the `NavigationLock` the surface owns protests
over a dirty form exactly as it does when the close is a navigation. `Departs()` reads the address
it is handed and nothing about stacking, so it is unchanged by any of this.

**One gesture spends one entry**, so `Close()` suppresses a second pop while the first is in
flight — `history.back` is a round trip (interop, popstate, circuit, the handlers, unmount) and
the X, Escape and the backdrop are three un-debounced handlers on the same surface, so a
double-click spending two entries would land one screen past the one the surface was opened from.
`SurfaceHistory` cannot answer that: it describes the browser's stack rather than the gesture, and
is deliberately never cleared on close so a surface returned to by Forward still pops. **The
suppression ends with the gesture, whichever way it ends** — `LocationChanged` for a pop that
committed, and `NsSurfaceContext`'s own guard for one it refused, which raises nothing. A latch
released by neither would trade the over-pop for a dead X: a refused pop leaves the surface open,
and it has to stay closable.

**One surface per name.** `?<name>=` carries one route, so a name IS a surface: pointing a name
that is already rendering at another route replaces what it holds — legal navigation, and
guarded rather than silent (below, *The form seam*). **A second simultaneous modal is therefore
a second surface the LAYOUT declares**, with a name of its own, never something a link can ask
for: `Surfaces.Auto` reaching the Chain's last rung self-targets and replaces (`Escalated()`).
The mechanics such a stack would need — its own rung in the Chain, its own chrome, a `Close()`
that restores what the key held instead of removing it — **wait for the first layout that
declares one**; none of it is built ahead of that layout.

---

## The query seam

Query values come from the surface, never from `[SupplyParameterFromQuery]` — a page in a named
surface is not routed by the browser's query. `NsPage.GetQuery<T>("Name")` delegates to
`SurfaceContext`; the raw forms are `TryGetQuery` and `GetQueryValues`. **Conversion is
invariant** and covers nullables, enums and arrays; an absent or unreadable value is the
default, never a throw. **Nothing is cached** (cases).

Writing goes back through the same door, `SetQuery("Name", value)`: onto the route the page is
*routed by*, always **replacing** the history entry — a tab or a filter is a view of the place
the user is at, not a place Back owes them. Null removes the key; on a dialog it is a documented
no-op.

**A component may ride this seam, and exactly one does: `NsTabs BindQuery`.** The shape to copy
if another earns it: opt-in from the call site, a fixed key rather than one derived per
instance, an invariant name the caller declares, both ends through the surface.

---

## The size seam

`SurfaceContext.Size` carries an axis-agnostic token (`NsSize`, default `Md`) and **each host
translates it** — `NsContainer` to a max-width, `NsDrawer` to a pixel width, `NsDialog` to a
**fixed width and height**, both read against the viewport and never against the content (hence
`Size`, not `Width`). That is what makes the same page render at a consistent size as main
content or inside a surface. Below MudBlazor's own `sm` breakpoint a dialog is full screen,
square-cornered — the same mobile degradation the aside already has below its docking breakpoint.
`Surface?.SetFloating(true)` overlays with a backdrop even where the layout would dock (cases).

**A dialog does not scroll its own content.** The frame is a box of known, still size; the
overflow it cannot avoid is the hosted page's own to solve, wrapped in the same `NsPanel` it
already needs for the main and aside surfaces. `.ns-dialog` clips (`overflow: hidden`) rather
than scrolling — a page that forgets to wrap itself gets a clip, not content painted over the
backdrop (cases).

- **`Xl` in the drawer means as wide as the surface may be — never full-bleed**: the nav gutter
  stays, the URL stays `?aside=`, the X still returns to the list.
- **No aside covers the nav gutter on desktop.** One rule in `ns-mud.css` on `.ns-drawer`, for
  every size. Below the nav's docking breakpoint the aside takes the viewport: the mobile
  degradation, and it is allowed.
- **Drawer behavior** is the app layout's decision: `<NsDrawer Mode="NsDrawerMode.Docked">`
  docks beside the content on wide screens and overlays on small; `Overlay` (default) always
  overlays. Backdrop click / escape close via `Surface.Close()` — the URL stays in sync.
- **Because the default surface outlives navigation, `NsPage` resets `Size`/`Floating` to
  defaults on every page load before `OnCreated`.** The hosts' own `MaxWidth`/`Size` parameters
  apply only when there is no surface cascade.

---

## The stacking seam

**Surfaces stack against the shell's chrome, never through it**, ranked on MudBlazor's own
tiers. **A docked surface renders at the vendor's drawer tier**, beside the page like the nav
drawer — full height in a layout with no header, clipped under the header in one that mounts
it (`NsDrawer` reads `NsLayoutState.HasHeader`). **A floating surface renders OVER everything**:
a modal covers the viewport and its backdrop dims *everything* underneath — nav gutter, an aside
standing open beside it, the page. **Nothing is scoped to the content region.**
`SurfaceContext.ZIndex` is the ladder for the floating half only, so a docked host must not read
it. Below the docking breakpoint the aside is a sheet ranked against the vendor's app-bar tier,
over the backdrop MudBlazor paints for it — the mobile degradation, not a leak (cases).

---

## The announce seam

On the main surface the page's title bar draws its own row and announces it. `NsTitleBar` — and
`NsPageHeader`, which composes it — decides by where it lives: on main (`Surface.IsMain`) it draws
the hamburger (below the drawer's breakpoint), the glyph/spinner slot, the title and the
utilities, with no X, and announces its title, its derived glyph and its utility-actions
fragment to its surface (`SurfaceContext.Announce`); on an overlay it draws its own row with the
X, and the announcement goes unread. **Pages are unchanged** — the component decides by where
it lives. **An overlay keeps its own chrome**, and its X is the way out (cases).

**The hamburger is the surface's, so a screen shows it once however many title rows it draws.**
The first row to ask holds the claim (`SurfaceContext.ClaimsToggle`) and a second row draws none;
the holder hands it back when it goes (`ReleaseToggle` from `Dispose`), because the main surface
outlives every page on it and a claim left standing would be a phone with no way to the drawer on
every screen after. **A panel that is a section of the screen rather than its subject is named
with a heading, not a second title row** — `<NsText As="Title">` in the panel's `Header`
(`PasswordPage`'s Segundo factor, `PendingReceiptCard`): the title row carries the screen's own
name, and a screen has one name.

**Chrome that reads the announcement must subscribe to it.** The surface cascade is `IsFixed`
and `Announce` raises `AnnouncementChanged` alone — never `StateChanged`, which is a separate
event for a reason (cases). So a component whose markup reads `Surface.Title` — `NsAppBar` is the
Stack's one, and no layout mounts it; the drawer's header shows the brand unconditionally and
reads no announcement at all — hooks `AnnouncementChanged` in `OnInitialized` and unhooks it
in `Dispose`. Without that hook nothing redraws it when the arriving page announces: it keeps
drawing the departed page's name until something unrelated re-renders the layout, which on a
docked desktop is nothing at all.

**And it redraws only for the part of the state its own render reads.** A surface announces
several times for one gesture — a save in an overlay announces three — and a host that redraws for
all of them spends the whole hosted tree, chrome, page and fields, emitting what is already on
screen. So `NsSurfaceContext` redraws for `HasChanges` alone, the only thing its own markup reads
(the `NavigationLock`), and `NsDrawer` for the width its size translates to and whether it docks —
its container refresh narrower still, for the dock alone, which is the half of that geometry the
vendor does not announce by itself: that container is the LAYOUT, and poking it reaches the nav,
the bar and the page the overlay is standing over. The gate is the COUNT and never a
duration: `SubmitRenderPassTests`, in the shipped composition, for the reason in
judgment.md (nsail: `docs/agents/judgment.md`) 3 — the CI box swings several-fold on identical work.

**A page's heading may be two lines, and the second is `Subtitle`** — a fragment `NsPageHeader`
draws on a row of its own between the title row and the toolbar, for **what the screen's subject
is called besides its name, and the standing that names it**: the road and the address under the
contact a thread is with, and who holds the ticket and where it stands (`TicketPage`) — standing
reads there as text rather than off the label of the control that changes it. It is not
`Caption`, which rides the title row's far edge where the utilities are, and it is not announced: the name is what chrome may borrow, and a second line
is the page's own.

**The toolbar's left side is the search zone or `Lead`, and they are different slots.** `Search`
takes over the search zone and wears its width (`.ns-toolbar-search`, a comfortable 22rem that
shrinks) — free text, or the scope selector a list is read through. `Lead` is for a control over
what the screen is **showing** rather than over what it lists, and it stands before the search
zone at its own width, so a screen that wants both keeps both. A control that governs the subject
*beside the acts on it* is neither: it rides `Actions`, at the row's right edge with them (no
header in the tree carries such a control today). A page that names `Search` for something
nobody searches with reads false at the call site and is sized by a box it is not; a field laid
out by hand in any of these slots takes `ns-field-slot` ([styling.md](styling.md)).

---

## The form seam

The surface owns the `NavigationLock`; `NsForm` is what tells it there is something to guard,
and what closes it when a submit succeeds. Both ends of that contract — tracking, closing,
placing a refusal — are [forms.md](forms.md).

**The seam carries one more word, for chrome the form's cascade cannot reach.** `NsForm` also
reports what it refuses (`FormRefuses`, and `FormRunning` for its narrower half), because a
dialog's header X is drawn above the hosted component in the vendor's own tree and no cascade of
that form gets there — the state travels up here and back down
([actions.md](actions.md)). **One form answers per surface, the first to report**, and
its own `Retire` hands the answer back. A reader of it subscribes to `RefusalChanged` — a third
event beside `StateChanged` and `AnnouncementChanged`, for the same reason the index has its own:
mutating a surface redraws nothing by itself, and the readers of one word have nothing to redraw
when another moves. **Its own event is what keeps a save from announcing twice.** `NsForm` reports
this one from the after-render of the pass that decided it, and a save starting is already on
`StateChanged` as `HasWork` for everything that draws a spinner — so on `StateChanged` it would
redraw the page, the chrome and the submit for a state change already announced, from a render
provoked by a render (`SubmitRenderPassTests`).
