# `NsForm`: the read, tracking, submit, refusal

What a form does on its own once a page wraps its fields in one: read the model it edits, track
edits, guard the exit, submit, and place what comes back.

The rules a screen is designed by — the three refusal placements, unsaved changes being the
surface's business, a popup that closes on a successful save — stay in
[intentional-ui.md](../intentional-ui.md). The record is in
[intentional-ui-cases-surfaces.md](../intentional-ui-cases-surfaces.md) under *Refusal placement* and
*Surfaces (aside / modal)*; a rule marked *(cases)* has its story there.

---

## The read half

**A form loads the model it edits.** `OnLoad` is the twin of `OnSubmit`: the handler answers with
the model (`args.Model`), and the form owns what happens when it does not. Three states, and only
the first two put fields on screen:

- **loaded** — the read answered, and the form renders its `ChildContent` over that model.
- **empty by design** — the handler answered with a *new* model because the document does not
  exist yet (a setting nobody has saved). The blank IS the data, so the form is editable.
- **failed** — the read threw, reported a `Problem`, or answered nothing at all. The form draws
  the refusal and a Reintentar where its fields would have been.

The guarantee is structural, not a check a screen remembers to write: a form with no model renders
no `<form>`, so a save over data nobody could read is never on screen to press. A page keeps
`Model=` only when it already holds the document — a create page's blank message, a filter, an act
a dialog collects. **The test is where the submitted values come from**: one that a read answers
belongs to `OnLoad`, however small (a bill's organization, an order's lines); a model built from
the route, the session or a parameter stays the page's, however much it reads beside it (cases).

- **The read runs once, when the form is created.** A screen whose read depends on a route
  parameter keys the form on it (`<NsForm @key="Id" ...>`): another document is another form, and
  no page writes a re-entrance guard.
- **A save that leaves the page open is followed by the read again** — master/detail, done by the
  form, so no page wires a post-save reload. A popup that closes on save does not read again, and
  neither does a handler that navigated away.
- **A conflict's Recargar is that same read**, so a form with a load half needs no `OnReload`.
- **`Load()` is the public twin of `Submit()`**: a page act that moved the document without going
  through the form (a state transition, a deleted override) asks the form to read it again.
- **`Document` is what the form is editing, from whichever half produced it** — the `Model` a page
  handed in, or what `OnLoad` answered with. It is null while a read runs and after one that
  failed, which is exactly when the form renders no fields and no submit. A test that reaches for
  the edited values reads `Document`, never `Model`: on the read half the parameter is empty.
- **What a page loads beside the form — lookups, lists, cards — stays the page's business.** The
  handler runs on the page, so it fills those on the way; none of them is what Submit writes.
