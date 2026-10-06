# The navigation menu

How a kit offers menu entries, how an app decides what its drawer says, and what the menu
does with them. The offer-and-mount rule it opens with is one rule for two surfaces — a kit's
dashboard cards are mounted the same way, and the cards' own host is
[hosts.md](hosts.md).

What belongs where in the tree — entries ordered by frequency of use, domain groups first and
system groups last, a leaf that disappears when its page is denied — stays in
[intentional-ui.md](../intentional-ui.md). The record is in
[intentional-ui-cases-shell.md](../intentional-ui-cases-shell.md) under *Navigation menu*; a rule marked
*(cases)* has its story there.

---

## The model

**The kit offers, the app decides: a menu in `Add<Kit>Menu()`, a home's cards in
`Add<Kit>Dashboard()`, both apart from `Add<Kit>Components`.** One rule over the two surfaces a
kit reaches into an app's shell, written here once and pointed at from
[hosts.md](hosts.md) (`NsDashboard`) — composing a kit buys its screens, its strings and its
messages; where the app's own drawer and its own home open is the app's word, kit by kit and
never all-or-nothing.

`NavMenuItem` (`NSail.Components/Navigation/`) is a flat model — `Name`, `Icon` (a `Glyph`),
`PageType` (+ optional `Parameters`) for leaves, `Items` for groups, plus the four fields an
override carries — `Weight`, `Parent`, `Visible`, `Permission`; **no Razor**. A kit implements
`INavMenuContributor` (async, re-asked whenever the authentication state changes) and registers
it in `Add<Kit>Menu()`. `NavMenu` (same folder, scoped) asks the mounted contributors and merges
in registration order; `NsNavMenu` renders that tree and prefix matching drives the active
highlight. The app layout just places `<NsNavMenu />`.

- **The drawer is the frame's one gutter, and the app's map is not always alone in it.** Below
  the drawer's own breakpoint `NsNavMenu` also draws whatever the surface's page announced as its
  index (`SurfaceContext.AnnounceIndex`), above the map and never instead of it — that is where a
  screen whose rail cannot stand beside its content collapses to, exactly as the nav itself does.
  The guide is today's one such screen ([guide.md](guide.md)).
- **The mechanism for the group order is weight bands**, so contributors compose without
  coordinating: domain groups 10–50, system groups 80+.
- **A page missing from the route table throws at startup** rather than 404-ing on click
  (cases).
- **The gate is the destination page's own authorize attributes** (cases). A page with no gate
  stays visible — the floor is "signed in"; stacked attributes all apply; a group goes when
  every one of its children went, bottom-up, and a leaf naming no page counts as gone. The menu
  is rebuilt on every change of the cascaded authentication state; asked before the session
  resolves, every entry answers "denied". **`PageGate` remembers each verdict for the session
  that earned it**, because every caller asks from inside a render and none can remember on its
  own — a gated link re-asks on every parameter set, so one pass over a full list of rows asks
  for the same handful of pages dozens of times, each ask a reflection walk plus a policy
  combination plus an authorization call on the client's one thread. A verdict is a function of
  the page and the session and of nothing else, so the session is the only thing that earns a
  fresh ask.
