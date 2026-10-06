# Data access — tenancy and the org axis

Part of [data.md](data.md), which holds the modes (`Tenancy:Mode`), `AddDataAccess` and
`TenancyProvider` (data.md, Tenancy). This page: the tenant wall, the org filter, and how a
tenant is resolved, credentialed, bootstrapped and swept by background jobs.

---

## The tenant is a column EF puts on every entity

Under `SingleDb` isolation is a **predicate, not a connection**, so the mode is three rules the
Stack sets and no product writes:

- **The column and the filter are set over the whole model**, in `ApplySetups`, beside
  `ApplyRowVersion` and `ApplyCaseInsensitiveText`. Every entity gets a **shadow** `TenantId`
  (no entity class names the tenant; `modelBuilder` does) and a query filter comparing it
  against the context's own tenant. Skipped: owned types (their columns are the owner's row),
  derived types in a hierarchy (EF allows one filter, on the root), and install-scoped entities
  (below).
- **The write is stamped from the resolved identity**, in a `SaveChanges` interceptor
  registered by `AddDataAccess` — where the unit of work sits, never in a handler. A payload
  cannot name a tenant: the column is a shadow property, so no message and no entity carries
  it. Even a caller that reached the change tracker itself is overwritten.
- **Every index leads with the tenant**, foreign keys included, on every entity that has the
  column; an install-scoped entity keeps the plain indexes its own foreign keys earn, restored
  by the same pass. A filter is cheap only when its index leads with the tenant; otherwise the
  index is scanned whole and filtered after — the small tenant paying the large one's size.
  This is why a product's context calls `configurationBuilder.ApplyTenancy()` in
  `ConfigureConventions`: it removes EF's `ForeignKeyIndexConvention`, which would otherwise put
  its single-column index back beside each tenant-leading one.

**The primary key does not move.** It stays `Id` alone in every mode; the tenant is a column
BESIDE it. A row is globally unique by its id and the wall is the filter, not the key — two
tenants cannot be handed the same id and rely on the tenant column to separate them. Anything
that MINTS an id for a tenant-owned row derives it per tenant (`TenantKey.For`); a document
that ships fixed ids goes through `TenancyProvider.Owned` before it is replayed (below).

**The model is identical in every mode.** A column present under one mode and absent under
another is a second snapshot and a second migration chain, drifting. So the column is always
there and the filter always compares; only the VALUE compared against varies. A tenant walled
by its **database** (`MultiDb`) carries `RowKey == Guid.Empty`, as `Tenant.None` does under
`None`, so every row carries `Guid.Empty` and the predicate matches everything. Needing an
`IModelCacheKeyFactory` is the sign the model diverged.

**The key is derived from the slug, not stored** (`TenantKey`). A shared database has no
`pg_database` to read and a catalog table would be a third runtime dependency, so the same slug
is the same key in every process, replica and restart — and the proxy stays the whitelist.

**The filter reads the tenant through the CONTEXT** (`ITenanted`, the whole of what a product's
context says about tenancy). A query filter is compiled into the model, built once per context
type, so a filter closing over the tenant itself would bake the first request's answer in for
the life of the process; closing over the context lets EF rebind it per query.

**`IgnoreQueryFilters` is not used, anywhere.** It leaves no trace at the call site; a query
that genuinely spans tenants is a seam nobody has ruled. `TenantAxisTests` holds all of it:
every entity filtered or explicitly install-scoped, every index leading, no call site opting
out.

