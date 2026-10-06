# Intentional UI — cases: surfaces and refusals

Part of [intentional-ui-cases.md](intentional-ui-cases.md): the why behind the surface, dialog,
form-lifecycle and refusal-placement rules. **Nothing here is a rule** — the rules are
[intentional-ui.md](intentional-ui.md) and the [`ui/` shelf](ui/README.md).

---

# Surfaces (aside / modal)

## Why a dialog re-provides `RouteTable` itself

`MudDialogProvider` (`NsSetup`) and `NsRouter` (`Routes`) are **siblings** in `App.razor` — a
dialog's render tree shares no ancestor with the page that opened it, so nothing cascades across.
`NsOpenDialog` injects `RouteTable` from DI and re-provides it with its own `<CascadingValue>`:
the host outside the router's tree carries the bridge, never the reusable component.

## What a dialog buys by reusing `NsSurfaceContext`

`NsOpenDialog` wraps its content in the exact component `NsRouter` uses for every routed page,
passing a `Close` override that closes the `MudDialog` instead of navigating — which buys the
`NavigationLock` unsaved-changes guard and `HasWork`/`HasChanges` for free.

## Why `[SupplyParameterFromQuery]` cannot work in a surface

The attribute binds from the *browser's* query, and a page hosted in a named surface is not
routed by that: its whole route — query included — is the **value** of `?aside=`, so the
attribute reads null and the screen renders unfiltered with nothing to see. `SetQuery` merges
onto the route the page is *routed by* and re-encodes exactly once so nothing nests. Nothing is
cached because the main surface reuses the component across two addresses that resolve to the
same page, while a named surface remounts on its own — which is why query is applied where it is
read, in `OnParametersSet`.

## Why a route constraint beats a parse

The constraint both types the value and filters the match — a malformed id doesn't route at all.
`GetUrl` formats dates and times sortably because that is what the reading side parses: a
culture-formatted date builds a URL the router refuses to match, and nothing reports it.

## Why a target is never dropped for want of a cascade

Address resolution asks a `SurfaceContext` unconditionally: the cascaded one where it arrives,
the DI-registered `RootSurface` — built from the same `NavigationManager` and `RouteTable` —
where it does not. There is no bare-route fallback: a dropped `Target` would produce a *working*
full-page navigation, a failure with nothing to see. Where the cascade is genuinely absent the
answer is only *correct*, not *right* — a link inside an aside resolves against the page
underneath — so `NsLink` writes that residual to the console in DEBUG. `Surface.Back()` is never
called by a page: on a deep link `history.back` leaves the site, and only `Close()` knows whether
this surface's own open left an entry to spend (below).

## Why a surface takes a history entry, and who is exempt

Two real complaints pull opposite ways: Back resurrecting a finished satellite (going back
reopened "nueva receta"), and Back leaving the whole page instead of closing an open aside.
Refusing every surface a history entry fixes the first and causes the second; they are not the
same open. Marking the special case is more intuitive than putting history everywhere.

- **It is the navigation's property, not the surface's and not the page's.** `Cerrar Caja` and
  `Nueva Receta` are both `Surfaces.Aside`, so a rule keyed on the surface kind cannot separate
  them. What separates them is that one is a window somebody opened and the other a satellite
  feeding the form underneath.
- **The special case is marked where it is written, never at a call site.** Fourteen lookup
  wrappers pass `CreateRoute`/`CreateTarget` to one shared `NsAutocomplete`, and the empty
  picker's door is one shared `NsMissing`, so `NoHistory` costs two internal changes instead of
  a parameter every lookup must remember.
- **Four transitions, not one switch.** Moving inside an open surface keeps replacing, or every
  intra-aside click would grow the back-stack; that is not a caller's choice, so it is not a flag.
  The fragment move is the fourth and it reads the same way: it pushes only where it addresses no
  surface, because inside one the move is still a move.
- **Two designs were refused for detecting what opened.** Diffing the query keys of the two
  addresses reads a filter or a tab written by a link as a surface opening; filtering that diff
  by "the value matches a route" makes the answer depend on the route table, which a unit test's
  assembly does not carry. The caller already resolved the href against a `Target` — it names it.
- **It cannot be remembered on the context, and that pays for the deep-link case.** Open and
  close run on different `SurfaceContext` objects, so the answer lives in a scoped
  `SurfaceHistory`; a service that starts empty is exactly what a pasted URL and an F5 are, so
  the fallback needs no detector of its own.
