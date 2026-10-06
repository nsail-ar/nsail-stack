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
| `NsMenu` | the menu: a trigger (`Icon` or `Content`, named by `Label`, sized by `Size`) and a body |
| `NsMenuItem` | a row — an `ActionItem`, so a row is a link or a command and nothing new is modelled |
| `NsMenuBlock` | what the menu *says* rather than offers: an identity header, fine print at the foot |
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
  MudBlazor 9.10.0, `NsMenuTests`).
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
- **A block is not an item**: no click, no tab stop, `role="presentation"` so the list never
  offers an option that does nothing. That is what makes "the version is fine print at the
  foot" a shape the component enforces instead of a caller's discipline.
- **A nested `NsMenu` is a submenu.** The vendor reads the nesting; a caller writes no wiring
  for the second level, and passing `Content` to a nested menu is what would cost it the arrow
  that says it opens one.

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
  MudBlazor 9.5 puts `tabindex`, `role="button"`, `aria-haspopup` and the Enter/Space toggle on
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