**An unresolved scope reads nothing, and a write without a tenant throws.**
`TenancyProvider.RowKey` answers `Guid.Empty` wherever the wall is not the column, the resolved
tenant's key where it is — and **`null` where the wall IS the column and nothing resolved**.
Null is the answer: the filter's comparison is lifted, SQL's `= NULL` is unknown, and no row
equals it — not another tenant's, not the seed's unstamped ones. So a background job that
entered no tenant reads NOTHING, where a constant-true fallback would hand it every tenant's
rows. The write cannot answer that way (every value it could stamp is somebody's), so
`TenantStamp` refuses the save. Work that must touch a tenant's rows enters one
(`ResolvedTenancyProvider.Enter`), and **which** tenants it enters is the runner's answer, not
each job's (Background jobs, below).

## The install's own rows, and the one law that decides which they are

The wall's one exception is a marker on the entity's own file: `IInstallScoped` (`NSail.Data`,
beside `IVersioned`). The pass skips a marked entity — no column, no filter, nothing to stamp —
so one planted row answers every tenant, and a scope that resolved none reads it too. Which
entities wear it: [org-map.md](org-map.md), Shared.

**An entity a tenant can WRITE is the tenant's**: the editor's existence is the classification.
Under a per-tenant database the customer already edits such rows freely, so marking one
install-scoped would not share reference data — it would let one customer rewrite everybody's.
That is why `Role`, `Policy`, `Currency` and `VoucherType` are NOT marked: each ships a screen
(`UpdateRolePage`, `UpdatePolicyPage`, `CreateCurrencyPage`, `UpdateVoucherTypePage`) — and why
`Country`, `Province` and `City` are not either: the lookups' quick add writes them. If a marked
entity ever grows an editor, it crosses sides in that same PR.

**The law is directional**: *an install-scoped row may never hold a required foreign key into a
tenant-scoped table; a tenant-scoped row may reference install-scoped rows freely.* Shared
pointing at private is the wall crossed backwards — one shared template naming one shop's
account, wrong for everybody else and reachable from outside its owner. Private pointing at
shared is using the furniture. `TenantAxisTests` encodes **the law, not the resulting list**, so
the next entity that tries the illegal direction fails CI. It is what puts `Account`,
`EntryTemplate` and `VoucherTemplateLine` on the tenant's side: the chart is the shop's own
(`CreateAccount`), so everything that names an account belongs to the shop.

## The org is the other axis, and its map is a page of its own

The tenant is a wall; the **org is a branch inside one**, and it filters far less. Which rows it
filters is [org-map.md](org-map.md): every entity of every kit and app classified once —
org-scoped if it records something that happened AT a branch, tenant-level if the company owns
or defines it, shared if nobody edits it (exactly the `IInstallScoped` set). The same page
classifies every MESSAGE that reads or writes those entities (Messages — where each send
stands): which name a branch, which consolidate over the seat's, what protects a row a send
names by id, and where the axis is declared and nothing constrains it.

## The org filter: a subtree WHERE no handler writes

By-id read sites number in the hundreds (`GetWorkOrder`, the shared `Load(id)` behind every
work-order transition, …), so scoping them is a WHERE set where the model is assembled, never a
hand edit per site. The fourth model-wide pass (`OrgFilter`, beside the tenant column's) puts a query
filter on every entity the map classifies **org-scoped**, and a by-id read from outside the
operation's subtree answers not-found.

- **The classification is the entity's own file**: `IOrgScoped` (`NSail.Data`), the positive
  twin of `IInstallScoped`. The map decides what wears it; the pass reads only the marker.
  `Membership`, `Branding`, `OrganizationChannel`, `OrganizationLocation`, `Relationship` and
  `Asset` carry an organization column and are NOT marked — the column says which branch the row
  is ABOUT, and the rows that define the org graph are read to COMPUTE the scope, so scoping
  them by it is circular. Therapy's `ConsultingRoom` is not an Organization: its column says
  where the room stands, which is the axis, so it wears the marker like `Store`.
- **The pass adds no column.** An entity that records where it happened declares
  `OrganizationId`; a **rider** — a line, a tender, an attendee — declares none and reaches its
  head's through the required reference it cannot exist without (`WorkOrderLine.WorkOrder`,
  `VoucherLine.Voucher.PointOfSale`): the map's *a row that inherits its answer states its
  parent*, made mechanical. So: no migration, nothing to stamp, and **a rider's answer cannot
  drift from its head's** — a stamped copy could, on exactly the rows a background job writes
  (`HorizonJob` materialises appointments under no session).
- **The visible set is a SUBTREE, reached through the context** (`IHasOrgScope`, the org axis'
  `ITenanted`) so EF rebinds it per operation. Read from the root org it is every branch; from a
  branch, that branch's own — the organization selector's law, applied to rows.
- **The subtree is the organization the MESSAGE names, else the session's.** A message carries
  its organization in a `[PolicyField(RestrictAs.Organization)]` field, the policy says which
  values this caller may put there (permissions.md), and the filter reads that branch — a caller
  the gate let ask about B reads B, not an empty slice of A. A message with no such field, or
  with it null or `Guid.Empty`, names none and the session's organization answers — every by-id
  read and every transition. Several fields, or a collection, read the **union** of their
  subtrees. The filter re-checks nothing (it stays plumbing, never a second policy), but it does
  ask the gate what the gate looked at:
- **It follows the field only where the gate READ the field.** A send an implication alone
  allowed (`[Requires]`, permissions.md) met a gate that answers the key and the audience and
  ignores values on purpose, so its organization field carries no permission — the caller's own
  subtree answers. The gate leaves the verdict on the flow (`AmbientAuthorization.FieldsVetted`)
  and the interceptor reads it; where no gate ran at all — a fixture, a reaction it exempts, the
  system's own send — the field is taken, because no value there is unchecked.
- **The subtree is walked once per operation, never inside a query.** The chart only names a
  row's parent, so a WHERE cannot climb it: `RelationProvider.GetDescendants` is `GetAncestors`
  read downward, one level per query, no recursive CTE (shallow trees), and
  `OrgScopeInterceptor` (Iam) enters the answer onto the operation's scope behind the gate — a
  caller the policies refuse pays for no walk. A leaf branch costs one query, the root of a flat
  chart two. No ancestor-path column, no closure table. **The outer message decides**: a send
  nested inside the operation finds the scope entered and asks nothing.
- **A GRANT may say its reads cross the branches, and where the session stands says nothing
  about it** (nsail#1582, ruled 2026-09-25 and 2026-09-28). What somebody holds towards the whole
  company rather than towards one of its counters — Optical's Cliente — is read from every branch,
  and the filter learns it from the policy that allowed the send: `Policy.EveryOrganization`,
  authored on the row (`PolicyBuilder.AcrossEveryOrganization`, the editor's *Reads every
  organization*), carried behind the gate beside `FieldsVetted` and answered with
  `OrgScope.Everywhere`. A message that names a branch anyway is asking for less and gets it.
  **No role is classified and no landing rule decides what anybody sees**: each screen is answered
  by its own policy — the customer's pinned `PartyId == @me` and across the branches, the
  counter's filtered by org — and somebody holding both reads both, each through the grant that
  allowed it, with nothing merged between them (permissions.md, Core doctrine). A row that crosses
  the branches while pinning nothing is a blanket cross-branch grant, which is its author's call.
  Where the session stands still decides where somebody *operates* (`OrganizationHierarchy.PickDefault`:
  the saved preference, else the shallowest membership). The Cliente membership itself is minted
  at the root for the same reason the grant crosses — a Cliente belongs to the óptica and not to a
  counter — and which memberships are of that kind is a **product's** word, never a kit's
  (`IPortalRoleProvider.GetPortalOrganizationId`).
- **Work that stands in no branch reads every branch** (`OrgScope.Everywhere`) — the org axis'
  `Guid.Empty`, not its `null`. A background job, a tenant's first touch, a migration and an
  anonymous request are the company's own work; a job reading no branch's rows would sweep
  nothing and write rows nobody can see. It is the one state a message cannot narrow, whatever
  field it carries. The **tenant** isolates customers; the org scopes access inside one.
- **The two axes are orthogonal and both filters apply.** EF holds one filter per key, so the
  tenant's is named `Tenant` and this one `Org` (EF refuses an anonymous filter beside a named
  one). Behaviour is identical under `None`, `MultiDb` and `SingleDb`: the org filter reads a
  message and a session, not a mode.
- **`IgnoreQueryFilters` stays refused**, and EF 10's per-key form with it: a call naming one key
  could keep the tenant wall and step over the branch scope. `OrgAxisTests` holds the sweep —
  every marked entity filtered, every organization column classified or answered with the map's
  sentence, every required reference into an org-scoped row itself org-scoped, no call site
  opting out.

**A head across a kit boundary is answered by carrying the column, not by an exception.** A
rider whose head the model cannot see states its branch itself: `ArcaAuthorization`'s
`VoucherId` is a soft reference into a peer kit, so the model holds no join to the voucher's
point of sale — it declares `OrganizationId` and is stamped where the row is written, off
Accounting's authorization request, which already knows the branch. No row the map calls
org-scoped is outside the filter; `OrgAxisTests.TheColumnIsNotTheAxis` holds the columns that
are not the axis at all.

**A stamp is not a ride: it does not follow the head when the head moves.** A rider is read
through its head and answers wherever the head stands; a stamped row keeps the branch it was
written at, so a head that changes branch leaves its stamped satellites behind, invisible from
the branch that now owns the work. Whoever moves the head owes that question an answer —
`UpdatePointOfSale` refuses to move a till that still has an issued comprobante no authority has
granted, because after the move the destination is the one branch that may authorize it and the
one branch that cannot read the attempts already made for it.

**An in-use guard that must NOT find a row fails open when it is filtered** (org-map.md, G2): a
delete asking whether any org-scoped row still names the id it is about to remove reads only the
caller's own subtree, so a row standing in another branch answers "free" and the delete hits the
provider's `Restrict` constraint instead of the `InUse` refusal it exists to pre-empt. The guard
is answered where the org axis allows reading every branch, `OrgScopeProvider.ReadEverywhere` —
`Enter(OrgScope.Everywhere)` for the one query, then back to the operation's own scope, never
`IgnoreQueryFilters`. The direction is what decides it: a guard that must FIND a row (a
workshop's store, a voucher's point of sale) fails closed under the same filter and stays as it
is.

**This filter does NOT answer which branch a caller may name.** It reads the message's field;
the gate says whether this caller may put that value there — so a marked field no policy
constrains is a filter pointed wherever the caller likes. Which messages carry the field, which
consolidate without one, and what the shipped policies pin it to: org-map.md's message table,
held by `OrgAxisMessageTests`. The two halves lean on each other on purpose: because this filter
answers an omitted field with the seat's own subtree, a policy pinning that field to
`@memberOfOrDescendant` — the one symbol whose permitted set CONTAINS that subtree — lets the
omission through rather than refusing every by-id site. `@current` and `@memberOf` stay strict,
because there the fallback reaches below what the symbol allows (permissions.md, Null
semantics).

**The party axis** is the other half, answered in org-map.md (The party axis) on purpose. A
membership and a relationship are EDGES the scope is computed FROM, so no model-wide filter can
touch them without circularity; the two lists of one person's edges write the subtree WHERE
themselves, against the set this filter compares to, read from `OrgScopeProvider` rather than
walked again. It is the one place a handler writes this WHERE.

## First touch bootstraps, and onboarding is the proxy's block plus this

Under `SingleDb` there is no database to create and no chain to apply: a new slug arrives at a
schema that is already there and rows that are nobody's. `TenantBootstrap` — the twin of
`TenantMigrator`, over the same advisory lock and per-process memo (`FirstTouch`) — plants what
the app says a tenant is born with (`ITenantSeed`, registered with `AddTenantSeed`), once per
slug. So **provisioning a tenant is writing the Caddy block; nothing else exists to do.**

- **The seed is a chain, and the tenant has a history of it.** `ITenantSeed` is an ordered list
  of named `TenantSeedStep`s and each touch runs the ones this tenant has not, so a step added
  after a tenant existed still reaches it. `AppliedSeedStep` is that history and the Stack's only
  table: one row per step per tenant, written in the SAME transaction as the step's rows, so a
  step that throws is not recorded and is tried again. It has rows only where rows are a
  tenant's; under `None` and `MultiDb` the install's EF chain is the truth and the table stays
  empty. **The memo is per process on purpose**: a deploy restarts the process, so a step that
  deploy added runs on the first touch after it.
- **A step's name is frozen the day it ships**, like a migration id: the history stores it, so
  renaming one makes every tenant run it again. Append, never edit.
- **What a process holds of a tenant's rows in memory is that tenant's, and the touch empties
  it.** A singleton that caches rows keys them by the tenant it read them for (one set per
  process would be whichever tenant loaded first) and registers with `AddTenantCache<T>`
  (`ITenantCache`): every first touch, the migrating one included, calls `Forget(tenant)` once its
  work ran, because a background job can enter a tenant and read before any request touched it.
  Iam's `PolicyStore` is the one there is.
- **What is planted is everything the tenant owns**: the organization, the administrator (party,
  user, `admin`/`admin` login), the membership pointing at the tenant's OWN administrator role,
  and the tenant's roles, policies and currencies. An app composing Accounting adds the
  accounting skeleton — the chart, the voucher types, the entry templates and the voucher
  template lines, re-pointed at its own accounts. The skeleton's shape is one; what hangs on it
  is the app's market (Optical's chart is an óptica's, Therapy's a consultorio's). Every app
  composing Directory adds the geography — every country and Argentina's places
  (geography.md (nsail: `src/Kits/Directory/docs/geography.md`)).
- **The shape comes from the app's own `Seed.*` blocks, never from a second copy** — the
  geography from the kit's own documents, which the migration reads too — and a tenant
  whose skeleton drifted would be a second product. The ids do not: each derives from the
  INSTALL's frozen id (`TenantKey.For(slug, wellKnown)`), so two processes planting one tenant
  write one set of rows and a restart writes the same ones.
- **The migration's own SeedCore rows stay**, because `None` and `MultiDb` need them. Under
  `SingleDb` they carry no tenant, match no filter and are read by nobody — harmless, dropped at
  the next compaction of that seed.
- `ImportStarterPackHandler` therefore asks for the root organization it can SEE rather than
  naming the seeded id: that id is the install's, and a tenant's derives from its slug.

## A frozen id is a row's NAME, and the seam answers it in this scope

The tree names some rows by constant — every id in `SeededRoles.All`, and the accounts and the
currency `AccountingSettings` defaults to. Where rows belong to a tenant, those constants name
the install's row, which the tenant cannot see: the foreign key is satisfied physically, nothing
throws, and the join answers nothing. **`TenancyProvider.Row(wellKnown)` answers "that row, in
this scope"** — the identity where rows are the install's, `TenantKey.For(slug, id)` where they
are a tenant's. It is the function the first touch derives with, so a handler resolving a frozen
id lands on the row the tenant was born with.

- **The set is closed and declared, never discovered.** `WellKnownRows` holds the ids,
  contributed with `services.AddWellKnownRows(...)` beside the constants (`SeededRoles.All`,
  `AccountingSettings.WellKnown`) — where the ROWS are composed, so a host that takes the data
  takes the set. An id outside it passes through untouched, which lets the seam be pointed at a
  stored setting: the customer's own choice is not a well-known row.
- **Translate at the boundary, once**: `CustomerManager.Ensure`, `PartyHandler`'s `RoleIds` (a
  screen and a deep link keep sending the frozen id), and `DbSettingsManager` for any settings
  type that says it names them (`INameWellKnownRows`). Nothing translated is written back
  untranslated — the stamp guarantees the row written is this scope's.