- **The pop stays guarded — checked, not assumed.** `blazor.web.js`'s
  `onBrowserInitiatedPopState` restores the history position *before* it asks the
  location-changing handlers and leaves it restored when one refuses, so a `NavigationLock` over
  a dirty form cancels a back exactly as it cancels a `NavigateTo`.

## In a popup, a successful save closes the surface

The user expects something to happen, in every case — the trigger was a Persona created from a
lookup leaving its aside standing. Moving the close to the surface seam removed thirteen
hand-written `Surface?.Close()` calls. The in-place navigation a create page performs is dropped
while a popup form is finishing — the surface would close over the page it had just mounted, so
it never mounts it: one move, not two. On the main surface: an edit page re-fetches after save —
the form does it, because the form owns the read (below) — and a create page navigates in place
to the edit route, which `NsSurface` keys by route, so this remounts a fresh `SurfaceContext`.

## The form loads what it will overwrite

The defect: `GoogleSettingsPage` built `_edit = new()` at its declaration and only replaced it
when the read came back, so a 500 or a network blip drew every field empty with Save enabled —
one click wrote blanks over the stored configuration.

**Why the read belongs to the form and not the page**: with loading on the page and saving on
the form, safety depends on every screen wiring the two correctly, which is exactly what failed.
Owned by one component, the overwrite cannot be written by accident — a form with no model
renders no fields and no submit, so there is nothing to press.

Three states, where the bug had two: loaded, **empty by design** (a document not saved yet —
editable, because the blank IS the data), and **failed** (the refusal and a Reintentar). A
nullable model tells the last two apart no better than an eager one: it renders nothing, forever,
and says nothing — which is why `= default!` screens count too.

`NsErrorBoundary` is not the answer: it catches what escapes a *render*, and these reads did not
escape — the pages ran them raw in `OnCreatedAsync`, and the page that survived the throw kept
the blank model it had built.

