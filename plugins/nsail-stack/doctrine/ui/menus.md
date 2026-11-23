# The anchored menu

The contracts of `NsMenu`, `NsMenuItem` and `NsMenuBlock`, and of the app-bar entry that opens
one.

When a menu is the right surface and a modal is not — and why a menu is not a surface at all —
stays in [intentional-ui.md](../intentional-ui.md), under *Surfaces*. The contribution
mechanism these rows are modelled on is [actions.md](actions.md).

---

## The components

| Component | Is |
|---|---|
| `NsMenu` | the menu: a trigger (`Icon` or `Content`, named by `Label`, sized by `Size`, `Disabled` when its list has nothing to say yet) and a body |
| `NsMenuItem` | a row — an `ActionItem`, so a row is a link or a command and nothing new is modelled |
| `NsMenuBlock` | what the menu *says* rather than offers: an identity header, fine print at the foot — or the field it holds, a menu's own search |
| `NsMenuDivider` | the rule between two families of rows, for a list whose separation position cannot draw — the toolbar's kebab emits it where `ActionItem.Group` changes ([actions.md](actions.md)) |

**A menu holds items and blocks, and it has no opinion about their order** — which block opens
a menu and which closes it is the screen's decision. The rule that separates a block from the
rows is drawn by position in `ns-mud.css`, so neither end needs a divider a caller remembers.

### Rows

- **A row writes no role: the whole widget is the vendor's.** MudBlazor renders `role="menu"` on
  the list, `role="menuitem"` on every row a menu holds — a labelled submenu's activator row
  included — and `aria-haspopup="menu"` + `aria-expanded` + `aria-controls` on all four activator
  shapes, the custom `Content` face among them. Anything written from this side could only
  disagree with the list the row lands in, so `NsMenuItem` writes nothing (measured against
  MudBlazor, `NsMenuTests`).
- **`Selected` is the one thing a row still says for itself, and unset is an answer.** A
  `menuitem` carries no state at all, so a row that is one of a single CHOICE answers
  `menuitemradio` + `aria-checked` instead. The parameter is `bool?` for that reason: **unset is
  a verb** (sign out, a destination) and belongs to no choice, while `false` is this row standing
  among the alternatives and losing — a sign-out that claimed `false` would be offered as one of
  them. The row in force draws a **trailing** check — an `aria-hidden` glyph that announces
  nothing on its own; trailing, because a check in the icon gutter would indent the chosen row
  past its equals.