## A document is a caller too, and every id it carries is answered

A starter pack is a list of message invocations shipped as data, so it writes rows like any
handler — but it also CREATES them, with ids fixed in the file, and the primary key carries no
tenant. A pack id written verbatim is one row in the whole install, so the second tenant of a
cell replaying the same document collides on it: a unique violation at onboarding's Finish, not
an `AlreadyExists` the replayer could skip.

**`TenancyProvider.Owned(id)` answers a document's guid** the way `Row` answers a handler's
constant, and tells only one class apart: an id the install SHARES — the seeded rows of an
`IInstallScoped` entity, declared with `services.AddInstallScopedRows(...)` where those rows are
seeded — answers itself, because there is one of that row and it is everybody's. Everything else
is a row of the importing scope and derives through `TenantKey.For`: what the tree freezes and
the first touch copies, what the document creates, and what it points back at. The derivation is
a function of the id alone, so both ends of a reference inside the document land on the same
value and nothing has to tell a creation from a reference.

- **One door, so no reader can skip the seam**: `PackLoader` resolves before it deserializes,
  and both callers — the onboarding wizard's `ImportStarterPack` and the Starter card's
  `ImportStarterCatalog` — read through it.
- **The laws are tested, not remembered** (`StarterPackScopeTests`, `TenantBootstrapTests`): no
  pack may name a row the first touch copies; every id a pack carries comes out derived unless
  the install shares it; two tenants replaying one pack write disjoint ids; and every row of
  every install-scoped table is declared — read from the model and the database, never from a
  list.
