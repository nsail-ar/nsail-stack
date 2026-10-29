# Data

EF Core over PostgreSQL, set up once for the whole model. A host calls one registration, a
`DbContext` applies the kits' model slices and the Stack's rules (row version, collation,
personal-data comment, tenant column, org filter) over every entity, and a handler injects
`DbContext` and uses `Set<T>()` — no repository layer, no typed `DbSet` properties.

## Packages

| Package | What it is | Referenced by |
|---|---|---|
| `NSail.Data` | `AddDataAccess`, `ApplySetups`, `IDbContextSetup`, `IVersioned`, `IAudited`, tenancy (`TenancyOptions`, `TenancyProvider`, `ITenanted`, `IInstallScoped`), the org filter, `EntityMapper`, migration helpers | Data (entities, the context), Web (host) |
| `NSail.Data.Testing` | `HandlerHost` (real pipeline over a throwaway Postgres database), `TestDatabase`, `Sessions`, `TenantPolicyAudit` | test projects |

`NSail.Data` depends on `NSail.Mapping` and the messaging runtime, so a Data project gets the
mapper annotations through it.

## Use it

The Data project references `NSail.Data`; the context applies every registered setup:

```csharp
public class AppDbContext : DbContext, ITenanted, IHasOrgScope
{
    readonly IEnumerable<IDbContextSetup> _setups;
    readonly TenancyProvider _tenancy;
    readonly OrgScopeProvider _orgs;

    public AppDbContext(DbContextOptions<AppDbContext> options, IEnumerable<IDbContextSetup> setups,
        TenancyProvider? tenancy = null, OrgScopeProvider? orgs = null)
        : base(options)
    {
        _setups = setups;
        _tenancy = tenancy ?? TenancyProvider.Install;
        _orgs = orgs ?? OrgScopeProvider.Ambient;
    }

    public Guid? TenantId
    {
        get { return _tenancy.RowKey; }
    }

    public OrgScope OrgScope
    {
        get { return _orgs.Current; }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseAuditStamps();
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

A kit or app contributes its slice of the model with `IDbContextSetup` and registers it:

```csharp
public sealed class DbContextSetup : IDbContextSetup
{
    public void Configure(ModelBuilder modelBuilder) { /* explicit Entity<T>() calls */ }
}

services.AddDbContextSetup<DbContextSetup>();
```

The host replaces `AddDbContext` with one call and applies migrations after `Build()`:

```csharp
services.AddDataAccess<AppDbContext>(connectionString, configuration.Load<TenancyOptions>());