- **A `Model=` the page hands in is filled in `OnCreated`, before the first render — never after
  an `await`.** A vendor control (MudBlazor's pickers) keeps its own copy of the value it first
  drew: an assignment that lands after the children rendered leaves the model right and the screen
  wrong, on the WebAssembly client only (prerender finishes every await before emitting HTML, so
  the server hides it). Values that need a read go through `OnLoad`. No universal
  `StateHasChanged` covers this — Blazor would ship one; a page that needs a re-render asks for it.

---

## Focus on open

**A create screen's document, the moment it appears, hands the cursor to its first rendered
field** — so typing can start at once instead of a click into the page first. A hidden candidate
(a field on a tab `NsTabs` has not switched to) and a field the screen LOCKED are skipped. The
first field can be a select or a lookup: what says "locked" is `aria-readonly`, the word
`NsFieldBase` writes for every read-only field, and never the plain `readonly` attribute — a
select's own input carries that unconditionally, because the vendor paints the chosen value into a
text box nobody types into, so reading it would walk the cursor past every select-first create
screen there is. Blazor registers `focus` — unlike `focusin` — as one
of its non-bubbling events: it listens for it on `document` WITH CAPTURE, the instant any
`@onfocus`-bound element first renders, so nothing on the bubble phase and no later capture
listener can run ahead of it. `ns.js` wins that race the only way possible — a `document` capture
listener of its own, registered once, unconditionally, by the script tag `NsApp.razor` places
ahead of `blazor.*.js` in `<head>`, so it is already there before Blazor's own ever gets added —
and `focusFirst` arms it only for a lookup, whose `OpenOnFocus` would otherwise react to this
focus exactly like a click and open its popover over the page. The signal for that is
`aria-autocomplete="list"`, not `role="combobox"`: a select publishes the role too and opens on a
mousedown instead. A plain field's own `onfocus="this.select()"` (`NsFieldBase`) never
loses the event, because the swallow is never armed for it to begin with.

**In a popup the cursor is placed twice.** The hosted dialog wraps its content in a focus trap
that puts the cursor on its own non-interactive fallback div when it opens, and it does so AFTER
the form's call — so a create form in a dialog took the cursor and lost it in the same tick, and
the vendor's `DefaultFocus` option does not withdraw that gesture. `focusFirst` takes it back on
the next animation frame, which is after the trap, and only where something actually moved it: on
a page nothing does and the second pass is a no-op.

- **Whether a screen counts as "create" is derived, by two independent signals, either enough**
  — never a parameter a page sets, so the behavior lands on every create screen without a line
  written for it:
  - **The model's own name** (`Create{Entity}`, messaging.md's closed verb set). This is also
    what catches a create screen whose blank model still needs a server round trip before it can
    render (`OnLoad` answering a NEW model, not reading an existing one — `CreateAppointmentNotePage`).
  - **A tracked form handed its model directly, with no read behind it at all** — the newborn
    document itself, regardless of what verb named it. This is what reaches a message outside the
    closed verb set that still mints one (`RegisterClient`, `RegisterPatient`, `OpenTicket`,
    `RegisterStockMovement`, a page's own untransmitted shape like `QuickJournalEntryPage`'s
    `QuickEntryModel`) without a line written for it either — the next one of these needs no
    sweep, because the signal is the shape of the form, not its name.
- **`FocusFirstField` overrides the derived answer** for the two ways both signals still get it
  wrong: a `Create*`-shaped model reused to edit something that already exists (`UpdatePartyPage`
  locks `Kind` and reads through `OnLoad` — the name outvotes the second signal and still needs
  telling), and an `Untracked` form that still mints a document in a dialog
  (`RegisterPractitionerForm` — `Untracked` reads as "holds no document" to the second signal, and
  this one does). A repeatable inline form that is not a screen of its own (`PriceListItemAdder`,
  inside a browse screen, `Create`-named) turns it off outright rather than fight the re-focus on
  every visit.

---

## Tracking and the exit guard

`NsForm` reports its edits to the cascaded surface (`SetDirty`/`SetUnchanged`), and each
`NsSurfaceContext` guards navigation with a `NavigationLock` (`ConfirmExternalNavigation` covers
tab close and refresh). How a submit spends those reports:

- **A tracked submit clears the flag before the handler runs — its own and every editor's on the
  same surface**, since those values are part of the document it is saving. Another FORM's report
  stands, because a surface can hold two documents.
- **The save window opens BEFORE that spend.** The surface going clean is an announcement
  (`StateChanged`, which `NsPage` turns into a render), and an editor re-entered by that render
  reports again at once; a window opened after the spend would not exist yet, so nothing would
  hold that report and nothing would ever spend it.
- **The window brackets the handler, it does not merely precede it.** A report filed while the
  submit runs is HELD, because the document is frozen for the length of it — what arrives in there
  is the save's own echo, a render the handler's publishes provoked re-entering an editor that
  reports the surface. The window has to cover the handler because the handler is where the
  navigation is: every create page ends its submit on what it just wrote, and an echo taken
  mid-window would have the exit guard protest that very departure over a document already on
  disk.
- **The window belongs to the CALL that opened it** — handed back by `BeginSave` and closed by
  passing it to `EndSave`, never looked up by document. Two submits overlap on one surface (a
  second `Guardar`, the untracked `Probar` beside it) and on one DOCUMENT as well, since nothing
  disables the plain button beside a `Guardar` and the second call unwinds through a `finally` of
  its own. Each closes the window it opened and reads nothing another one is holding. An editor
  names no document it speaks for, so its report is held by every submit in flight and each of
  them answers for it with its own outcome.