- **A row this scope already owns arrives as a token, not an id** — `{{OrganizationId}}`,
  substituted after the guids are answered, because answering it twice would send the document
  at a row nobody has.

## Caddy assigns the tenant, Postgres is the registry

Under `MultiDb`, **the tenant comes only from the proxy** and **there is no catalog database** —
a third runtime dependency buys nothing Caddy and Postgres are not already telling us:

- **Caddy is the whitelist.** Each tenant's site block — written at provisioning — strips any
  incoming copy of `X-NSail-Tenant` and writes its own (`header_up`). The wildcard demo route
  takes its slug from the host's label (`{labels.N}`), a custom domain from its own block: same
  header, two sources, and **the app never parses the Host**, so it cannot tell them apart.
  `TenancyMiddleware` (`NSail.BaseServices.WebApi`) reads that header and nothing else; the
  header arriving twice is not a slug and is refused like any malformed one.
- **Postgres is the registry — under `MultiDb`.** `TenantDatabases` derives the name as
  `{product}_{cell}_{tenant}` — `Tenancy:Product` and `Tenancy:Cell` from the cell's own
  configuration, the tenant from the header — and the row in `pg_database` **is** the truth that
  the tenant exists. Unknown slug → no database → **404, answered before authentication** and
  creating nothing. Only the database moves per tenant; server, user and password stay the
  install's.