- **`NsMenuItem.Label` overrides the derived label for a row whose text is DATA** — an
  organization's name — rather than a verb the string catalog names. Everything else about the
  row (`Actions.{Name}`, the destination page's title, the icon, `Disabled`) is `ActionItem`'s
  own derivation.
- **A block is not an item**: no click, no tab stop of its own, `role="presentation"` so the
  list never offers an option that does nothing; a field it holds keeps its own stop. That is
  what makes "the version is fine print at the foot" a shape the component enforces instead of
  a caller's discipline.
- **A nested `NsMenu` is a submenu.** The vendor reads the nesting; a caller writes no wiring
  for the second level, and passing `Content` to a nested menu is what would cost it the arrow
  that says it opens one. Its row stands on a line of its own like any other: the vendor's rule
  for a nested menu reads it as a child of the list, and the span `NsMenu` wraps every menu in
  stands between them, so `ns-mud.css` restates that rule past the span — without it several
  submenus flow in a line and widen the menu toward the window's edge.

### A form that refuses writes

**A menu is a way IN, so a form that refuses writes takes it away.** A row reads its own
`ActionItem.Writes` ([actions.md](actions.md)) and the face reads `NsMenu.Writes`; both default
to *this writes*, so a kit names nothing and a menu inside an `NsForm` that is `ReadOnly`,
`Disabled` or frozen for the length of a submit is withheld **face and all** — a trigger that
opens a list of withheld rows is a button that does nothing.

- **The face carries a word of its own because nothing can count the rows first.** The body
  does not exist until the trigger is used (below), so *is anything left in here* has no answer
  at render. `Writes="false"` says the rows answer one by one, and the two menus the toolbar
  draws are its callers: the contributed act's own word forwarded, and the kebab, whose rows
  passed the same gate before the cap counted them ([actions.md](actions.md)) — asking them again
  at the face would take the overflow away from a read-only row that still has links to offer.
- **The menu carries the form's word across the vendor's portal.** A body renders under the
  popover provider, a sibling of the router, so nothing `NsForm` cascaded reaches a row on its
  own — the same hole a row's surface already fills with `RootSurface`. `NsMenu` re-cascades
  `ParentReadOnly`/`ParentDisabled` around the body, being the one component that stands on
  both sides of it, and `NsActBase` answers inside exactly as it does on the page. The two
  names that split the form's word apart — `FormDisabled`/`FormRunning` — do not cross, so
  inside a body the combined one is the only word there is ([actions.md](actions.md)). **A
  `Disabled` form greys the trigger by itself** — MudBlazor reads those two names too — and
  greying is what a value does: a way in is withheld.

### The trigger

- **A `Content` face owns its click, and owns it as a method of the component.** It is the one
  trigger that can be seated inside something already clickable — an option row in a lookup's
  open list ([fields.md](fields.md)) — so the activator stops the click there and the surface
  behind never learns of it. A face seated that way lives inside a popover, which re-renders
  around it while it is being used, and Blazor keeps an element's event handler id only while
  the old and new delegates compare equal: a lambda over the vendor's per-render `MenuContext`
  is issued a new id by every render, so a toggle the caller holds is aimed at a handler the
  renderer has retired (`UnknownEventHandlerIdException`, intermittently). The menu holds the
  vendor's component by reference and toggles it from a method of its own; the activator's
  `context` goes unused.
- **The face's tab stop is the vendor's wrapper, and the wrapper needs a BOX to be one.**
  MudBlazor puts `tabindex`, `role="button"`, `aria-haspopup` and the Enter/Space toggle on
  the `div.mud-menu-activator` it draws around a `Content` face, and then gives that div
  `display: contents` — which Chromium skips in sequential focus navigation and refuses
  `focus()` on, leaving the trigger with no tab stop and a click on it focusing `<body>` with
  nothing for the arrow keys to move from. `ns-mud.css` makes the wrapper a flex item instead,
  transparent to the width chain the chip and the seated row both run through it. **The face
  itself carries no `tabindex` and no `role`**: the wrapper is the button, named by the face's
  own `aria-label` through name-from-content, so a second one nested inside it is one element
  too many for a screen reader and ambiguous to a strict locator. Only a `Content` face renders
  that wrapper — an `Icon` trigger is the vendor's own `<button>`, a direct child of `.mud-menu`.
- **`Beside` opens the menu at the trigger's trailing edge** instead of under it, for a
  trigger that IS a row: under it is where the next row already stands, and a body drawn there
  covers the very row it belongs to. A submenu the vendor recognizes as one is already placed
  this way and says nothing.

---

## The list stays the trigger's, and Escape and Tab dismiss it

- **The list stops at a measure, the same one a long lookup list stops at.** A list left to
  grow outgrows the room under its trigger, and the vendor then turns the whole panel over to
  the other side: sixteen rows came to 552px and were drawn from the top of the page down to a
  trigger at its foot, which reads as a list belonging to nothing. Capped, a long list scrolls
  under the trigger it was opened from. A caller that finds its list scrolling has an ordering
  question to answer — which rows are worth the first screen — not a measure to raise.
- **Escape and Tab dismiss the list of a menu the POINTER opened, and that is read off the
  span around the trigger.** The vendor answers both keys on the list's own wrapper, inside the
  popover, and hands the cursor into it, to the list's first row, only where the gesture that
  opened the menu was itself a keystroke — so after a click the cursor is still on the trigger and
  neither key is answered anywhere. A lookup's offer opens its menu by that click for this reason
  ([fields.md](fields.md), *keyboard-complete*). The span (`ns-menu-keys`, `display: contents`, so it is not a box) is the
  first ancestor on the bubble the house owns. Tab belongs there beside Escape because leaving
  the trigger IS a dismissal: the next field takes the cursor, and a list still standing over it
  answers for nothing.
- **Escape and Tab from a control a block holds close the menu, and that is read off the
  block.** The vendor answers both on the list's wrapper only while the menu holds a row, so a
  menu that is a search alone (`NsMenuBlock` around the field) would stand open through every
  key until something typed drew one. A Tab between two controls of one block closes it too, as
  the vendor's does in a menu with rows: a menu is left by Tab, never walked by it, so one shape
  of menu does not answer the key two ways. A control that answers Escape itself — a lookup's
  open list — stops it first. Its stop holds every key, Tab too, so the lookup hands a Tab it
  stopped to the block it sits in (`NsMenuBlock` cascades itself): the list closes on the Tab
  and so does the menu, since the cursor leaves both. In a menu with rows the vendor closes too,
  and closing a closed menu is absorbed.
- **A menu a key opened with no row takes the cursor on its first control, and Escape gives it
  back.** The vendor hands the cursor in only to a row, so a submenu whose body is a field alone
  (a lookup, opened by Arrow Right or Enter on its row) or a menu that is its own search would
  open with the cursor left on what opened it — a parent's arrows walking on under a submenu left
  standing. ns.js moves it to the popover's first control once the opening key is up (a lookup
  answers Enter's key up by opening its list), and when an Escape closes that menu — the block's,
  above — puts it back on the trigger or row it was opened from, where the vendor's own return
  leaves it for a menu of rows. An Escape a list open inside answers leaves both where they are.
- **The cursor in a control a block holds spends the vendor's keyboard opening.** Armed, the
  vendor hands the cursor to the first row at the next render that finds one, so rows a menu's
  own search draws while somebody types would take the cursor from it and the keys after with it.
  `NsMenuBlock` clears the vendor's private mark when a control of its own takes focus
  (`MudVendorContractTests` pins the name); only an arrow then moves the cursor onto the rows.
- **The innermost open thing wins Escape**, so one Escape closes one thing and the next one the
  surface ([surfaces.md](surfaces.md)): a dismissal that also threw away the form under it would
  make the universal gesture for a list a way to lose work. `NsDropdown`, `NsHelp`,
  `NsExpander` and `NsAutocomplete`'s list stop the key's propagation on their own wrapper **only
  while open** (`@onkeydown:stopPropagation` bound to their state); closed, they let it through.
  The lookup's wrapper is a `display: contents` span around its box, bound to the open state the
  vendor reports (`OpenChanged`), and it closes nothing itself: the vendor closes the list on the
  Escape's key up, after the keydown the surface listens to has been stopped. **The others close
  on the next turn (`await Task.Yield()`), never in the Escape's own dispatch**: Blazor reads the
  bound stop after the handler returns, so a same-dispatch close reads `false` and the same Escape
  reaches the surface behind. A new surface whose stop is bound to its state and that closes
  itself owes the same yield; one whose stop is literal does not. `NsTextField`'s camera is that case: its wrapper
  exists only while the camera is open, so the stop is written literally and the close runs in
  the Escape's own dispatch. That stop cuts every key bubbling to a Blazor ancestor, not only
  Escape, which is harmless because the house's other keys (the dropdown's Enter and arrows) are
  ns.js's native listeners. Inside a hosted
  dialog the dialog's Escape is the vendor's native listener, which no Blazor stop reaches, so the
  six report their open state up and `NsOpenDialog` withdraws `CloseOnEscapeKey` while any is
  open ([actions.md](actions.md)). **A menu the pointer opened is the one exception**: the vendor
  publishes no open state to bind the stop to and a flag of the house's own would drift from it,
  so its Escape closes the list and still reaches the surface behind.
