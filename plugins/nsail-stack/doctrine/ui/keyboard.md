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
  dialog and returns the cursor to what opened it;
- Enter and Space activate what a click activates.

A screen that fails the walk is not done. A red that is not a one-line fix becomes a story
(`p1`, with the page and the key pressed) and is named in the walker's known list, with its
number, in the product that walks; nothing is silenced.

## The contract per control

| Control | Keys |
|---|---|
| `NsAutocomplete` and every lookup | the box is a tab stop; focus opens the list with its first row highlighted; Arrow Down / Arrow Up move the highlight; Enter picks it and closes the list; Esc closes it and picks nothing; the cursor stays in the box |
| `NsSelect`, `NsMultiSelect` | the readonly box is a tab stop with `role="combobox"`; Arrow Down, Enter and Space open the list, Esc closes it |
| `NsDateField`, `NsDateTimeField`, `NsTimeField` | typed in the box; the picker opens from the keyboard and Esc returns the cursor to the box |
| `NsTable` | a sortable heading, a row action and a pager control are buttons or links, never a bare element with a click handler; a clickable row has a key for what the click does |
| `NsMenu`, `NsNavMenu` | the trigger is a tab stop; Enter, Space or Arrow Down opens; Esc and Tab dismiss from the trigger the list was opened from ([menus.md](menus.md)) |
| Dialogs, drawers, `NsWizard` | focus lands inside on open, stays inside, and returns to what opened it on close ([forms.md](forms.md), *Focus on open*) |
| `NsTabs` | the tab strip is one tab stop; arrows move between tabs |
| Toggles (`NsCheckBox`) | Space toggles |
| `NsPasswordField` | the eye is a button in the tab order and Enter / Space reveal |

An interactive element is a native button, link or field. A `div` with a click handler is not
reached by Tab and does not answer Enter.

## Walked where it can be

The Stack proves in bUnit what the DOM expresses: the keys a component handles itself
(`KeyboardContractLookupTests`), and the structure a Tab stop needs — a native element, no
`tabindex="-1"`, not disabled (`KeyboardContractSelectTests`, `NsPasswordFieldEyeTests`). A key
the vendor reads through its JS key interceptor never reaches bUnit; those keys, the visible
ring and the whole-page order are the browser walk's, in each product.

## Known reds

| Control | What | Why it stays |
|---|---|---|
| `NsPasswordField` | the eye is outside the tab order | the vendor draws its adornment button with `tabindex="-1"` and offers no parameter for it; the test is skipped and names this row |
| `NsSelect`, `NsMultiSelect` | open / close by key is not walked in bUnit | the vendor reads those keys through its JS interceptor; the browser walk owns them |