- **An entry may name a second condition, and only a second one**: `NavMenuItem.Permission` /
  `NavMenuEntry.Permission` is a message type, ANDed with whatever the branch below already
  answered and never a replacement for it. It exists for the one case the page cannot answer —
  **two doors onto one page**, where the page's gate is the same for both and only the door
  differs (Optical's Mi Cuenta > Turnos and Contactología > Turnos, both `AppointmentsPage`) —
  and for a **group**, which is otherwise never asked about itself and so survives any session
  that legitimately holds one child. It takes the LAST non-null value in the merge like `Visible`,
  so an app tightens a kit's door by contributing the same `Name` carrying only this; it hides
  the DOOR and not the route; and it is a message, never a role name read in code
  ([permissions.md](../permissions.md)). An install's stored row carries none, for the same
  reason it carries no `PageType`: arranging a drawer is not authoring a grant.
- **Two doors onto one page are told apart by the open section.** The active mark and the
  expansion follow the longest path match, then the most specific query (*Personas* bare,
  *Pacientes* with `RoleIds`), then the section the person has open. Where the first two tie the
  URL holds nothing that distinguishes the doors, and the branch they are working in is the only
  thing that does: the door they touched keeps the mark and its section stays open instead of
  handing the screen to the other one. Arriving on the route cold, with nothing expanded, the
  first door in the tree takes it.
- **`NsIcons.Progress` is the odd catalog entry**: hand-drawn, needs no transform, animates
  itself (`.ns-spin`). Adding any other icon is the `new-icon` skill.

---

## Mounting and arranging

**The kit plants, the app arranges.** A kit is a tool, not a plugin
(kits.md (nsail: `docs/agents/domain/kits.md`)): it publishes an arrangement, the app either mounts it
(`services.AddSchedulingMenu()`, `services.AddSchedulingDashboard()`) or leaves it unmounted —
**and having mounted it, the app renames, reorders or hides what it took.** An override is a
contribution carrying the same `Name` and only the fields it changes; `Merge` takes the last
non-null `Weight`, `Icon`, `Parent`, `Visible` and `Permission`, so **the app's contributor is
registered after every kit's** — the `AddNavMenu<Menu>()` call sits at the foot of `Browser.cs`'s
mount list, not inside `Add{App}Components`, and `NavMenuMountTests` pins it in both roots. A kit
entry the app never names stays exactly as the kit put it. Optical mounts Scheduling and wears it
as **Contactología** — where the section sits and its own *Medidas de Adaptación* inside it are
Optical's `Menu.cs`; renaming needs no field, because the label is `NavMenu.{Name}` and the
app's strings already win by registration order. **The net is a
drawer snapshot per product** (`DrawerSnapshotTests`, order, nesting and visibility): a kit's
new door reddens CI instead of surfacing in a release.

- **`Parent` moves an entry, `Visible` hides one, and neither deletes anything.** A `Parent`
  override lifts the entry out of where it was contributed and merges it into that group's
  children, so an app reaches a leaf the kit nested and not only the doors it planted at the
  first level; it can move an entry INTO a group, not back out to the first level, since null
  is "no opinion" and not "the root". `Visible = false` takes the entry and its whole branch
  out of the drawer, and leaves it in the tree — `NavMenuItem.Find` still answers, so the
  pages under a hidden door keep the glyph on their own title bars. A `Parent` nobody
  contributes throws, the same call the arrangement makes about a name declared twice.
- **The cards go the same way.** `DashboardItem.Merge` unites contributions by `Name` on both
  dashboard hosts: `Weight`, `Icon`, `Tall`, `Visible` and `Eligible` take the last non-null,
  `CardType` and `PageType` the first, so an override names the card and one field — `CardType`
  is not `required` for exactly that — and an app may tighten or replace a kit's `Eligible` the
  same way. `Visible = false` DROPS the card here rather than marking it: a hidden nav entry
  stays in the tree because `Find` still answers for the pages under it, and a card off the home
  has no second reader. A contribution carrying no `CardType` that nobody plants a card for
  throws naming it — the drawer cannot make that call, since there an override and a new entry
  are the same shape, and the dashboard spends the information it has. The app's own
  contributor is registered LAST for the same reason and in the same place, at the foot of
  `Browser.cs`'s mount list; `Add<Kit>Dashboard()` is still all or nothing to MOUNT, and Optical
  mounts Scheduling's and hides every turnos card (Optical's `Dashboard.cs`).
- **A kit's offer splits by kit, never by contract.** `Add<Kit>Dashboard()` carries every card
  the kit contributes — the home's and the subject dashboards' alike, Accounting's `Dashboard`
  and `Cards` in one call — so an app decides about a kit, not about a surface, and a mount that
  landed the home's figures while dropping the Ficha's cannot be written by accident.
