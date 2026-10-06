# Intentional UI — case law

Why the rules in [intentional-ui.md](intentional-ui.md) are what they are: rejected designs,
measured vendor facts, and the Stack-internal mechanisms that make a rule hold.

**Nothing here is a rule.** Read a case before proposing to *change* a rule — not before building
a screen. Sections carry the same names as the core doc's headings and the [`ui/`
shelf](ui/README.md) pages; a rule marked *(cases)* has its story under the matching section
below. The catalog is split by area:

| Section | Cases in it | File |
|---|---|---|
| `As` is the semantic pivot | Why derivation lives in wrapping components · Why `NsSubmit`/`NsClose` stay their own components · Why `GetTitle()` is a method · The exit, and the dialog that ends in Cerrar · Main acts and the footer · A field nobody will type is not a field · One arrival, one loading — and the handoff that keeps it true · Icons are chosen, never defaulted | [controls](intentional-ui-cases-controls.md) |
| Fields | Why `MailAddressField`'s label always floats | [controls](intentional-ui-cases-controls.md) |
| Combos, lookups, filters | Why `MinCharacters` is 0 at the vendor · Why the open list is floored at the field and capped at a measure · Why the cascade crosses the popover · Why the create entry restates its own geometry · The one case where creation does leave the dropdown | [controls](intentional-ui-cases-controls.md) |
| Files | The logo tab that proved one field is not two | [controls](intentional-ui-cases-controls.md) |
| Contributed actions | The toolbar's one icon size | [controls](intentional-ui-cases-controls.md) |
| Hosts | `NsTimeGrid` · `NsDashboard` · `NsListEditor` · The way in nine row editors could not have withheld · Why an act declares that it writes · Why a menu declares for its face and a row for its act | [hosts](intentional-ui-cases-hosts.md) |
| Tabs | What "keeps the panel alive" actually renders · Why a tab derives its problem mark · Which screens bind the tab to the query | [hosts](intentional-ui-cases-hosts.md) |
| Grids: column importance, the action cell, and state | The action cell's hole · The pager row at phone width · The stacked open row is a form · The open row stacks where its container cannot seat it | [hosts](intentional-ui-cases-hosts.md) |
| Surfaces (aside / modal) | Why a dialog re-provides `RouteTable` · Reusing `NsSurfaceContext` · Why `[SupplyParameterFromQuery]` cannot work · Why a route constraint beats a parse · Why a target is never dropped for want of a cascade · Why a surface takes a history entry · In a popup, a successful save closes the surface · The form loads what it will overwrite · Form density (grid fraction, 14rem cap) · Unsaved changes · Size, and the aside that covered the menu · The dialog that stopped taking its shape from content · The two z-index halves · The backdrop that left the DOM · The title bar is main's chrome · Only what is marked scrolls · Page lifecycle | [surfaces](intentional-ui-cases-surfaces.md) |
| Refusal placement | The counter sale whose refusal vanished · The client-side refusal that reached for an alert · Nothing reserves, and what arrives is read in full | [surfaces](intentional-ui-cases-surfaces.md) |
| Navigation menu | Why a menu leaf names a page type · Why the menu asks the page · Settings groups · The group of one disarms — and keeps its name | [shell](intentional-ui-cases-shell.md) |
| The wizard | `NsNext`/`NsBack` · A claim is a fixed point, not a veto | [shell](intentional-ui-cases-shell.md) |
| Localization | Menu entries and titles are Title Case | [shell](intentional-ui-cases-shell.md) |
| Branding and theming | The brand/theme handoff | [shell](intentional-ui-cases-shell.md) |
| Layout and styling rules | Why `NsContainer` stacks by default · Why `NsSubmit`'s label defaults to pinned · Why collapsing is two classes · The bubble a collapsed control whispers · Container queries: the measured facts | [shell](intentional-ui-cases-shell.md) |
