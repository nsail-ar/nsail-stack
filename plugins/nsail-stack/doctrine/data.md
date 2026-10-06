# Data access

How NSail persists data in a semi-modular monolith. This page is the entry point; the tenant
wall, the org filter, tenant resolution and background-job tenancy are in
[data-tenancy.md](data-tenancy.md) (routed from Tenancy, below).

---

## Model

One database, one app `OpticalDbContext`, modular configuration per kit and app. The Stack owns
the extensions; kits own entities; apps own the context, composition, migrations and seeds.

```
Stack/NSail.Data              → IDbContextSetup, DbContextExtensions
Kits/{Kit}/NSail.{Kit}.Data   → entities + DbContextSetup + Add{Kit}Data
Apps/{App}.Data               → OpticalDbContext, migrations
App host (Web)                → AddDataAccess (context factory + SetDefaultDbContext)
```

Handlers inject `DbContext` (or the concrete `OpticalDbContext`) and use `Set<T>()` — no typed
`DbSet` properties on the context, no repository layer.

---

## Stack: NSail.Data

Infrastructure only. No domain entities, no shared DbContext type.

| Type | Role |
|---|---|
| `IDbContextSetup` | Kit/app registers EF model configuration |
| `Setup.AddDbContextSetup<T>` | Registers a kit's `IDbContextSetup` in DI |
| `DbContextExtensions.ApplySetups` | Applies `IDbContextSetup` instances to a `ModelBuilder`, then the Stack's own seed history and the model-wide rules (row version, collation, tenant column, org filter) |
| `DbContextExtensions.ApplyTenancy` | What a product's context states in `ConfigureConventions` — see Tenancy |
| `DbContextExtensions.SetDefaultDbContext<T>` | `AddScoped<DbContext, T>()` for handler injection |
| `Setup.AddDataAccess<T>` | The tenancy seam plus the app's context — what a host calls instead of `AddDbContext` |
| `TenancyOptions` / `TenancyProvider` / `Tenant` / `ITenanted` | The seam itself — see Tenancy |
| `TenantRoster` | Who the install's tenants are, for work outside a request — data-tenancy.md, Background jobs |
| `IInstallScoped` / `ITenantSeed` | What a tenant does NOT own, and what it is born with — data-tenancy.md |
| `TenantSeedStep` / `AppliedSeedStep` | One named step of that seed, and the tenant's history of which it has run — the Stack's only table (data-tenancy.md, First touch bootstraps) |
| `WellKnownRows` / `TenancyProvider.Row` | The ids code names by constant, and which scope's copy one means (data-tenancy.md, A frozen id) |