- **An app that opts out pays for the doors it does not repose.** `NsTitleBar` derives a page's
  glyph from the ADDRESS it was opened at: the entry for that address, or the nearest ancestor
  address that has one (`RouteTable.Match` up the path, `NavMenuItem.Find` by `PageType`) — so a
  create, an edit and a detail hanging under a list wear the list's glyph without being given
  one, and a family left with no door anywhere loses the icon on every title bar in it. The walk
  stops before the app root: the home's glyph on a page that derived none would be a default, and
  icons are chosen. Hiding a door costs nothing: a hidden entry stays in the tree and `Find`
  still answers.
- **What a kit contributes into *Mi Cuenta* / *Configuración* keeps riding with
  `Add<Kit>Components`** and is never an app's decision: an app has no business re-declaring
  "Mi Cuenta > Accesos > Google", and those leaves are decided after the kit asks its own
  server. Iam's contributor is two classes for exactly this — `Iam.Menu` holds Seguridad, a
  domain door mounted by `AddIamMenu()`; `Iam.Accounts.Menu` holds Mi Cuenta and rides with the
  components, because the provider kits land their Accesos leaves by naming into that group and
  an unmounted parent takes them with it.
- **Both composition roots mount the same menus, and the same dashboards.** `Add{App}WebApp`
  (prerender) and `Add{App}Wasm` (client) are two lists, and they are one drawer and one home to
  whoever is looking at them: mounted in one and forgotten in the other, a menu changes shape and
  a home gains or loses cards the moment interactivity lands. `NavMenuMountTests` and
  `DashboardMountTests` (Architecture) run both roots over a real `ServiceCollection` and compare
  the registrations.
- **An app's own arrangement is declared as DATA, not composed.** `NavMenuEntry` is the flat row
  — `Name`, `Parent` (a Name), `Order`, `Shown`, plus `Icon` / `PageType` / `Parameters` — and
  `NavMenuEntry.Resolve` hands the merge one item per row, carrying its `Parent`, because
  **`Merge` is what nests it**: a row can only override a kit's entry of the same `Name` if the
  two arrive at one level. `Order` becomes `Weight` and `Shown = false` becomes `Visible = false`
  — the row's answer only when it is NO, so a row that shows says nothing about a kit's own
  decision. **Kits keep returning `NavMenuItem` trees** — this shape is the app's alone.

### The install's arrangement

**An install arranges its own menu, and that is a third layer, not a second app.** **The kit
contributes, the app arranges, the install arranges** — each over the one before it, and the
install's rows are read from storage, so a shop reorders, renames or hides a door with no
release. `NavMenuArrangement` (`NSail.Settings`, `[SystemSettings]`, [settings.md](settings.md))
is the stored shape: `Name`, `Parent`, `Order`, `Shown`, `Icon` and `Labels` (the shop's own word
per language), and **no `PageType`** — an install arranges the doors its modules planted and
cannot invent one that opens nowhere. `INavMenuArrangement` (`NSail.Components`) is the seam,
registered with `AddNavMenuArrangement<T>()`; the settings kit registers the one that reads the
stored row (`MediatorNavMenuArrangement`), so an app that composes no storage has an empty
cascade and nothing to remember. **`NavMenu` applies it after every contributor by
construction**, which is why it is not an `INavMenuContributor`: registration order decides who
overrides whom among contributors, and an install's word must not be able to land under a
module's by being mounted early.

- **A stored row forgives what a declaration throws for.** A row naming an entry nobody
  contributes is ignored, and so is a `Parent` that is gone or that would close a loop: a
  declaration is written against the composition it ships with, and a stored row outlives the
  release that could fix it — a throw there is a drawer no install can reach its own editor
  through.