- **A submit that ends refused puts back exactly what it spent, and what its window held with
  it** — through either door, a reported problem or a bare `Abort()` — so the surface is left as
  dirty as the submit found it and no edit anybody made goes unguarded; a reported problem
  re-marks the form itself besides.
- **An echo is dropped only by a save that took, and only its own.** A report handed back into a
  window by another submit's refusal is that window's to return, not to spend — it neither caused
  it nor saved it, so the report waits there (an open window is a save about to navigate, and
  taking it would protest that departure) and reaches the surface when that window closes,
  whatever its outcome.
- **`Untracked` opts a filter or search form out entirely**: an untracked submit spends nobody's
  report but its own, opens no window and closes none, and never gates its `NsSubmit`.

**The guard asks about what no open window answers for, never about `HasChanges` alone**
(`SurfaceContext.HasUnansweredChanges`). Where in a save's life a report was filed — before its
spend, inside its window, handed in by somebody's refusal — is not a fact a departure can read,
and an unforeseen ordering must not surface as the operator being asked to abandon a document
already on disk. A report a window reaches is that save's, and the save is about to navigate; a
report no window reaches is somebody's unsaved work and still protests, which is why the second
form's edit is still asked about when the first one's handler leaves the screen. With nothing
saving, the two answers are the same one.

- **The guard fires only when the form's own hosting surface actually leaves** — opening an
  aside over a form is internal navigation. **A named surface leaves when the PATH of its own
  route changes** — the route its query key carries, the address bar's path for the main
  surface: `modal=a/1 → modal=a/2` is the modal leaving, so it protests before the replace
  lands — a create opened from inside a create is asked, never refused. Cancel keeps the
  surface and everything typed into it; confirm replaces (`SurfaceContext.Departs`).
- **A dialog is asked the same question, and Cancelar means cancelled there too.** A
  host-managed surface has no route to keep representing, so following a link out of one
  closes it — but the close is spent on `LocationChanged`, never on the line after
  `NavigateTo` (`SurfaceContext.Follow`): the guard *awaits* its question, so a close fired
  before the answer arrives tears the form down while the person is still reading the protest.
  Declining keeps the dialog and the address; confirming closes it once and follows.
- **That route's own query is filter or component state, never identity** — on a named surface
  as on the main one. A screen writing its own `?tab=` or `?search=` through `SetQuery` has not
  moved: the guard stays silent, and `NsSurface` keys its content by the same path, so the page
  keeps everything typed into it instead of remounting. A page whose identity genuinely lives in
  its query has no way to say so, deliberately.
- **A value echoed into a field from the model is not an edit.**
- **A component that mutates state outside `NsForm`'s field tracking is its own dirty source**:
  it cascades `SurfaceContext?` and calls `SetDirty(this)`. **No page spends that report** — the
  submit beside it spends every editor's on its surface before the handler runs, and the unmount
  **retires** whatever a page left with an edit still in it: spent, and no later report from that
  instance taken, because a gesture still in flight files its report after its component is gone
  and no page is left to spend that one (cases). A refused submit therefore hands nothing back to
  a source that unmounted while the handler ran, and none to one that spent its own report in
  there. The `ClearDirty()` every editor exposes drops a report outside a save; after one there
  is nothing left to drop, and a page that calls it there is writing a line that does nothing.

**Work nobody is waiting on reports to the other half of the guard, `SetPending`/`ClearPending`,
and arms the browser's confirm alone** (`SurfaceContext.HasPending`). A fire-and-forget send is
not a document being edited: the person emptied the box and walked off on purpose, so the in-app
prompt asking them to abandon it would be the very wait the fire-and-forget removed — which is
why this report stays out of `HasChanges` and out of `HasUnansweredChanges`, and why no save
window answers for it. A tab close is the one departure that drops the work, so it is the one
that asks. The report is counted by source and **its filer owns both halves**: it outlives every
page on the surface, which is the point (a queue that survives leaving the screen), so nothing
retires it on an unmount and a report never cleared guards every later screen forever.

---

## Closing