Connection string: app `{App}Options` with `[Required] ConnectionString`, loaded via
`configuration.Load<OpticalOptions>()` from `NSail.Configuration`. See
[types.md](types.md#nsailconfiguration).

### Tenancy

**A tenant is a customer inside an install, not the install itself.** An install states its
mode in `Tenancy:Mode`; nothing else about tenancy is written by a product:

| `Tenancy:Mode` | Connection | Rows |
|---|---|---|
| `None` (absent means this) | install | install |
| `MultiDb` | tenant | install |
| `SingleDb` | install | tenant |

**A mode is a preset of two independent switches, never a three-branch `if`** — `Connection`
and `Rows` on `TenancyOptions` are derived separately, so the quadrant no mode names (sharding)
stays representable without being built, and nothing downstream reads `Mode`. Both switches are
implemented. The `Connection` switch keeps one refusal: a scope that reached a `DbContext` with
no tenant resolved, which would otherwise be served the install's own database.

`AddDataAccess<TDbContext>(connectionString, tenancy = null)` is the whole host-side surface —
a null `tenancy` *is* `None`, so a plain install composes as
`AddDataAccess<TDbContext>(connectionString)`. It registers the seam, registers a **scoped
`IDbContextFactory<TDbContext>`** that asks that scope's `TenancyProvider` for the connection,
sources the scoped context from that factory, and calls `SetDefaultDbContext<TDbContext>()`. The
factory is scoped rather than singleton because resolution is per request, and it is a factory
rather than a bound string because a Blazor circuit outlives the request that opened it. The
migrations assembly is derived from the context's own assembly, never named.

`TenancyProvider` is the one injected service: `Current` answers the `Tenant` (slug, database,
row key, whether one is resolved at all) and `ResolveConnection` the connection the work runs
on. The default answers `Tenant.None` and, under `None`, the install's own completed string, so an install that
states nothing behaves as if the seam did not exist. A host that resolves tenants replaces the
provider. `RequestOrganizationProvider` has the same shape and is **not** this seam: it brands an
anonymous page and is never a tenant selector.

The rest lives in [data-tenancy.md](data-tenancy.md), under these headings:

- **The tenant is a column EF puts on every entity** — `SingleDb`'s shadow `TenantId`, filter,
  stamp and tenant-leading indexes; the primary key does not move; `IgnoreQueryFilters` is
  refused; an unresolved scope reads nothing.
- **The install's own rows, and the one law that decides which they are** — `IInstallScoped`;
  an entity a tenant can WRITE is the tenant's; the directional FK law.
- **The org is the other axis** and **The org filter: a subtree WHERE no handler writes** —
  `IOrgScoped`, `OrgFilter`, the message-named subtree, `OrgScope.Everywhere`.
- **First touch bootstraps** — `TenantBootstrap`, `ITenantSeed`, `AppliedSeedStep`,
  `AddTenantCache<T>`.
- **A frozen id is a row's NAME** — `TenancyProvider.Row`, `WellKnownRows`.
- **A document is a caller too** — `TenancyProvider.Owned`, `PackLoader`.
- **Caddy assigns the tenant, Postgres is the registry** — `X-NSail-Tenant`,
  `TenancyMiddleware`, `TenantSlug.Normalize`.
- **The credential is stamped, and checked on every request** — the `tenant` claim,
  `TenantClaimMiddleware`, `AmbientTenant`, `TenantMigrator`.
- **Background jobs** — `IBackgroundJob.Tenancy`, one tenant per scope, `Tenancy:Tenants`,
  `BackgroundJobMetrics`.

### Provider

**PostgreSQL, through `Npgsql.EntityFrameworkCore.PostgreSQL`.** A slot's connection string is
`Host=localhost;Database=optical_devN;Username=postgres` — **no password, by design**: every
connection route goes through `ConnectionStrings.Complete` (`NSail.Data`), which fills a missing
password from the machine's `PGPASSWORD` (the variable psql already honors; Npgsql alone does
not read it). `PGHOST` and `PGPORT` ride the same seam: set, they repoint the string's `Host=`
and port (a CI job reaches its postgres service through the docker gateway on a per-run
published port, never `localhost:5432`); unset, nothing changes. An explicit `Password=` always
wins (a deployed install's full string passes through untouched), and no repo carries the dev
password. The first `ApplyMigrations` creates the database the string names.

**Every text column is case- and accent-insensitive.** Postgres compares text byte for byte, so
a `search=` filter written as `Contains` would miss a capitalized name. `ApplySetups` closes that
over the whole model: it declares a non-deterministic ICU collation
(`Collations.CaseInsensitive`, locale `und-u-ks-level1`) and assigns it to **every** text column,
so handlers keep writing plain `Contains`. Deliberate consequences:

- Equality folds too — `Code`, `Name` and `Login.Identity` are unique case-insensitively, and
  signing in as `ADMIN` finds the `admin` login.
- A value that must compare byte for byte opts out with `[CaseSensitive]` on the property —
  among others `Login.ProviderKey`, `Login.AccessKey`, `Policy.Document`, and Calendar's
  external keys and tokens (`CalendarEvent.SourceKey`, `CalendarConnection.Credential`,
  `CalendarFeed.Token`, …). The rule, not the roster, is the source of truth: anything secret,
  keyed or serialized belongs on it — folding case there widens what counts as a match.
- Nondeterministic collations require **PostgreSQL 18 or newer** — earlier versions reject
  `LIKE` against such a column, which is what `Contains` compiles to.

**Dates split by meaning** — three shapes, each with its own storage:

- A **business date** is `DateOnly` on Postgres `date` — no zone, no `Kind`, nothing to convert.
  Its `today` is the host's local day, read through `NSail.Dates.BusinessDate.Today` — never
  `DateTime.Now`/`UtcNow` inline: UTC rolls over hours before local midnight and would move an
  evening's business into tomorrow while the day is still open. **Tests obey this too**: a
  fixture pinning behavior to "today" reads `BusinessDate.Today`; one reading `UtcNow` goes red
  from 21:00 local to midnight — a failure that belongs to no commit.
- A **system timestamp** is `DateTime` from `DateTime.UtcNow`, on `timestamp with time zone`,
  always UTC.
- A **civil time** — when it happens on somebody's wall clock, not on the timeline — is a
  `DateTime` mapped **explicitly** to `timestamp without time zone`, paired with an IANA
  `TimeZoneId` text column (`CalendarEvent.StartsLocal` is the precedent). The explicit column
  type is required: Npgsql maps `DateTime` to `timestamptz` by default and refuses an
  `Unspecified` kind on write.
  - A zoned value at the HTTP edge (`18:00:00Z`) deserializes with `Kind = Utc`, which the column
    rejects — the handler rejects it as a caller error (`BusinessException`), never coerces it:
    stripping the zone from `18:00Z` silently names a different instant. UTC conversion at rest
    is wrong for the same reason: zone rules can change between writing and the event.
  - The handler resolves the zone id (`TimeZoneInfo.TryFindSystemTimeZoneById`) and rejects one
    that does not resolve — a typo stored today fails far from its cause. Resolvable Windows ids
    are **canonicalized to IANA at rest** (`TryConvertWindowsIdToIanaId`): that preserves
    meaning (normalize-and-validate in one step, the `IChannelProvider.Normalize` precedent), and
    the real calendar providers (Google, Graph) demand IANA.
  - **Never branch on zone-id equality.** tzdata keeps its `backward` aliases:
    `TryConvertWindowsIdToIanaId("Argentina Standard Time")` answers `America/Buenos_Aires`, not
    `America/Argentina/Buenos_Aires`, and .NET exposes no alias resolution — so two stored ids
    can name one zone while string equality **and** `TimeZoneInfo.Equals` both say no (on .NET
    10: `HasSameRules` true, `Equals` false). Convert both sides to instants and compare those;
    when the question really is "same wall-clock behaviour", resolve both ids and ask
    `HasSameRules`.

A business date typed as a bare `DateTime`, paired with nothing, is not a shape the model has;
it fails at write time.

---

## App DbContext

Each app defines its own context in `{App}.Data`:

```csharp
public class OpticalDbContext : DbContext, ITenanted
{
    readonly IEnumerable<IDbContextSetup> _setups;
    readonly TenancyProvider _tenancy;

    public OpticalDbContext(
        DbContextOptions<OpticalDbContext> options,
        IEnumerable<IDbContextSetup> setups,
        TenancyProvider? tenancy = null)
        : base(options)
    {
        _setups = setups;
        _tenancy = tenancy ?? TenancyProvider.Install;
    }

    public Guid? TenantId
    {
        get { return _tenancy.RowKey; }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.ApplyTenancy();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplySetups(this, _setups);
    }
}
```

`ITenanted` over an injected `TenancyProvider` (defaulting to `TenancyProvider.Install`, so a
design-time or fixture-built context needs no argument), `ApplyTenancy` and `ApplySetups` are
everything a product says about tenancy. `TenantId` is `Guid?` and reads `RowKey`, not
`Current.RowKey`: null is what an unresolved scope gets under `SingleDb`, and it is what makes
that scope read nothing. Answering `Guid.Empty` there would hand it the install's rows.

The context applies all registered `IDbContextSetup` instances in `OnModelCreating`, so EF
conventions stay active. Do not pre-build the model with `UseModel` — a conventionless
`ModelBuilder` cannot map primitive types.

---

## Kit Data projects

Each kit with persistence has `NSail.{Kit}.Data`:

```
Entities/                 # persistence types (NSail.{Kit}.Entities)
DbContextSetup.cs         # IDbContextSetup — model config in one place
Persistence.cs            # Add{Kit}Data()
```

`Add{Kit}Data()` registers `DbContextSetup` via `services.AddDbContextSetup<DbContextSetup>()`.
It does **not** call `AddDbContext`.

### EF model configuration

Configure entities in `DbContextSetup` with explicit `modelBuilder.Entity<T>()` calls —
typically one private static method per entity.

**Do not** use `ApplyConfigurationsFromAssembly` or `IEntityTypeConfiguration<T>` scanning:
assembly reflection works against AOT, adds startup cost, and hides what the model contains. A
large setup splits into `partial class` files (e.g. `DbContextSetup.Party.cs`) with extra
`Configure*` methods, still wired with **explicit calls**.

```csharp
public class DbContextSetup : IDbContextSetup
{
    public void Configure(ModelBuilder modelBuilder)
    {
        ConfigureParty(modelBuilder);
        ConfigureOrganization(modelBuilder);
        // ...
    }

    static void ConfigureParty(ModelBuilder modelBuilder) { ... }
}
```

---

## Directory as foundation kit

`NSail.Directory.Data` holds shared entities (Party, Organization, Location, Relationship).

- Other kits may reference `NSail.Directory.Data` from their **Data project only** — not from Sdk
  or WebApi (e.g. `NSail.Iam.Data` references it for FKs to `Party`).
- Peer kits (Iam ↔ Billing) do not reference each other's Data projects. Use IDs or messaging.

**`Location`** is a generic place (street/city/province/postal code, optional `Name` for
depots/branches without a formal postal address, optional `Latitude`/`Longitude`), owned by
neither Party nor Organization. It carries no reverse navigation collections, so it does not
accumulate one per consumer kit. `Party`/`Organization` link to it through join entities
(`PartyLocation`/`OrganizationLocation`: FK to owner + FK to `Location` + FK to `LocationType` +
`IsPrimary`) — the association-entity pattern of `Membership`/`Relationship`. `LocationType` is a
reference table (`Id`/`Code`/`Name`/`IsSystem`), not an enum. `IsPrimary` is scoped to
`(owner, LocationType)` by a filtered unique index, so one primary Billing and one primary
Shipping location can coexist for the same owner, even pointing at the same `Location` row.

**Contact channels** reach the opposite conclusion: a place is shared (three parties at one
address are three joins to one row), but an email or phone belongs to its owner alone, so
`PartyChannel` / `OrganizationChannel` carry `Type` (string key) and `Value` directly — no shared
table. `Type` is a string, neither enum nor reference table: channel types are *code*-extensible
(each has behavior — validation, normalization, a contact URI), so the catalog is the registered
`IChannelProvider` set, with `ChannelTypes` constants naming the built-ins. `IsPrimary` scopes per
`(owner, Type)` with the same filtered index. A stored type whose provider is gone degrades to a
plain value — displayed without action, never an exception.

### Enum vs. reference table

Plain enum (`.HasConversion<string>()`) for structural distinctions unlikely to need
business-specific extension (`PartyKind`: Individual/LegalEntity). Reference table with
`Id`/`Code`/`Name`/`IsSystem`, seeded with `IsSystem = true` rows, when values are
business-configurable (`Role`, `LocationType`) — a category a customer may want to extend
without a deploy. Directory's entities serve close to any small/medium business (a generic ERP
foundation, not one vertical) — when in doubt, prefer the table.