Under `SingleDb` there is no registry and no database name: a well-formed slug **is** the
tenant, and `TenancyMiddleware` enters it. A malformed one is still 404, ahead of everything.

`TenantSlug.Normalize` is the one gate between the header and a database name: lowercase
alphanumerics and the hyphen, case folded, **anything else refused rather than repaired** —
stripping a character folds two words onto one tenant, which is the leak, not the fix. A
composed name Postgres would truncate at 63 bytes is refused for the same reason.

## The credential is stamped, and checked on every request

The tenant wall — a connection or a column — keeps the work inside one tenant; what would
otherwise cross it is the **actor**. So wherever a tenant is resolved (`MultiDb` or `SingleDb`)
sign-in stamps the tenant it ran on into the ticket (`PrincipalFactory`, claim `tenant`), the
adapter reads it back as `Session.Tenant`
(permissions.md), and `TenantClaimMiddleware` (`NSail.BaseServices.WebApi`) compares the two on
every authenticated request:

- **The claim is compared against the header's resolution, never a re-read of the Host.** One
  tenant is reached through a wildcard demo label and through its own domain; a wall that looked
  at where the request landed would refuse one of them.
- **A mismatch answers 401, not 403**: on this install the holder of another tenant's ticket is
  nobody, and signing in here fixes it (permissions.md, the two refusals). **A ticket that names
  no tenant is a mismatch too** — deny by default reads an absence as an absence.
