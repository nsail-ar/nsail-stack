# Where the cursor lands on arrival

What happens to the keyboard focus the instant a navigation lands — as opposed to what happens
to it when a form opens (forms.md, *Focus on open*) or a dialog/drawer does (same page, same
rule: the first field of what just opened, and back to what opened it when it closes).

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

The actual focus happens in `ns.js` (`arrivalFocus`), for three reasons a comma selector cannot
cover:

- **An element rendered after the first paint** — a list still reading, a tab's panel — is not
  a lost cause: not finding the marker right away starts a `MutationObserver` (bounded by a
  timeout that falls back to `h1`) instead of giving up on the spot, which is what left the
  vendor's own first-render-only `AutoFocus` unable to reach it (the Notes on nsail#2338: a field
  inside a panel not yet shown).
- **A navigation between two pages sharing a layout** (the same routed component, new
  parameters) still fires: `NsArrivalFocus` compares `RouteData` BY REFERENCE, which Blazor's
  router hands it fresh on every navigation and keeps identical across a render the page caused
  for its own reasons — so this runs once per arrival and never again on a re-render.
- **Something the person already reached for is never stolen** — only for the deferred half
  (the wait for a late marker, or the eventual `h1`): the first, synchronous try always wins,
  exactly as today's unconditional `h1` focus does, but the moment this call starts WAITING for
  something that is not there yet, a hand that got there first keeps what it took.

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
- **A read-only page, a dashboard** — nothing declares the marker, so the router's fallback
  (`h1`) is the arrival focus, same as every page got before this.

The per-screen table — which of these six a screen actually is, and which needed a line
changed — is the walk in the story's own PR, not repeated here: a table copied into a doc rots
the day a screen is added, and this page states the rule it was walked against.

## Touch

A coarse pointer's own keyboard rises the instant a text box takes focus, over the very list
the cursor was just handed to see — a cost a device with a physical keyboard, or a pointer that
only hovers (a mouse, a trackpad), never pays. `arrivalFocus` skips outright wherever
`(pointer: coarse)` matches (the same query `ns-mud.css` already gates touch-sized targets on)
— **the router's own pass never fires on a touch screen**, marker or fallback alike.

This is the arrival marker's rule alone. A create form's first-field focus (forms.md) is a
different mechanism — `NsForm.focusFirst`, not `arrivalFocus` — and is not part of this story:
changing it is a separate call, with its own cost to weigh (a sheet being filled in is the one
thing on screen, not a list the keyboard would cover).
