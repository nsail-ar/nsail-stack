# Where the cursor lands on arrival

What happens to the keyboard focus the instant a navigation lands — as opposed to what happens
to it when a form opens (forms.md, *Focus on open*) or a dialog/drawer does (same page, same
rule: the first field of what just opened — or a button, when the question has nothing to type
into — and back to what opened it when it closes).

## The marker, not a selector list

`NsRouter` wraps Blazor's own `FocusOnNavigate`, which takes one plain CSS selector and nothing
else — `FocusSelector`, "h1" by default, which is why a person who opens a list has always had
to click into its search before typing. A comma list ("`[data-ns-arrival-focus], h1`") is not
the fix: `querySelector` answers the first match in DOCUMENT ORDER, and a page's `h1` always
comes before whatever a screen marks, so the marker would lose the one time it mattered.

`NsArrivalFocus` (replacing `FocusOnNavigate` in `NsRouter`) checks the marker on its own first,
and falls back to `FallbackSelector` ("h1", so a screen reader still hears the page's title)
only where there is none — **the marker always wins when it exists.** `AutoFocus` — the
parameter every `NsFieldBase` field already carries — is one source of it: `true` writes
`data-ns-arrival-focus` on the field's own input (`NsFieldBase.SetInputAttributes`), the same way
it already writes `data-ns-field` for the Enter policy. Nothing else needs to spell the
attribute by hand.

## The fallback is a heading something has to draw

A selector finds nothing that nobody draws: the fallback is **the screen's own title row**
(`NsTitleBar`, through `NsText As="PageName"`), and it is the ONE row that is the screen's
subject — the row that claimed the surface, the same claim the hidden drawer's toggle rides
(`SurfaceContext.ClaimsRow`). A section's row, a table's title and an overlay's row stay
`Title`: **one title row names the screen**, and a second top-level heading would name it
twice. A heading inside the CONTENT is not one of those rows and is not governed here — a guide
chapter's `#` renders as an `<h1>` of the markdown's own (`Markdown`), and what keeps the
arrival on the screen's name is document order: the title row is drawn before the content it
heads.

The heading also carries `tabindex="-1"`: `focus()` moves the cursor only onto something that
can hold it, so a bare `<h1>` leaves the call a no-op — the same price the shell's skip link
pays for the main region it lands in (`NsMainLayout`). It is no tab stop, so `ns-mud.css` draws
no ring around it, and the rank is the element alone: the title row's type scale is unchanged
(`MudText.HtmlTag` separates the two).

The actual focus happens in `ns.js` (`arrivalFocus`), for three reasons a comma selector cannot
cover:

- **An element rendered after the first paint** — a list still reading, a tab's panel — is not
  a lost cause: not finding the marker right away starts a `MutationObserver` (bounded by a
  timeout that falls back to the screen's name) instead of giving up on the spot, which is what
  left the vendor's own first-render-only `AutoFocus` unable to reach it (the Notes on
  nsail#2338: a field inside a panel not yet shown).
- **A navigation between two pages sharing a layout** (the same routed component, new
  parameters) still fires: the arrival is read off the address, not off the page type or the
  component's identity.
- **Something the person already reached for is never stolen** — only for the deferred half
  (the wait for a late marker, or the eventual heading): the first, synchronous try always wins,
  as an arrival's unconditional focus does, but the moment this call starts WAITING for
  something that is not there yet, a hand that got there first keeps what it took. Asked as
  "has the cursor moved since we landed", so a tab strip, a clickable grid row or a tree node —
  places a hand deliberately goes, none of them a control — are kept too.

## An arrival is a change of path

`NsArrivalFocus` fires on a new **path**, and on nothing else. Blazor's router hands down a
fresh `RouteData` on every location change, and **a surface written over the address the person
is already standing at — an aside, a dialog, a tab, a filter — is not an arrival**
(navigation.md, the same rule that keeps `NsNavMenu` from recounting on one): nobody went
anywhere, so nothing may move the cursor out of what the hand is doing. The reference to the
previous `RouteData` is still the first question, because it is what keeps a render the page
caused for its own reasons free; the path (`SurfaceContext.StaysInPlace`'s own question) is the
second. The cost of asking only the first: a screen writing its own tab or filter onto the
address takes the cursor back, and on a coarse pointer — where the fallback's pass is the one
that runs — the keyboard closes over the very box that wrote the query.

**A fragment is the same answer**: the shell's skip link and a guide section link are same-path
moves (`NsMainLayout`, guide.md, *Anchors*), and an arrival firing on one hands the cursor back
to the screen's name — the exact place the link was pressed to get away from.

## By kind of screen

- **A list's search is its arrival focus.** `NsPageHeader`'s own `NsSearchField` carries the
  marker by default (`ArrivalFocus`, on NsPageHeader) — a page using the built-in search writes
  no line for this. `ArrivalFocus="false"` opts a screen out whose first act is not searching
  (a scope selector on the left, a screen that reads too slowly to search nothing in while it
  does); a page supplying its own `Search` fragment decides for itself, by marking a field or
  not.
- **A create or edit form's first field is `NsForm`'s own rule** (forms.md, *Focus on open*) —
  structural, derived from the model's shape, and untouched by any of the above: it scans the
  form for the first typable control outright, not for the marker, and a repeated inline editor
  (`PriceListItemAdder`) already turns it off on its own terms.
- **A repeated act with one working box** — `ProductScanField`'s barcode field, a sale's
  "Buscar producto" — marks itself with `AutoFocus` directly and re-arms every scan by changing
  its own `@key`, which is a first-render trick entirely its own and not the router's concern:
  it already works today and stays exactly as it is.
- **A dialog or a drawer keeps the form's own rule**: the first field of what just opened, and
  the vendor's focus trap hands the cursor back to whatever opened it the moment it closes — no
  line written for either end. **A dialog with nothing to type into — a yes/no question — opens
  on a BUTTON instead**, and which one is the question's own answer, because it is what a bare
  Enter presses: the act it offers, or the way out when the act destroys something (forms.md,
  *Focus on open*).
- **The companion region recalled open on load is not a door opening.** Above the drawer's own
  breakpoint the page stands beside it, so this page's own arrival focus is left to settle;
  below it the region IS the screen, and takes focus the way a drawer that opened for a reason
  does. A door's own `Toggle`/`Open` always takes it, docked or not. Reference: `NsCompanion`.
- **A read-only page, a dashboard** — nothing declares the marker, so the router's fallback is
  the arrival focus: the screen's own name, the `h1` its title row draws. A screen with no field
  to mark still announces itself to a screen reader, which is the whole point of the fallback;
  a screen that renders no title row of its own gets no arrival focus at all, and is the one
  shape to fix at the row rather than here.

The per-screen table — which of these six a screen actually is, and which needed a line
changed — is the walk in the story's own PR, not repeated here: a table copied into a doc rots
the day a screen is added, and this page states the rule it was walked against.

## Touch

A coarse pointer's own keyboard rises the instant a text box takes focus, over the very list
the cursor was just handed to see — a cost a device with a physical keyboard, or a pointer that
only hovers (a mouse, a trackpad), never pays. That cost belongs to the BOX, so wherever
`(pointer: coarse)` matches (the same query `ns-mud.css` already gates touch-sized targets on)
`arrivalFocus` skips **the marker's pass alone**: the fallback still fires and the screen's own
name takes the cursor, because a heading raises no keyboard and a person reading a phone with a
screen reader is owed the name of the page they just landed on.

This is the arrival marker's rule alone. A create form's first-field focus (forms.md) is a
different mechanism — `NsForm.focusFirst`, not `arrivalFocus` — and is not part of this story:
changing it is a separate call, with its own cost to weigh (a sheet being filled in is the one
thing on screen, not a list the keyboard would cover).