var app = builder.Build();
await app.ApplyMigrations<AppDbContext>();
```

`AddDataAccess` registers a scoped `IDbContextFactory<TDbContext>` (the connection comes from the
scope's `TenancyProvider`), enlists the context in the ambient unit of work and registers
`EntityMapper`. A null `tenancy` is `Tenancy:Mode = None`.

## Configuration

**Connection string.** Yours to bind (the Sample reads `Sample:ConnectionString`). Passed through
`ConnectionStrings.Complete`, which fills a missing `Password` from `PGPASSWORD` and repoints
`Host`/`Port` from `PGHOST`/`PGPORT`; an explicit `Password=` wins.

**`Tenancy` section** (`TenancyOptions`, loaded with `configuration.Load<TenancyOptions>()`):

| Key | Default | Meaning |
|---|---|---|
| `Mode` | `None` | `None`: one customer, install's own database. `MultiDb`: a database per tenant (`{Product}_{Cell}_{tenant}`). `SingleDb`: one database, a `TenantId` column on every entity |
| `Product`, `Cell` | none | Required under `MultiDb`; lowercase alphanumerics and hyphens, refused at startup otherwise |
| `Tenants` | none | Comma-separated slugs for work outside a request. Only read under `SingleDb` |
| `TenantsFile` | none | A file with one slug per line, unioned with `Tenants` and read again on every sweep; replace it atomically |
| `StateCacheSeconds` | `120` | How long this process holds a tenant's state; range 1 to 3600 |
| `SupportContact` | none | Shown as a line on the suspended page |
| `RequireRowSecurity` | `false` | Refuse to start if the Postgres role can bypass the tenant row policies. Valid only under `SingleDb` |

`Connection` and `Rows` are derived from `Mode`, never written. Under `SingleDb` the connection
string may not set Npgsql `Multiplexing` or `No Reset On Close`; startup refuses both.

**Registrations**

| Call | Does |
|---|---|
| `AddDbContextSetup<T>()` | Adds a kit's `IDbContextSetup` |
| `AddTenantSeed<T>()` | An `ITenantSeed`: ordered, named, run-once `TenantSeedStep`s planted on a tenant's first touch (`SingleDb`) |
| `AddTenantCache<T>()` | An `ITenantCache` that forgets a tenant after its first touch |
| `AddWellKnownRows(ids)` | Frozen ids code names by constant; `TenancyProvider.Row(id)` translates them per tenant |
| `AddInstallScopedRows(ids)` | Ids of seeded `IInstallScoped` rows, so `TenancyProvider.Owned(id)` leaves them alone |
| `SetDefaultDbContext<T>()` / `AddUnitOfWork()` | Called by `AddDataAccess`; for a host that composes its context another way |

**Model rules**, applied by `ApplySetups` to every entity:

| Declaration | Effect |
|---|---|
| `IVersioned` (`uint Version`) | Optimistic concurrency token (Postgres `xmin`, no column). `db.ExpectVersion(entity, version)` rides the caller's version into the `UPDATE`; a stale one answers a 409 `Conflict`. Zero means no token |
| `IAudited` (`CreatedAt`, `UpdatedAt`) | Stamped on save by `UseAuditStamps()`; handlers never assign them |
| `IInstallScoped` | Marks reference data the install shares: no tenant column, no filter |
| `IOrgScoped` | Puts the org filter on an entity: reads narrow to the branches in `OrgScope`. `OrgScope.Everywhere` (the default) and `OrgScope.Of(ids)`; `OrgScopeProvider.Enter` and `ReadEverywhere` set it |
| `[CaseSensitive]` | Opts a text column out of the case- and accent-insensitive collation every text column gets (needs PostgreSQL 18+) |
| `[Personal(PersonalData.X)]` / `[NotPersonal]` | Every text column declares one; the kind is written to the column comment |

**Migrations.** `migrationBuilder.AddTenantPolicies("Table")` walls a tenant-scoped table with a
Postgres row-level security policy, in the migration that creates it; `DropTenantPolicies` brackets
a data migration that must touch tenants' rows. Scaffold migrations with the Data project as its own
startup project.

**Stack tables.** `ApplySetups` also maps `AppliedSeedSteps`, `TenantStates` (status, plan,
entitlements, suspension window; written from outside the app) and `DeclaredFeatures`, so a
migration for them lands in your app's chain.

**Tests** (`NSail.Data.Testing`). `TestDatabase` connects to `Host=localhost;Username=postgres` and
caps live fixtures per server (default 4; override with `NSAIL_TESTDB_CONCURRENCY`).
`HandlerHost.Start(prefix, context, compose)` migrates a throwaway database and sends through the
real pipeline with the security gate on; `Sessions.User(partyId)` builds a session.
`TenantPolicyAudit.Unwalled(db)` lists tenant-scoped tables a migration forgot to wall.

## How it fits

- Messaging: the context is the unit of work every outermost send opens ([../Messaging/README.md](../Messaging/README.md)).
- Mapping: `EntityMapper` runs the generated mappers over the scope's context ([../Mapping/README.md](../Mapping/README.md)).
- Security: the org filter follows the message's organization field and the policy that vetted it; `TenantState.Entitlements` is read through `Entitlements` ([../Security/README.md](../Security/README.md)).
- BaseServices: the tenancy middleware resolves the tenant from the `X-NSail-Tenant` header and fills the provider ([../BaseServices/README.md](../BaseServices/README.md)).
- Background: jobs enter one tenant per scope, from `TenantRoster` ([../Background/README.md](../Background/README.md)).

## Going deeper

- Doctrine: [data.md](../../../plugins/nsail-stack/doctrine/data.md), [data-tenancy.md](../../../plugins/nsail-stack/doctrine/data-tenancy.md), [testing.md](../../../plugins/nsail-stack/doctrine/testing.md)
- Guide: [mapping.md](../../../docs/mapping.md), [getting-started.md](../../../docs/getting-started.md)
- Sample: [NSail.Sample.Data](../Sample/NSail.Sample.Data)
- Tests: [NSail.Data.Tests](../../../test/Stack/Data/NSail.Data.Tests), [NSail.Data.Testing.Tests](../../../test/Stack/Data/NSail.Data.Testing.Tests)