**The close is the form's, never wired per page**: `NsForm` closes its cascaded surface itself
when a submit succeeds. **The question is the surface's, not the tracking's** — a routed overlay
is a *place*, so only a tracked document's finish closes it (an `Untracked` filter in an aside
stays open); a *dialog* is a question, so a successful act closes it tracked or not. **Success is
the form's verdict, not the dirty flag**: a reported problem keeps the surface open, and
`SubmitEventArgs.Abort()` closes nothing. The only opt-out is `KeepOpen` (cases).

`NsForm.Submit()` runs the whole pipeline from outside the form's chrome and answers whether it
took — which is how a wizard step submits a form it did not draw a button for
([wizard.md](wizard.md)).

**A button that drives it says so: `<NsButton Submits …/>`.** That is what stops it being
pressable while the form is already saving — a second press re-enters the `Runner` and can only
earn its refusal — and it is opt-in so that a button beside it that submits nothing keeps
working while the save runs ([actions.md](actions.md)). A form that is `Disabled` is the other
word entirely and refuses both of them, save or no save. The hero needs no word; `NsSubmit`
reads the same running state for itself.

**Chrome standing OUTSIDE the form gets that word through the surface**, since no cascade reaches
it: `NsForm` reports what it refuses after the render that decided it, and a dialog's header X —
drawn above the hosted component in the vendor's own tree — reads it there
([surfaces.md](surfaces.md), [actions.md](actions.md)).

---

## Placing a refusal

`ApplyProblem` owes one contract: an issue naming a field **this form renders** draws under that
field; an issue naming anything else — a member no field bound, a rule name, or no source at all
— draws in the form's problem line. A problem carrying no issue draws its title there.

**The contract is a piece, not a method of the form** (`RefusalPlacement`): it is the field
tracker and the placement together, because only a field that announced itself may carry a
message. A second host with fields on screen when a refusal arrives — `NsListEditor`'s open row —
places its own with it rather than writing the rule again ([hosts.md](hosts.md)). **A placement
lifts what it has standing whenever the `EditContext` it posted to is validated**, and lets go of
it when its own host leaves the screen: a store's messages outlive the store, and one posted on
someone else's context is a validation failure that context's owner cannot reach. **What the lift
would leave unanswered is settled before it**: a host holding a row open registers on the form
(`IFormRows`), and the submit runs that row's own commit on the current values before it validates
anything — a row nothing refuses closes and the save goes on, a refused one draws its reason and
ends the submit there (intentional-ui.md, Refusal placement).

- **Being a real property is not what anchors an issue — being *rendered* is.** A field announces
  the identifier it binds (`IFieldTracker`, cascaded by `NsForm`, forwarded by `NsTab`), and only
  an announced field can carry a message. **A screen that renders its members as fields needs no
  wiring, and there is no page-level "show the error" parameter to forget** (cases).