- **A row says only what it CHANGES**, so an entry the arrangement never names keeps the
  place its module gave it. That is what lets a release add a screen to an install that
  arranged its menu years ago, and it falls out of the merge rather than being remembered.
- **Hiding is not a permission.** `Shown = false` is the same `Visible = false` an app's row
  carries — out of the drawer, still in the tree — and what decides whether the page opens
  stays the page's own authorize attributes. An arrangement orders and hides what is already
  allowed, and it cannot name a `Permission`: the stored shape has no field for one, because a
  shop arranging its drawer is not authoring a grant.
- **The label is stored beside the arrangement, not in a catalog.** `NavMenuItem.Label` wins
  over `NavMenu.{Name}` where a row carries one, per language, with no translation invented
  for a language the shop wrote nothing in. `NsNavMenu.GetName` is the one site, so the
  drawer's search finds a renamed door by the name on screen. A product renaming a kit's door
  for its whole market still writes a string (registration order); a SHOP renaming its own
  door writes data. Optical is both at once: `NavMenu.SettingsScheduling` is a string, and
  Contactología in the drawer is a row (optical.md).
- **It is read in the one ask, with the tree**, so a change lands on the next load of the app
  and never on a release. There is no editor or settings page yet: the door is
  `SaveNavMenuArrangement`, administered like any write, while reading is a built-in grant — a
  drawer is everyone's, and it hides nothing a page's own gate does not already hide. The user
  layer of the cascade — a person arranging their own drawer over the install's — is this same
  seam registered after it, and is not built.

---

## Asking, counting, merging

- **`NavMenu` is the only thing that asks the contributors, and it asks once per session**, and
  the install's arrangement is read in that same ask. A contributor is a module's chance to ask
  its own server — the three sign-in kits each spend an HTTP round trip deciding whether their
  Accesos leaf belongs — so a second enumerator is the cost paid twice, not a second feature. The
  tree it holds is keyed on the cascaded authentication state and shared by the drawer and by
  every `NsTitleBar` deriving its page's glyph; a faulted ask is not kept.
  `NavContributorAskTests` (Architecture) keeps it one seam. **Nothing else may inject
  `IEnumerable<INavMenuContributor>`.**
- **A number beside an entry is a second contribution, not a field on the tree.**
  `INavMenuCount` names an ENTRY — `Name`, the same merge key and the same localization key —
  and answers how many things are waiting behind it; `AddNavMenuCount<T>()` registers one, apart
  from `AddNavMenu` because they are separate decisions: a module may count for a door somebody
  else planted, and an app that mounted the door may still not want the number on it. A count
  whose entry the app hid is drawn nowhere, with nothing on either side needing to know.
  **It is apart from `INavMenuContributor` because the two answer at different rates**: the tree
  is a function of the session and `NavMenu` asks it once for one, while a number is the point of
  being a number — `NavMenu.GetCounts()` remembers nothing, `NsNavMenu` asks it when the drawer
  builds and again on every **arrival**, and a contributor that throws is left out rather than
  allowed to take the map down with it. A surface written over the address the person is already
  standing at — an aside, a dialog, a tab, a filter — is not an arrival and is not counted: a
  round trip per contributor lands inside the window of every save taken in an overlay, and
  nobody went anywhere. The route is still reapplied on it, because a deep-link filter in the
  query is what tells two entries naming one page apart, and whatever moved the work inside the
  overlay says so itself — an alta publishes `NavMenuCountsChanged` next to its own saved event,
  the way the acts that move a ticket already do. Zero draws nothing: a zero is a claim the
  reader has to read to dismiss. Tickets is the only one, and it is Tickets' `OpenTickets` —
  the entry outlived the kit behind it because the count names the entry.