Moving onto `OnLoad` removes three per-page wirings: the `if (_model.Id == Id) return;`
re-entrance guard (`@key` on the form says it), the post-save `await Load(...)` (the form
re-reads what it saved), and `OnReload` for the conflict act (the form's own read answers it).

**What counts as the pattern**: a screen moves onto `OnLoad` when a value in the document it
submits comes from a READ — a bill built from the order it bills, an editor filled from `Get*`. A
screen whose model comes from its route, its session or its parameters keeps `Model=` however
much it reads besides: what it fetches fills lookups, a corrected-note card or a balance, and
none of that is what Submit writes. `QuickJournalEntryPage`, `RescheduleForm`,
`UpdateAvailabilityForm`, `SettlementForm`, `CreateAppointmentPage`, `CreateNotePage`,
`CreateBrandPage`, `CreateProductPage` and `SignInPage` sit on that far side.

## Form density: why the grid fraction died

The *same* date is huge at 6/12 of a full screen and cramped at 4/12 of an aside — a fraction
never knew the content. `Grow` stays, for open-ended text. One default width
(`--ns-field-width`, 14rem) serves the whole content-width family; per-type intrinsic widths are
a later tune, not a mechanism change.

## Form density: why the flat 14rem became a cap

Measured on test.optical: the aside's own container is 448px, and two content-width fields at a
flat 14rem ask for 464 with the grid's gap — eight pixels short. So every date, DNI and short
select in an aside stood alone with half its row empty while the `Grow` fields beside them (12rem
basis) paired fine; sixteen screens read as one column each for one cause — a default width that
never knew the one surface with no say in its own.

The default is a CAP instead — `min(14rem, (100% - 16px) / 2)`, half the container's line less
the gap — so a pair exactly fills the row where twice 14rem would not fit, and where the line
already seats two at 14rem `min()` hands 14rem back and nothing on a wide surface moves. **Only
the default is capped**: `--ns-field-width` (`ns-field-narrow`, `ns-field-money`,
`ns-field-wide`) is a field answering its own content rather than the line and means that width
at every container size, which is what a label too long for half a line asks for instead of a
shorter word — and, at `ns-field-money`'s 10rem, what a block of money figures read together
(Cerrar Caja's four) asks for so the line seats all of them instead of leaving the last one a
row of its own.

It stops at **400px** — two `Grow` bases plus the gap, the narrowest line on which a pair still
reads. Under that the cap lifts and the band is one field per row: the phone, where half a line
is not a field. Neither edge could be a viewport media query — an aside is narrow on a wide
desktop, which is why `ns-container` exists.

## Unsaved changes

- **The guard fires only when the form's own hosting surface actually leaves.**
- **A custom dirty source's unmount RETIRES its report automatically** — not merely spends it.
  Without that, an abandoned dirty editor haunts every later screen of the session with an
  enabled save and an exit prompt nobody earned; and a job card that reprices and refits before
  telling the surface files its report after `Crear` has navigated away, so a report arriving at
  a surface with no page to spend it would ask the next screen about changes nobody made.
- **The editors' reports are spent in the submit itself** — before the handler runs, put back on
  a refusal — so a save that closes its surface is not a rule each page must remember (a create
  page that forgot left its popup standing over a receta it had just saved).

## Size, and the aside that covered the menu

A full-screen aside (Nueva OT) covered the menu, on desktop too — and the menu is how you leave
the document, so an overlay painting over it takes away the app's only exit. The cap is one rule
in `ns-mud.css` on `.ns-drawer` because it is the rule for every size, and the gutter it leaves is
read from the nav drawer's own declared width, never restated.

## The dialog that stopped taking its shape from content

A tabbed form in a dialog resized on every tab change — a fixed width against a `min-height` the
content could still grow past, the one surface taking its shape from content. Rejected: the
dialog scrolling its content — **the dialog never scrolls its content; scrolling is `NsPanel`'s
job, not the frame's.** Width and height are the same kind of property, written through custom
properties (`--ns-dialog-width`/`--ns-dialog-height`) so the phone override wins on ordinary
cascade order instead of fighting an inline style. The height ladder (240/300/360/440/520/600)
is fixed px chosen to read as a fraction of the screen per step — its aspect drifting across the
steps is accepted, not an oversight.

## The two z-index halves

In a layout that mounts a header, `DrawerClipMode.Always` plus
`.ns-drawer.mud-drawer.mud-drawer-clipped-always` say the docked geometry twice on purpose, so a
variant MudBlazor's own `clipped-*` selectors do not list still lands below the header; with no
header the aside runs full height (`ClipMode.Never`). `NsDialog` is `ZIndex`'s one reader. Below
the docking breakpoint MudBlazor ranks a temporary drawer and the backdrop it paints for it as a
pair against its app-bar tier — splitting the pair puts the sheet under its own backdrop.

## The backdrop that left the DOM when a lookup opened

A lookup's dropdown inside a route modal took the modal's backdrop away while the list stood, and
closing flickered it back. Measured, not inferred — the element was **removed from the
document**, neither hidden nor painted over. `MudOverlay` renders through
`<SectionContent SectionName="mud-overlay-to-popover-provider">`, `MudPopoverProvider` holds the
one `SectionOutlet` that receives it, and a Blazor section shows its **last registrant alone**:
the overlay `MudAutocomplete` mounts with its dropdown displaces every earlier one. The opt-out is
the vendor's own and carries no CSS — `Class="mud-skip-overlay-section"`, read by
`MudOverlay.RenderOutsideOfSection`, which keeps the backdrop in `NsDialog`'s own tree where
nothing can displace it. **Never fixed by lowering the popover tier** — an open list inside a
modal has to keep painting above the backdrop and stay clickable, and that tier is one number on
purpose.

## The title bar is main's chrome

A title strip under a bar that also names the screen says where the user is a second time and
pays a whole row for the repetition — so the shell draws no bar, and the page's own title bar
is the one row that names it. The carrier is `SurfaceContext`, so the component decides by
where it lives and no page was touched. **An OVERLAY's row is the exception, and it is the same
carrier**: the page's bar announces and draws nothing there, because that row is declared inside
the page's `NsForm` and would have waited for its first read — which is a blank box with no way
out for as long as the read takes, and no backdrop to click in a routed modal. The host draws it
instead (`NsSurfaceChrome`), naming the surface off the routed type it was handed until the page
announces its own word, and still no page was touched. **The announcement is its own event, and that is not
decoration**: `SurfaceContext.StateChanged` re-renders the page, so a page whose render announced
would announce from the render its own announcement caused — forever. `AnnouncementChanged` has
exactly one kind of subscriber, the chrome, and the loop cannot close. The clear rides
`ResetView`, or a page carrying no title bar would inherit the previous page's name. The
record-state switch lives in the form as a normal field, and `NsTitleBar` has no `Enabled*`
parameters.

## Only what is marked scrolls — the failure a missing scrollbar hides

`MudMainContent` deliberately carries no `overflow-y-auto`: an ambient scroller at the shell's
edge cannot repair boxes squeezed further in — it only drags their spilled ink into view, which
is how a broken dashboard once read as a working one. The trap by shape: a tall child inside a
`d-flex` parent pinned to the viewport (`flex: 1 1 0%` + `min-h-0`) does **not** overflow —
`align-items: stretch` hands it the parent's height, a grid sizes its auto rows against that
definite height, and every row is compressed and paints across its neighbours. The cards look
chopped and overlapped, not cut off at the bottom. The cure is never a scrollbar further out; it
is a parent whose cross axis does not stretch (a column stack), marked as the owner.

## Page lifecycle: why `OnInitialized` is sealed

The wiring cannot be lost to a forgotten `base.OnInitialized()` — the compiler rejects the
override.

---

# Refusal placement

## The counter sale whose refusal vanished

Being *rendered* — not being a real property — anchors an issue, because the opposite failed in
the wild: a counter sale bound its tenders through a page-local list, the handler's issue named
the message's real `Tenders` property, and the refusal attached to an input that never existed —
it vanished whole and the sale just sat there.

## The client-side refusal that reached for an alert of its own

Nuevo Turno's cadence check is a rule over two values the screen holds, so it never sends:
`CreateSeries.Until` weighed against the start. Drawing it as an `NsAlert` — beside the field,
then in the footer — was measured in headless Chromium with the real stylesheets: the footer is
34px clean or with the *Nothing to save yet* hint and **50px** with the alert. A 16px jump on
every appearance and disappearance, moving Save under the cursor at the instant it is aimed at —
the one thing the placement doctrine exists to prevent.

The channel already exists: `SubmitEventArgs` inherits `Problem` from `AsyncEventArgs`, and
`Runner.Raise` reports whatever the handler leaves on it — so `args.Problem = …` inside
`OnSubmit` is the whole of it (`ChannelVerifyForm`, `SaleLinesEditor`, `AccountsStep` use it).
The doctrine names the channel because "at the foot, as one alert after the form's content" reads
like an instruction to render an alert.

The price is timing, and it is the right one: the alert re-evaluated the rule on every keystroke,
the refusal answers Save. That is the contract of every other form-level refusal, and a rule that
changes its mind while the user is still typing the value it judges is noise, not help.

## Nothing reserves, and what arrives is read in full

**No reservation.** A padding sized for one message on every control box was spent whether or
not anything was ever wrong, on top of the grid's row gap — the fewest patches possible is the
aim. Rejected on the way: a tighter reservation (cosmetic — it keeps the double spend) and a
floating tooltip (occludes what is under it, poor on touch and to a screen reader).

**No clamp.** A one-line anchor with an ellipsis, the full text revealed on hover or Tab, was
dropped after measurement on test.optical.nsail.ar: Nueva Venta's "Una venta se le hace a
alguien: el Tercero es obligatorio." in a 232px box with a `scrollWidth` of 325 read "Una venta
se le hace a alguien: el Ter…". A sentence worth writing is longer than the field it names, so
the clamp meets every refusal, and the half it cuts says what to fix. A reveal of words already
on screen answers the pointer and abandons the phone, the surface where every field is narrow.

**Push-on-error is the price**: the row grows, the fields beside it keep their tops (`.ns-grid`
is `align-items: flex-start`) and nothing above it moves, because the vendor's control box is a
flex column and the message is its last item. The alternatives are worse — a box wider than the
field can only be drawn over the field beside it, and a second surface for the same words reports
them twice. The grid's foot padding went with the absolute placement: a message in flow grows the
grid that holds it, so nothing hangs past its bottom edge for a panel's scroller to clip. So did
the hint's yielded box, for the mirror reason — with both tenants in flow, a hint kept invisible
holds an empty line open beside the message it stepped aside for.

**The screen reader is served the same words.** The container the eye reads is named by nothing,
so `.ns-field-described` carries the same words where an assistive technology can be pointed at
them, and the input is handed that node's id with everything else the field splats onto it
([ui/fields.md](ui/fields.md)).