- **An outside click is already the vendor's**, through the overlay it draws for an open menu;
  the house adds nothing there.
- **`Disabled` is the menu offered and not yet answerable**, for a list with nothing to say YET
  because the screen around it is half filled — a comparison of what each bench charges, before
  a lens is on the job. The trigger stays, named and greyed, because withholding it takes the
  knowledge that the comparison exists along with it. It is **not** the answer for a menu nobody
  may use: that one is withheld whole, by `Writes` or by what its rows are authorized for.

---

## Nothing renders until the trigger is used

A menu's body is the vendor's popover content, so **the body does not exist on a page nobody
opened the menu on**. That is a load-bearing property, not an optimization: a menu whose
content has to be read (the session menu's organizations) mounts its component — and therefore
runs its `NsLoad` — when someone asks, never on every page load.

**And the mirror of it: choosing a row closes the menu, which unmounts everything that drew
it**, a kit's whole menu body included.

- **A row's command is a BARE handler, never `NsPartial.GetAction`.** That helper wraps the
  call in a run of the offering component's own, and that component is already gone: a `Runner`
  whose component has ended runs nothing and says nothing ([surfaces.md](surfaces.md)), so the
  row does nothing, silently. **`NsMenuItem` supplies the run itself**, on
  a host the render tree does not own, so the work outlives the menu — but that host falls back
  to the root surface, which nothing subscribes to `OnProblem`, so **a refusal reaches no
  screen**. A row whose handler can be refused catches its own `BusinessException` and answers
  with an `Alert`, the same rule an offer's own write follows ([fields.md](fields.md)). The link
  half has no such rule: `GetLink<TPage>` is exactly right.
- **A read whose result the menu shows goes in an `NsLoad` inside the body**; what needs no
  read stays outside it. A sign-out is the example — it is the way out of a session gone
  wrong, so it must be offered on a backend that just refused to answer.

---

## The toolbar's menu entry

`ActionItem.Menu` is the third mode beside `PageType` and `OnClick`: **a contributor offering
a CHOICE rather than a destination or an act**. `NsActionToolbar` renders it as an `NsMenu`
whose trigger is the item's own `Icon` or `Content`.

The fragment is a plain Blazor primitive for the same reason `Content` is one — **the
contributor's own kit builds it, and what it builds is its own component**, which loads its own
data and is named nowhere in the Stack. `SessionChip` is the exemplar — the chip at the foot of
the nav drawer that anchors `SessionMenu`: it names the trigger and nothing about what the menu
holds.

The toolbar's own overflow kebab is the other rider — the same `NsMenu` and `NsMenuItem`, with
the items the cap pushed out of the row. Both of the toolbar's menus declare `Writes="false"`:
what a form withholds fell out where authorization did, before the cap, so a second filter at
the face could only take away what already passed one.

---

## `NsDropdown`: a panel the host opens

A menu opens from its own trigger; `NsDropdown` is opened by its host from code — results hung
from a plain field the user is typing in. `Open` (two-way with `OpenChanged`) is the only state;
`Anchor` is the field and `ChildContent` the panel, rendered only while open.

- **The panel is the vendor's popover, not a box inside the wrapper.** Drawn inside, it is
  clipped by the nearest scrolling ancestor (`NsPanel`'s content) and grows that scrollable area
  for as long as it shows. As a popover it hangs from the anchor's bottom-left edge, at least as
  wide as it, stops at the same measure a menu does and scrolls past it, and is turned over
  where the room under the field runs out.
- **The wrapper is transparent to layout.** It is the flex item the field was and can shrink
  under its content, so the field's `Grow` still pays.
- **The panel writes no role; the wrapper marks the list open.** The rows are the host's, so a
  role written here could only disagree with them. A plain field cannot be handed
  `role="combobox"`, so while open the wrapper carries `data-ns-dropdown-open` and `ns.js` reads
  it as it reads `aria-expanded`: Enter over the panel never submits the form, and inside
  `.ns-cells` the arrows are not the cell cursor's ([fields.md](fields.md)). The field must
  carry `data-ns-field` (every `Ns*` field does).
- **It stands on both sides of the portal, so it carries across what a row asks of the page.**
  The panel renders under the popover provider, where nothing cascaded from the page reaches:
  the dropdown re-cascades the surface, `ParentDisabled`, `ParentReadOnly` and the
  `EditContext`, as `NsMenu` does, so an `NsLink` with a `Target` resolves from the surface the
  field stands on and an act inside a read-only form is withdrawn.
- **The panel names its own tier, one above its catcher.** The click-outside catcher sits one
  tier under the popover ceiling (`SurfaceContext.VendorZIndex - 1`), and the vendor script seats
  a popover from a tier it reads before the theme writes it — under the catcher, where the first
  click on a row would close the panel instead. So the panel carries `z-index` at the ceiling
  itself; `NsHelp` builds the same pair and does the same.
- **Closing is the dropdown's.** A click outside lands on an overlay one tier under the popover
  (as `NsHelp`'s), and Escape from the wrapper or from inside the panel does the same: both set
  `Open` false and tell the host through `OpenChanged`, and neither reaches the surface around the
  field while the list was open. The overlay covers the field while the
  panel shows, so a click on the field closes it.
