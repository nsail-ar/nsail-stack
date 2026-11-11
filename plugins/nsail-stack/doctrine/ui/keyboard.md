# The keyboard contract

NSail is usable from the keyboard alone, on every screen. This page is what each control owes a
person who never touches the mouse, and the rule that makes it measurable: **a screen is done
when its walk is green.**

## The walk

A product's browser suite walks every page its route table lists — routes come from the table and
`MetadataProvider`, never a hand list — pressing Tab through the whole page, and asserts:

- every interactive element (button, link, field, tab, menu item, row action) is reached, in
  visual order;
- the focused element is visible: a ring that is drawn, not only a `:focus` rule;
- there is no trap: Tab and Shift+Tab leave every region, Esc closes every popover, menu and
  dialog and returns the cursor to what opened it — the same Esc still reaches the surface
  behind, so one over an open list inside a dialog is judged on the surface it leaves standing;
- Enter and Space activate what a click activates.

A screen that fails the walk is not done. A red that is not a one-line fix becomes a story
(`p1`, with the page and the key pressed) and is named in the walker's known list, with its
number, in the product that walks; nothing is silenced.

## The contract per control

| Control | Keys |
|---|---|
| `NsAutocomplete` and every lookup | the box is a tab stop; a browsed lookup opens its list with the first row highlighted, a `Lazy` one shows the create entry alone until something is typed; Arrow Down / Arrow Up move the highlight; Enter picks the highlighted row and closes the list, except on a `Lazy` term with several answers, where the first Enter leaves the list open to walk and the next one picks (one answer is taken by the first Enter); on a lookup's create entry (the last row Arrow Down reaches) Enter opens the create route and picks nothing; Esc closes it and picks nothing; the cursor staying in the box is the browser walk's ([fields.md](fields.md), *keyboard-complete*) |
| `NsSelect`, `NsMultiSelect` | the readonly box is a tab stop with `role="combobox"`; Arrow Down, Enter and Space open the list, Esc closes it |
| `NsDateField`, `NsDateTimeField`, `NsTimeField` | typed in the box; the picker opens from the keyboard and Esc returns the cursor to the box |
| `NsTable` | a sortable heading, a row action and a pager control are buttons or links, never a bare element with a click handler; a clickable row has a key for what the click does |
| `NsMenu`, `NsNavMenu` | the trigger is a tab stop; Enter, Space or Arrow Down opens; a menu opened by key is dismissed by Esc and Tab from inside its list; one opened by a click, from the span around the trigger ([menus.md](menus.md)) |
| Dialogs, drawers, `NsWizard` | focus lands inside on open, stays inside, and returns to what opened it on close ([forms.md](forms.md), *Focus on open*; [focus.md](focus.md) for the return) |
| `NsTabs` | the tab strip is one tab stop; arrows move between tabs |
| Toggles (`NsCheckBox`) | Space toggles |
| `NsPasswordField` | the eye is a button in the tab order and Enter / Space reveal |

An interactive element is a native button, link or field. A `div` with a click handler is not
reached by Tab and does not answer Enter.

## Walked where it can be

The Stack proves in bUnit what the DOM expresses: the keys the vendor binds in Blazor
(`KeyboardContractLookupTests` for a browsed lookup, `NsAutocompleteReaderEnterTests` for the
`Lazy` one, `NsLookupCreateKeyboardTests` for the create entry), and the structure a Tab stop needs — a native element, no `tabindex="-1"`, not
disabled (`KeyboardContractSelectTests`, the sortable heading in `NsTableOrderTests`, a row's actions in
`NsTableActionsChromeTests`, the pager's controls in `NsTablePagerShrinkTests`, the date box in
`NsDateFieldTests`, a menu's trigger in `NsMenuTests`, the toggle in `NsCheckBoxDirtyTrackingTests`). The controls that lack that structure are the Known reds
below, each pinned as it is. A key the vendor reads through its JS key interceptor never reaches
bUnit; those keys, the visible ring and the whole-page order are the browser walk's, in each
product.

## Known reds

| Control | What | Why it stays |
|---|---|---|
| `NsPasswordField` | the eye is outside the tab order | the vendor draws its adornment button with `tabindex="-1"` and offers no parameter for it; a test pins the vendor's `tabindex="-1"` and fails the day it changes; the `p1` story's number goes here |
| `NsTextField` with `Copy` | the copy button is outside the tab order | the same vendor adornment as the eye; the box selects its whole value on focus, so Tab then Ctrl+C copies it; a test pins the vendor's `tabindex="-1"` |
| `NsTextField` with `Camera` | the camera button is outside the tab order, and no key opens the camera | the same vendor adornment as the eye; the keyboard's way to a code is the box itself, typed or a reader's burst, which the camera only stands in for; a test pins the vendor's `tabindex="-1"` |
| `NsTable` | a clickable row is a bare `tr` with a click handler: Tab does not reach it, Enter does not open it | the row click is the vendor's; a test pins the row without a tab stop; the `p1` story's number goes here |

## Owed

Not red, not yet proved: Arrow Down / Enter / Space / Esc on `NsSelect` and `NsMultiSelect`
(the vendor's JS interceptor), the date-and-time and time boxes' structure, opening and
dismissing a menu by key, dialogs and wizards, tabs. Each is the browser walk's or a bUnit
case still to write; none is claimed by the tests above.