**Party is an identity, not a base class.** A concept references Party when it has a civil
identity — it bills, gets paid, signs, holds a CUIT or DNI. A concept with its own data and
behavior gets its own entity, linking to Party only where the identity is real:
`Workshop { Name, IsEnabled, PartyId? }` — the internal workshop is a named workstation (no
Party), the external one a supplier (Party, so it can have a current account). Modeling
everything as Party-or-Organization is the universal-graph anti-pattern: when everything is a
node, nothing means anything.

**Table names are unique across the shared database, and the plain name belongs to the
platonic owner.** Kits share one DbContext, so two kits cannot both have an `Accounts` table. In
an ERP *the* Account is the ledger account, so Accounting keeps `Account` and Iam's
credential-per-provider is `Login` (ASP.NET Identity's name for that table). A kit reaching for a
shared noun checks the database first and yields the plain name to the better claim.

**A flag lives on the entity whose OWN query has to read it, not on the kit that writes it.**
Dependencies point one way, so a mark on Iam's `User` is invisible to Directory's `ListParties` —
and "this Party holds nothing but what a sign-in provider claimed" is a sentence the people list
has to answer. So `Party.IsProvisional` is Directory's, written by each sign-in provider's
founding branch and lifted by Iam's `CompleteSignUp`. The alternative — the flag upstairs plus a
filter Iam contributes into Directory's query — is machinery bought to keep a column in the kit
that happens to set it.

---

## App composition

The app host wires everything once:

```csharp
public static void AddOpticalWebApi(this IServiceCollection services, IConfiguration configuration)
{
    services.AddDirectoryData();
    services.AddIamData();
    services.AddOpticalData();

    var optical = configuration.Load<OpticalOptions>();
    var iam = configuration.Load<IamOptions>();

    services.AddDataAccess<OpticalDbContext>(optical.ConnectionString, configuration.Load<TenancyOptions>());

    services.AddOpticalServices();
    services.AddOpticalEndpoints();
    services.AddIamWebApi(iam);
}
```

Order: **data setups → AddDataAccess → kit WebApi → app handlers**.

Kit `Add{Kit}WebApi()` methods must not register DbContext or call `Add{Kit}Data()` — the app
owns persistence composition.

---

## Migrations

Migrations live in `{App}.Data` (e.g. `NSail.Optical.Data/Migrations/`). Design-time factory:
`OpticalDbContextFactory` in the app Data project.

```bash
dotnet ef migrations add <Name> \
  --project src/Apps/Optical/NSail.Optical.Data \
  --startup-project src/Apps/Optical/NSail.Optical.Data \
  --output-dir Migrations
```

**The Data project is its own startup project.** Pointing at the Web host fails with
`Method 'ReduceExtensionMember' ... does not have an implementation` and writes nothing: the
host's output holds `Microsoft.CodeAnalysis.dll` at the source generator's version next to
`Microsoft.CodeAnalysis.Workspaces.dll` at EF's, and the migration code generator loads both.
The Data output carries no Roslyn.

Apply pending migrations at host startup, after `Build()` and before serving requests:

```csharp
var app = builder.Build();
await app.ApplyMigrations<OpticalDbContext>();
```

**Migrations are normal and append-only.** A schema change adds a new migration; nothing is
folded, no database is dropped. Every slot's next `ApplyMigrations` moves its own database
forward, so parallel sessions take schema changes from each other without recreating anything.

**A scaffolded migration is read before it lands.** The differ is wrong about at least one thing
by construction: a provider-implemented row version (Optimistic concurrency, below) scaffolds an
`AddColumn`/`DropColumn` pair for a column the database refuses to create. Keep that migration
and empty its bodies by hand. The refusal is `AddColumn`'s alone — inside `CreateTable` the
provider drops the system column itself, so `InitialCreate` keeps every
`xmin = table.Column<uint>(type: "xid", rowVersion: true, …)` the differ writes.

**The model snapshot is one shared generated file.** Two schema stories landing together
conflict in `OpticalDbContextModelSnapshot.cs`; the resolution is never a manual merge of
generated code: pull-rebase, **remove your migration, re-add it on top** of what landed. Schema
stories still sequence softly through Cap, to keep the re-scaffold rare.

**A kit entity change migrates every composing product in the same story.** A migration
generated for one product leaves every other product composing that kit with a model that no
longer matches its chain — it stops booting. Before generating, enumerate the composing apps from
their Web.cs; one migration per product, same commit.

**A raw SQL statement ends with its own semicolon.** `migrationBuilder.Sql` passes its text
verbatim, and `dotnet ef migrations script` concatenates the chain's commands without adding a
terminator — so a statement missing its `;` gets EF's next command glued onto its tail, and
Postgres reads one malformed statement (`… AND w."StoreId" IS NOT NULL INSERT INTO
"__EFMigrationsHistory" …`). Applying the chain one command at a time (every `Migrate()`, every
`database update`) never notices; the script — how a chain is read before it touches a database
— does. The rule covers every `Sql` call, the `Seed.*` blocks included. `MigrationScriptTests`
generates both chains' scripts through the one call the CLI wraps and applies them to an empty
database, so a missing terminator is red in CI.

```bash
dotnet ef migrations script \
  --project src/Apps/Optical/NSail.Optical.Data \
  --startup-project src/Apps/Optical/NSail.Optical.Data \
  -o /tmp/optical.sql
```

### Compaction

The chain was last compacted 2026-09-16 (both apps; chains are per-app) and restarts from
`InitialCreate` + `Seed`. A compaction is a deliberate, announced event, never assumed. The
procedure:

- **Seeds are hand-authored `InsertData` with fixed Guids and do not regenerate** — they carry
  over by mechanical transformation, and the stored-shape pin tests (`PolicyDocumentTests`) prove
  the carry-over byte-identical.
- **A seed block's `InsertData` names every column the model gives no default.** In an appended
  chain a column arrived through `AddColumn` with a `defaultValue` that filled the seeded row;
  born inside `InitialCreate` it is `NOT NULL` with no default and the bare INSERT fails (e.g.
  `Parties.IsProvisional` and the two consent flags, `Users.RequiresSecondFactor`, `VoucherTypes.TaxInclusive`/
  `DiscriminatesTax`, `Workshops.Mounting`). A retrofit `UpdateData` that wrote a flag over the
  seeded rows folds INTO the block's own INSERT.
- **Data-only migrations fold into the `Seed.*` blocks**, or are dropped where they only rewrote
  rows a fresh database never has.
- **A block that READS a column a later migration drops does not survive** (`SeedSellingPrices`
  backfilled from `Products.Price`, which the compacted model never has): the backfill goes, the
  list it planted beside stays.
- **Retrofit halves die, and so do the tests that replay them.** A migration-replay test migrates
  to a migration BY NAME (`MigrateTo(database, Before(database))`), so it is deleted with the
  migration it pins. Coverage of the tenant guard (`SeedSql`) then rests on the tenant-seed
  retrofit tests.
- **Prove the carry-over by diffing two fresh databases, never by reading the diff.** Build a
  scratch database from the OLD chain before deleting anything and another from the new one, and
  compare `to_jsonb(row)` per table — column order changes under a re-scaffold, so a `pg_dump`
  text diff says everything moved and nothing about the rows.
- **A deployed install is re-based, never dropped** (a staging cell may instead be dumped,
  dropped and reloaded around the compaction — last bullet): truncate its `__EFMigrationsHistory` and
  insert the new `InitialCreate`+`Seed` as applied (the schema already matches, byte-verified);
  then the deploy runs and only genuinely-new migrations apply. **This happens WITH the deploy,
  back-to-back** — a truncated history under the old container is a crash waiting for a restart.
- **Check the install's real state before truncating.** An install BEHIND the compaction
  baseline (missing a migration the compaction folded in) needs that migration's delta replayed
  by hand FIRST — back up, apply the missing delta (e.g. one `ALTER … ADD COLUMN`), then re-base
  — or the folded column is lost forever.
- **Data loaded from an old dump crosses columns it never had.** Every column born `NOT NULL`
  with no default inside the new `InitialCreate` refuses the dump's rows outright. `SET DEFAULT`
  on each, load, `DROP DEFAULT` — the schema ends identical to the model. Seed-owned rows come
  back refused as duplicates on the second pass: the seed winning, as intended.

---

## Seeding

Kits do not seed data — the **app** owns bootstrap data. A kit may ship rows and the way to
plant them, and only the app's chains run it: Directory's geography is a data migration and a
`TenantSeed` step in each app, both calling `GeographySeed`
(geography.md (nsail: `src/Kits/Directory/docs/geography.md`)).

**Seed carries the boot minimum; everything else is a pack.** Boot minimum is not "the least
that starts a process": it is **the data with which the system can administer itself and be
filled in until it is usable on day one**. Content an install could live without or replace —
catalogs, reference content, role *presets* (the permission bundles hung on a role) — is a pack,
imported per install.

**Role rows are Seed.** `NSail.Optical.Sdk/Roles/SeededRoles.cs` names those roles **by id** from
code, and the portal policies wire `Paciente` and `Cliente` in. A row the code names by id is not content
an install may replace; it is structure that happens to live in a table.

Optical's seed as the worked example:

| Block | Verdict | Why |
|---|---|---|
| `Seed.Core.cs` | Seed | the admin login, the Default organization, the rows without which the first screen cannot render |
| `Seed.Roles.cs` | Seed | named by id from `SeededRoles.cs` and from the portal policies |
| `Seed.Staff.cs` | Seed | `vendedor`/`encargado`: named by id from the preset's own `RoleId` constraint, and a shop that must import before it can hire is half-administered |
| `Seed.Personnel.cs` | Seed | `Personal`, the marker a seat earns: named by id from `SeededRoles.Staff`, and it grants nothing, so there is no preset it could be a pack of. Appended after the last compaction, so it ships in its own migration with a guard, and is the one role block that also writes MEMBERSHIPS — its retrofit content is who already holds the role |
| `Seed.Policies.cs` | Seed | `Iam.Users.ChangePassword` — the system administering itself is the definition |
| `Seed.Accounting.cs` | Seed | no settlement can post without a chart of accounts |
| `Seed.EntryTemplates.cs` | Seed | the templates the chart above is posted through |
| `Seed.Vouchers.cs` | Seed **today**, pack the day there is a second country | Factura A, Consumidor Final, Exento are AFIP/ARCA shapes — country structure, not universal |

Catalogs are packs: the starter catalog is imported per install (`ImportStarterCatalogCard`).

**A pack is not the same thing as a click.** Where content is a pack and the system must still
arrive able to administer itself, both hold: the document stays a pack — replayed through the
mediator, landing ordinary editable rows — and the app replays it itself, at startup for the
install and from an `ITenantSeed` step for a shop. Optical's role presets are the case: the
grants are a pack, the roles under them Seed, and nobody presses anything. It is safe unattended
for the seed chain's reason — **once per scope**, recorded under a step name in
`AppliedSeedSteps`, so a row the shop deleted is not replanted by a restart.

### Seed migrations

Bootstrap rows go in a **separate** app migration (schema first, data second) with
`migrationBuilder.InsertData` and matching `DeleteData` in `Down`, with fixed Guids so `Down`
removes the same rows. That keeps data with the schema history and applies it once, with
`dotnet ef database update` / `Migrate()`. New bootstrap rows go in new data migrations, same
rules.

Example, `NSail.Optical.Data`: `InitialCreate` builds tables; `Seed` inserts the admin role,
Default organization, Party, Membership, User and `nsail`/`admin` Login (password hash
precomputed for `PasswordHash.Verify`). `Seed`'s `Up` is a list of calls, one per area —
`SeedCore`, `SeedAccounting`, `SeedVouchers` and the rest, each a static class in its own
`Seed.*.cs` file beside the migration, `Down` running them in reverse. Kept apart rather than
concatenated because a thousand-line `Up` is unreviewable, and the order the calls are written in
*is* the dependency order.

**The blocks are also read by the tenant bootstrap.** A `SingleDb` tenant's first touch plants
the same shape from the same tables (`ITenantSeed`, data-tenancy.md) under ids of its own, so a
compaction that re-authors these files keeps them exposed — the alternative is a second copy of
the chart, drifting.

**A block added after a tenant exists needs a tenant seed step, or it never reaches that
tenant.** The migration chain is the INSTALL's, and every `InsertData`/`UpdateData` names a
frozen id — a row the tenant's filter hides. The tenant's own chain is `ITenantSeed`'s ordered
list of named steps: appended, never edited, run once per tenant, recorded in the same
transaction as its rows (data-tenancy.md, First touch bootstraps). Growing the seed is two
writes — the block, and the step that plants its twin — and the step guards its own rows, so a
shop that edited or deleted one keeps its decision.

### The seed guard

**A guard or read-back matching on a business key names the tenant; matching on the primary key
does not.** Every unique index leads with the tenant — `(TenantId, Code)`, `(TenantId, Name)` —
so a code is taken only WITHIN a tenant, and a `SingleDb` database holds one such row per
tenant. Asked without the column, "is this row already here?" is answered by ANOTHER tenant's
row and the block silently plants nothing; a scalar subquery reading a row back by code returns
one row per tenant and fails the whole `INSERT` with Postgres 21000. A migration's rows land
under tenant zero, the `TenantId` column's own default (`SeedSql.Tenant`):

```csharp
$"\"Id\" = {SeedSql.Literal(id)} OR ({SeedSql.Tenant} AND \"Code\" = {SeedSql.Literal(code)})"
```

The **id half stays unscoped**: the primary key carries no tenant, so a row under it is a
collision whoever owns it, and scoping that half would let the block plant a duplicate key. A
guard reading a whole table is scoped the same way — "any lab at all" means any of *this
install's*. A guard anchored on a frozen id needs no tenant, because a tenant's twin derives its
own id (`TenantKey.For`). The tenant seed's twin asks the same questions through the query
filter, which scopes them for free; a migration has no filter and must say so itself.

**A block that writes SQL instead of `InsertData` terminates its own statement** (Migrations,
above). Optical's shared emitter carries the `;` in its format string (`Seed.Sql.cs`); Therapy's
blocks write their SQL inline and carry it in the text.

---

## Handlers

```csharp
[Injectable]
public class AuthenticationHandler : IHandler<SignIn>
{
    private readonly DbContext _db;

    public async Task Handle(SignIn message, CancellationToken cancelToken = default)
    {
        var user = await _db.Set<User>()
            .FirstOrDefaultAsync(a => a.Identity == message.Identity, cancelToken);
        // ...
        await _db.SaveChangesAsync(cancelToken);
    }
}
```

Inject `DbContext` when kits should stay provider-agnostic; inject `OpticalDbContext` in app
handlers when the concrete type is acceptable.

**`SaveChanges` is not a commit.** The outermost in-process `Send` owns a transaction every
nested send joins, so a handler's saves — its own and every nested handler's — are visible to
the rest of the operation at once and durable only when that outermost send returns. A handler
writes and saves exactly where the work is, and never orders its steps to make a half-failure
survivable. `SetDefaultDbContext<T>()` enlists the scope's context (`DbContextUnitOfWork` as
`IUnitOfWork`); mechanism and boundaries: [messaging.md](messaging.md#the-ambient-unit-of-work).

---

## Mapping

A handler that writes a message's graph onto rows, or reads rows as a model, declares it on the
entity and lets `Mappers.Entities` generate it (generation.md). The entity names the message —
`[MapFrom(typeof(UpdateParty))]` writes, `[MapTo(typeof(PartyRef))]` reads — and never the
reverse, because the Sdk that holds the message cannot see the Data project's types.

`EntityMapper` (`NSail.Data`, registered by `SetDefaultDbContext`) runs them over the scope's
`DbContext`. Nothing is saved; the handler's `SaveChanges` writes what the run decided.

| Call | Does |
|---|---|
| `Map<TSource, TEntity>` | Loads the root by the source's key, graph included, and merges onto it; a key that names no row is `NotFound`; no key is a create |
| `Upsert` | The same, creating the row that is not there; one source or many |
| `Import` | Upsert for a document that cannot order its rows: a reference to a row not written yet is created from what the document says about it |
| `Project<TEntity, TRow>(query)` | The query read as rows: an expression tree, so the store selects only the columns the row carries |

The graph rules are Detached Mapper's:

- **A member typed as an entity is an aggregation**: attached by key and never written — a
  message cannot rewrite the row it points at. `CustomerId` writes `Customer`, `ChildIds`
  writes `Children`, and a row reads them back the same way.
- **`[Composition]` is owned**: created, merged, replaced or deleted with its root; a
  collection is matched by key, kept in place, and what the source no longer carries is
  removed — a null collection as much as an empty one. A key named twice is one row, written
  from its first occurrence. `[Parent]` receives the owner.
- **A tree is merged level by level**: the root load includes each composition once, and a
  level it did not bring (a composition that holds its own type) is read as the merge
  reaches it, never taken for missing. Depth costs heap, not stack.
- **The concurrency token rides in**: an `IVersioned` entity's `Version` read from the
  message is the expected version, as `ExpectVersion` sets it (Optimistic concurrency).
- **`OrganizationId` is never written by name alone** (NSG004): `[MapFrom(typeof(M),
  "OrganizationId")]` on the member says it, `[MapIgnore]` leaves it to the handler. A
  projection reads it like any member — a read moves no row.
- **A row may be init-only** (`{ get; init; }`, a record): a projection builds it with an
  initializer. A `[MapFrom]` target merges in place, so its members need a setter.
- **An entity is what the model declares**: a `DbSet<T>`, a `ModelBuilder.Entity<T>()`, an
  `[Entity]`, or what those navigate to, unless `[Owned]` or `[NotMapped]`.

**Aggregations are the boundary of a map; compositions are its nodes.** An `UpdateInvoice`
writes the invoice and its `[Composition]` rows as one document, and stops at every aggregation:
it can say which `InvoiceType` the invoice points at and can never rewrite that type. So a
message carries the **whole** set of a composition, never a diff.

**One message per act.** C# has no *undefined*, so `null` cannot mean "leave it alone", and the
mapper has no patch type for it. The answer is the small specific message: `CreateX` and
`UpdateX` are separate contracts with their own `[MapFrom]`, and an act that changes one field
is its own message. What differs between a create and an update (a creation stamp, a constant
the create sets) is the handler's step after the call, never a mapping profile.

A member whose write is a rule, not a copy — a consent that stamps when it was given, a
channel whose verification a new address drops — is `[MapIgnore]`d and written by the handler
around the mapper's run. `[MapIgnore]` holds in both directions.

The handler keeps its guards, the stamps, the constants a create sets and every side effect;
the order is guards, the mapper's call, what the mapper does not write, `SaveChanges`. How to
declare and call it, and what not to convert: the `new-mapping` skill.

Proof: `NSail.Mapping.Tests` (the graph rules without a store), `NSail.Data.Tests/Mapping`
(Detached's EF suite on SQLite in memory, projections included) and
`NSail.SourceGenerator.Tests/Mappers` (every refusal above).

---

## Optimistic concurrency

**A new edit form over a persistent row means the entity is `IVersioned` and its Update carries
`Version`** — otherwise a screen loaded long ago saves its stale values over newer data.
Append-only rows (ledgers, documents, audit trails) and transition-guarded ones (a `Status`
machine that already refuses the illegal move) never carry it — their invariant is the guard.

- **`IVersioned { uint Version }`** (`NSail.Data`) is the whole opt-in. `ApplySetups` calls
  **`IsRowVersion()`** on every implementing entity; the token itself is the provider's. Postgres
  answers with the row's transaction id, a system column it maintains, so **no column is added
  and nothing has to remember to bump anything**.
- **The migration is real and its body is empty.** EF's differ has no notion of a system column
  and scaffolds an `AddColumn`/`DropColumn` pair the database refuses. **Empty both bodies by hand
  and keep the migration** — the snapshot carries the property and the chain must stay in step
  (`SettingsRowVersion`, Optical and Therapy, is the precedent).
- **The handler rides the version into the UPDATE**: load, `db.ExpectVersion(entity, version)`,
  mutate, `SaveChanges`. Setting `OriginalValue` **before** the mutation puts the check in the
  statement's own `WHERE`, so no read sits between check and write. **A zero version means the
  caller holds no token** — a background job, a first save — and `ExpectVersion` states no
  opinion about it.
- **`ExpectVersion(entity, 0)` does not make the write unconditional.** The row version is a
  concurrency token EF checks against whatever `OriginalValue` the load carried in, so a second
  writer between that load and this `SaveChanges` still earns a `DbUpdateConcurrencyException`.
  A caller that means "write over whatever is there" catches it and retries against the row's
  current state — `DbSettingsManager.Save` is the precedent, bounded the way
  `ConcurrentNumbering`'s retry is.
- **The refusal is a Problem, not an exception**, for a version the caller DOES hold.
  `ConcurrencyInterceptor` (registered by `AddUnitOfWork`, so a harness gets it too) maps
  `DbUpdateConcurrencyException` to `BusinessProblem.Conflict` — 409, code `Conflict`. It sits in
  the **pipeline**, not at the HTTP edge, because an in-process caller never passes that edge.
  The row's `UpdatedAt` narrates it when the entity has one (issue code `ConflictAt`, `{at}`
  argument); the token decides, the audit field only says when. Nothing records a **who** yet.
- **The screen keeps the user's values** and draws the Problem where every refusal draws
  (intentional-ui.md). A conflict — only a conflict — adds one act: `NsForm`'s `OnReload`, the
  screen's first read run again. Re-applying the edit is the user's; a merge is never attempted
  and a silent overwrite never happens.

Proof: `NSail.Google.WebApi.Tests` (current version saves, stale version answers `Conflict` and
changes nothing, the refusal narrates `at`), `NSail.Components.Tests/NsFormConflictReloadTests`
(values kept, refusal inline, the act offered only for a conflict and only while it stands), and
`NSail.Settings.WebApi.Tests/SettingsUnconditionalWriteConcurrencyTests` (a version-zero save
wins over a row another writer moved under it, instead of throwing).

---

## Queries vs commands

| Operation | Rule |
|---|---|
| **Queries** (reads, joins, reports) | May use `Include` and cross-kit joins via Directory entities |
| **Commands** (writes) | Mutate only the handler's module entities; cross-kit = FK/ID |

Write boundaries stay clear while reads stay pragmatic in a shared database. Extracting a module
to a service later means replacing joins with IDs + remote calls.

---

## Indexes

The index audit's rules; every new entity answers to them before its migration lands.

1. **A declared FK is already indexed** by EF convention — never restate it by hand.
2. **A soft reference** (an id column with no navigation/FK) **earns no free index** — only a
   query that actually traverses it does.
3. **A `search=` column is never btree-indexed**: `Contains` compiles to unanchored
   `LIKE '%term%'`, which btree cannot serve. GIN/trigram is a separate decision demanding its
   own evidence.
4. **A natural key always gets a unique index, no exception** — a handler's existence check is
   not the guarantee (races, and seeds/imports bypass it).
5. **`Code` is the natural key where it exists; `Name` where there is no `Code`.**
6. **Text uniqueness is case/accent-insensitive by the model collation** — a feature, but ask it
   explicitly before adding a unique index on text; a column that truly needs byte distinction
   goes `[CaseSensitive]`.
7. **Uniqueness over a nullable column has to say so.** Postgres counts every NULL as distinct,
   so a plain unique index over `(A, B?)` lets the same `A` land any number of times while `B` is
   null (two global `Settings` rows under one key happened behind an index that read as unique).
   The shape is one index with `NULLS NOT DISTINCT` (`.IsUnique().AreNullsDistinct(false)`,
   PostgreSQL 15+): the whole invariant in one object, one convention across the tree. The older
   filtered pair — composite `IS NOT NULL` plus single-column `IS NULL` — is still correct but
   says the same thing twice; it earns its keep only when the filter carries more than null
   handling. Either way, **adding one to a table that has been running dedupes first, in the
   same migration, before the index** — a `CREATE INDEX` that assumes a clean table fails at
   startup on every database carrying rows it refuses.
8. **Low-selectivity flags (`IsEnabled`, `IsSystem`) are not indexed** — the planner scans
   anyway. A status column qualifies only as the real exception (primary filter, skewed
   distribution — `WorkOrder.Status`).
9. **The scope column (`OrganizationId`) leads a composite**, never trails it — and the **tenant
   leads even that**, on every index, set by `ApplySetups` (data-tenancy.md). Nothing declares
   `TenantId` by hand.
10. **A bounded table needs no ordering index** — only unbounded ledgers/documents do
    (`Voucher.Date`, `JournalEntry.Date`).

---

## Sdk vs Data entities

| Assembly | Contains |
|---|---|
| `{Kit}.Sdk` | Messages, API/UI models — no EF |
| `{Kit}.Data` | Persistence entities and EF configuration |

Map between them in handlers. Do not duplicate Sdk types as entities 1:1 without reason.