- **A member edited through composed controls is anchored by `NsFieldRefusal`, and that is the
  one thing a screen hands over.** A member no field binds — an attendee list drawn as a panel of
  rows, whose rows are `PartyRef` while the refused member is a `List<Guid>` — has nothing to
  announce it, so its refusal falls to the foot wearing the Stack's generic "Valor inválido",
  naming neither the reason nor the place. The editor places an `NsFieldRefusal` (a field with no
  input: [fields.md](fields.md)) and the PAGE writes its expression against the model member
  (`For="() => _model.AttendeePartyIds"`) — `FieldIdentifier` resolves the object the member hangs
  off, so an expression written inside the editor would name the editor and match nothing. An
  editor with two mounts needs it at both: the one handed none keeps the foot strip.
  **The anchor carries the member's own client rule as well as the server's refusal**: a
  DataAnnotation on that member posts against the same identifier, so a rule the message mirrors
  from its handler (`[AttendeesRequired]` over Bookings' `RequireAttendees`) says the same
  sentence in the same place with no round trip, and the handler never runs.
- **A field reserves nothing, and the message it draws is read in full** — nothing is spent while
  the field is clean, and the message that arrives stands in flow under the field and wraps to as
  many lines as its sentence needs. The refused field is taller than its neighbours and the row
  pays for it downward, so nothing above moves and the fields beside it keep their tops
  ([fields.md](fields.md), cases).
- **The form-level refusal reserves nothing.** The panel footer holding the buttons claims it
  (`IFormProblems`) and seats it as the flexible left member of a row that already exists, so the
  right-anchored buttons never move. A form no footer claimed draws a fallback strip after its
  content, only while there is something to say (cases).
- **A refusal the screen decided itself uses the same channel and lands in the same places.**
  Every handler's args carry a `Problem` (`AsyncEventArgs`): the `Runner` that raised the handler
  reports whatever was left on it, so `args.Problem = …` inside `OnSubmit` reaches `ApplyProblem`
  by the path a thrown one takes — no `@ref`, no page parameter, and **no alert a page draws for
  itself**. It clears with the rest, since `HandleSubmit` opens with `ClearProblems`. `Abort()`
  is the same refusal with nothing to say (Closing); a rule anchoring to nothing the form
  renders names ITSELF (`BusinessProblem.RuleViolation(rule, message)`) and falls to the foot.
- **A refusal raised by an act the form only HOSTS lands in the same places.** A page act
  standing inside a form — the `Probar` in the `Guardar`'s own row — runs on the PAGE's `Runner`
  (`NsPartial.GetAction`), so its `Problem` reaches the surface and not the form, and `NsAction`
  is where the press and the form around it are known at once: standing in a form is a cascade
  (`IFormProblems`). The surface carries that claim for the length of the call
  (`SurfaceContext.BeginAct`) — the one object both ends hold, as it already is for the word a
  dialog's X reads — and `NsPage` offers the problem to the claimant last, just before the toast,
  so a page that answers its own problems still answers first. The form places it with the single
  copy of the placement and takes none of a submit's other meanings: a hosted act edited no
  document, so no report is handed back and no wizard step reads it as a verdict. **The last one
  comes down at the next press of an ACT, not at the end of the act that worked** — an act that is
  refused reports a `Problem` and one that works reports nothing, so silence is only legible
  backwards, and a press that sends nothing (`NsHelp`'s `?`) resolves nothing and takes nothing
  down. **The seat is one**: a submit drawing its own refusal ends the hosted act's claim to it
  (`Draw`), so what the person reads about `Guardar` is never spent on a later probe.
  **The bound is the cascade**: an act outside every form keeps the toast, and so does one drawn
  on the far side of a portal (a menu row), where no cascade of the form's reaches.
- **`OnReload` is the one act a refusal may offer** — wired to the screen's own first read, drawn
  beside the `Conflict` message and never under it, because a second line would move the buttons.
- `NsForm` carries `NsDataAnnotationsValidator`, so `[Required]`/`[MaxLength]` speak under their
  fields without a round trip; the pipeline evaluates the same attributes for every caller
  (messaging.md) and its issues name the PROPERTY, which is what lands the server's refusal in
  the same places as the client's. **Every attribute says the same sentence at both ends**: the
  screen hands the failing one to the wire's own switch (`MessageValidator.IssueFor`) and draws
  the catalog's row for the code it mints, so the whole BCL vocabulary is worded once. A rule
  outside that vocabulary earns a sentence of its OWN by naming its problem code
  (`ICodedValidation`, messaging.md); one that names none gets the generic word — what no
  refusal ever shows is an attribute's `ErrorMessage`, which no catalog can translate.
- **The browser is not one of the refusal's placements**: the form is `novalidate`, so the HTML
  `required` a marked field hands the input is read by an assistive technology and by nothing
  else. Without it Chromium's own constraint validation stops the press in its own English
  bubble, before Blazor sees the submit (`NsFormRequiredTests`,
  `CalDavConnectDoorTests` in Therapy's E2E). **`required` is not the only gate it withdraws**:
  an `input` of a typed kind is refused natively for its FORMAT whatever its `required` says, so
  a field that renders one owes that rule itself through `GetOwnProblem` (fields.md, which carries
  what owning a kind's gate costs, and how many sentences it owes) — which is where
  `NsUrlField`'s own refusals come from, and why a box whose rule is its binding's declares it
  (`NsEmailField` over an `[EmailAddress]` member).