- **`NavMenuCountsChanged` is the third moment, and it is how work that ARRIVES reaches the
  drawer.** A number asked only on a navigation cannot see an act taken in another seat, or a
  message landing while somebody sits on one screen — so anything that knows the work moved
  publishes this UI event and `NsNavMenu` recounts every contributor. It carries **nothing**:
  the Stack owns no count, so an event naming an entry, a module or an id would be the drawer
  learning what a kit counts, and "ask again" is the only reaction honest for every contributor
  at once (the bargain `BooksPosted` and `StockChanged` make, messaging.md). It needs no
  composition — a UI event is published through the Mediator every client already has — and the
  drawer hand-rolls what `NsPartial.Subscribe` would have given it, because the drawer is a plain
  component. Channels' `ConversationWatch` is the first publisher, woken by the server's push
  (messaging.md, Pushed to clients) — the drawer never learns a socket exists.
- **A section with one screen contributes an entry, not a group.** The contributor writes the
  first-level entry itself — the *group's* name, the *leaf's* `PageType` — and the group is born
  the day the section gains its second screen. This is NSail's rule, not a product's: the kit
  flattens its own contribution, so every app that mounts it inherits the flat menu. It is the
  shape, not `Merge`'s fold: `collapseSingleChildGroups` stays the settings tree's alone.
- **Merge is by `Name` at every level, so an entry and a group cannot share one at that level.**
  A merged item keeps the first non-null `PageType` *and* the union of the children, but
  `NsNavMenu` renders a group the moment `Items` is non-empty — the route is silently dropped,
  and `Hide` gates the item by its children alone. A contributor naming its leaf into another
  kit's group is the supported case; naming it at a first-level entry that already has a route
  is the defect. **`PageType` and `Parameters` are the one pair that stays first-non-null**: they
  are what the entry IS, not where it sits, so an override that names no page keeps the kit's.
  The key is per level and not global on purpose — *Calendario* the agenda and *Calendario* under
  Mi Cuenta > Preferencias are two doors, and only a `Parent` naming one of them explicitly
  crosses a level.
- **`NavMenuItem.Merge`** is the opt-in fold: a settings group that comes down to exactly one
  child collapses to that child at the group's position, the child inheriting the group's weight
  but keeping its own icon and label. The fold is born the moment a second member lands under
  the same name, and reverses just as mechanically.

---

## The cuts

**A line in the drawer is one somebody asked for.** It is an item —
`NavMenuItem.Separator(weight)`, `NavMenuEntry.Separator(order)` in an app's arrangement —
drawn as `role="separator"` with no tab stop, the way `NsMenuDivider` does it for action menus.
The stylesheet deduces none: a border above every top-level group could only ever say "a group
starts here" — which the chevron already says — and two mechanisms drawing one line leave the
app unable to remove the one it did not ask for.

- **The contributor that places it is the one composing the menu**, which in practice means the
  app: a kit does not know what its entries sit between. Therapy places exactly one, after
  Inicio; Optical places seven, including the joint at Seguridad.
- **A separator bypasses the merge.** `Merge` unites by `Name` and a separator has none, so two
  of them would fold into one and a line would vanish; they pass through by identity and take
  their place among the merged entries by `Weight`.
- **Nothing gates a separator** — `Merge` keeps every line it was given — but the render below
  still refuses to draw one with nothing on a side of it. `Hide` reads a null `PageType` as
  denied for a leaf, and a separator is not a leaf; it also cannot be what keeps a group alive.
  `Leaves()` and `NavMenuItem.Find` ignore it for free, both keying on `PageType`.
- **A separator goes while the drawer's search box is filtering.** A filtered drawer is a list
  of matches and not the map, so the two families the line stood between are gone; keeping it
  draws a rule between nothing and nothing.
- **The same reasoning applies to what the permission gate leaves behind**: a run of
  consecutive separators draws one line, and one leading or trailing a level draws none. It
  happens at `NsNavMenu`'s render rather than at `Merge`, because the gate is per session and the
  merge is not.
- **A separator carrying a name is a section header** — Material has both, and it is the same
  primitive with a second job. Not built; the shape leaves room for it.