- **Changing tenant is re-authentication.** No switch endpoint, no claim refresh: a different
  tenant is a different sign-in, on a different host, against a different database. The
  organization selector is the other wall and is untouched.

**The refusal takes the ticket with it.** The wall stands ahead of every endpoint, sign-in
included, so a refusal that left the cookie standing would lock its holder out of the one act
that fixes it — and a ticket minted before the wall existed names no tenant, so that holder is
anyone still carrying a credential from the previous deploy, on their own tenant's host. So the
middleware signs the credential out, then answers as the install answers a caller with none: 401
to an API, the sign-in page to a browser. It cannot do that by throwing — the error handler
answers by clearing the response, and the `Set-Cookie` goes with it.

Under `None` nothing is stamped and the middleware is not installed. Under `SingleDb` it matters
most: the connection is not a wall there, so the credential check is the only thing between one
tenant's ticket and another tenant's rows. Both middlewares install wherever a tenant is
resolved at all — `host.ResolvesTenants()`, either switch; `host.ConnectsPerTenant()` stays for
what depends on the connection wall itself.

**The tenant is ambient, not scope-local** (`AmbientTenant`, an `AsyncLocal`). A request's own
work routinely opens child scopes — a prerendered component resolving a brand,
`DbAnonymousBrandingProvider` taking a fresh scope so two threads do not share one `DbContext` —
and a child scope that could not see the tenant would answer the refusal on a page the request
had already answered. Entering reaches every scope the request opens and no request beside it.
**Work that outlives its request pins instead**: `ResolvedTenancyProvider.Enter` also fixes the
tenant onto its own scope, and `TenancyCircuit` (`NSail.BaseServices.WebApp`) does that at
circuit-open — the one moment a Blazor circuit's scope is still inside the connect request.

**First touch migrates**, under `TenantMigrator`: a provisioned database carries no schema until
the first request reaches it, and that request applies the chain. The lock is a Postgres
**advisory lock on the tenant's own database**, so it crosses processes and replicas with nothing
to install; two concurrent first touches produce one migration and one wait. A per-process memo
keeps the cost off every later request.

For a host: `ApplyMigrations<T>` and any other startup pass over "the install's database" **do
nothing** under a per-tenant connection — ask `host.ConnectsPerTenant()` — because there is no
install database to migrate and a startup scope resolves no tenant.

## Background jobs: the runner sweeps, the job says whose the work is

A job's own scope resolves no tenant. **The answer is in the runner's contract, so no job
invents one**: `IBackgroundJob.Tenancy` is `TenancyScope.Tenant` or `TenancyScope.Install`,
**with no default** — a job that never answered would answer by accident, as the install, which
reads nothing and cannot write at all.

- **The runner acts on it and the job never does.** For a per-tenant job `BackgroundJobRunner`
  opens **one scope per tenant** and enters it before resolving the job, so `Run` is written as
  if there were one tenant in the world. **One tenant per scope** is mandatory: `Enter` pins onto
  the scope and refuses a second tenant, because a context built on the first keeps its
  connection.
- **A tenant that fails costs its own pass.** The catch is per pass, not per tick; the log line
  names the tenant and the sweep continues with the next one.
- **A job runs in the install's default language, and so does a deferred item.** No request sets
  a culture on a loop or on a queue's consumer, so the runner sets `Language:Default` as
  `UseRequestLanguage` does on a request; `LanguageProvider` then seeds itself with "es" instead
  of the invariant "iv", and a send from a job or from `DeferredWork` finds the template a request
  would. The culture is per loop and per deferred item: it neither outlives the work nor reaches a
  request. A host that configures none keeps the ambient culture, which is what
  `HandlerHost`'s inline drain leaves a fixture on — so the language a deferred send asks for is
  proven in `NSail.Background.Tests` and nowhere a fixture reaches.
- **The roster is declared, never guessed** (`TenantRoster`). Where the wall is the connection,
  the provisioned databases ARE the registry and are read whole — the same truth a request
  resolves against. Where the wall is the tenant column there is no registry: the proxy's
  whitelist is the roster, so the install states it in **`Tenancy:Tenants`**, comma-separated,
  refused at startup if one entry cannot be a slug. Rows cannot answer the question — a query
  spanning tenants is what the filter refuses, and `DISTINCT TenantId` would name whoever has
  written rather than whoever exists. The cost is a second place to add a tenant under
  `SingleDb`: a slug in Caddy and not here serves requests and gets no jobs, which is why an
  **empty roster under a tenant-scoped mode is logged at warning**, once per job, **and reported
  as a metric**.
- **The runner's silence is a metric, not a log line** (`BackgroundJobMetrics`, meter
  `NSail.Background`). `LogScrubber` drops every string attribute at the export seam, so the
  warning above reaches the backend with `{Job}` cut out and no alert could name what broke.
  Three instruments, one per way a job goes quiet, each tagged `nsail.job` (the type's name —
  not `job`, which the Prometheus side of an OTLP gateway spends on the service):
  - `nsail.background.job.passes`, a counter also tagged `nsail.outcome` (`ok`/`failed`, the
    Mediator span's vocabulary) — **it fails**;
  - `nsail.background.job.roster`, the passes the last tick swept: the roster for a per-tenant
    job, **one** for install-wide work and for an install that resolves no tenant, **zero** for
    the job that runs for nobody — so the two never look alike; nothing is reported before the
    first tick;
  - `nsail.background.job.overdue`, time since the last completed pass **divided by the job's
    declared `Interval`** — dimensionless, so one alert rule at `> 3` covers an hourly job and a
    six-hourly one. It counts from boot until the first pass completes, so a job running for
    nobody is late too.

  The install is **not** a metric attribute: it is already a resource attribute (`service.name`,
  `nsail.tenant`, `deployment.environment` — baseservices.md), and re-emitting it would be
  writing what is derived. Nothing per-item, per-user or per-exception is ever tagged: every
  distinct label value is a series on the backend.
- **Under `None` nothing is registered and the tick is the single pass it always was** — the
  invariant the contract must not break, held by the runner's suite.
- **A job provisions nothing.** The runner does not run the first touch beside `Enter` the way
  the middleware does, so under `MultiDb` a tenant whose database no request has reached fails
  its pass, loudly, rather than having a migration applied by a clock.

`BnaRateJob`, `HolidayFeedJob`, `HorizonJob`, `ScheduledMessageJob` and
`WhatsAppTemplateSyncJob` all declare `Tenant`. The question is asked rather than left to the job
because the defect is silent: a per-tenant job with no tenant never fails, its reads just return
nothing behind the filter (`HorizonJob` did exactly that). A `SingleDb` cell that names no roster
is the same silence one level out: **an install declares its tenants where its other deploy facts
live** (nsail-ops, `setup/hosting/cell-template`), and the alert rules written against the three
instruments are in that repo's `providers/grafana.md`.
