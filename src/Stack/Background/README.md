# Background

Work that runs outside a request, in two shapes: **recurring jobs** that a runner invokes on a
cadence, and **deferred work**, an in-memory fire-and-forget queue for what a handler should not
wait on. Both run as the system session, in the install's default language, and both are
tenant-aware: the runner enters the tenant, the work never does.

## Packages

| Package | What it is | Referenced by |
|---|---|---|
| `NSail.Background` | `IBackgroundJob`, `AddBackgroundJob<T>`, `DeferredWork`, `AddDeferredWork`, the runners and their metrics | Kits that ship a job, hosts; `NSail.Telemetry` subscribes its meters |

## Use it

```xml
<PackageReference Include="NSail.Background" Version="$(NSailStackVersion)" />
```

A job, contributed by the kit that ships it from that kit's own composition method:

```csharp
public sealed class ExpireInvitesJob : IBackgroundJob
{
    public TimeSpan Interval => TimeSpan.FromMinutes(5);

    public TenancyScope Tenancy => TenancyScope.Tenant;

    public async Task Run(CancellationToken cancellationToken)
    {
        // resolve the DbContext here, from the scope's provider, never in the constructor
    }
}

services.AddBackgroundJob<ExpireInvitesJob>();
```

Deferred work:

```csharp
services.AddDeferredWork();

deferred.Enqueue("Acme.Orders.Confirm", tenant, async (services, cancellationToken) =>
{
    // a mail, a template: resolved from services, a scope of its own
});
```

## Configuration

The runners read three things from the host:

| Source | Used for |
|---|---|
| `Background` (`BackgroundOptions`) | `JobTimeout` (default 5 min) is the budget of one pass of a job, one tenant's for a per-tenant job; `JobTimeouts` overrides it per job, keyed by the job type's name; `DeferredTimeout` (default 30 s) is a deferred item's. Each is 1 ms to 1 day; outside that the host does not start. |
| `Language:Default` | The culture every job pass and every deferred item runs in, since no request sets one. Absent configuration keeps the ambient culture. |
| `Tenancy` (`NSail.Data`) | The roster a per-tenant job sweeps (`Tenancy:Tenants`, `Tenancy:TenantsFile`, or the provisioned databases under a per-tenant connection). Under `SingleDb` a declared roster also refuses a request's slug off it with a 404 ([Data](../Data/README.md)). |

**Jobs** (`IBackgroundJob`):

| Member | Meaning |
|---|---|
| `Interval` | How often `Run` is invoked; must be positive. Read once, before the first tick. |
| `Tenancy` | `TenancyScope.Tenant`: one pass per tenant on the roster, each in its own scope with that tenant entered. `TenancyScope.Install`: one pass for the install. No default. |
| `Run` | One tick's work. The job decides what is due ("run often, ask what expires"). |

- `AddBackgroundJob<T>()` calls `AddSecurity()`, registers the job scoped and adds one hosted
  runner however many jobs are contributed. Registering the same job twice does not double its
  loop.
- Each job has its own loop and runs once at startup, then every `Interval`. A failing pass is
  logged and counted; the loop continues. A suspended tenant is skipped, neither a pass nor a
  failure.
- A pass past its budget has its token cancelled and, after a short grace, is abandoned: counted
  failed, logged with job and tenant, and the sweep goes on to the next tenant. A job that ignores
  its token keeps running unobserved, with its scope held open until it ends, so honour it. While it runs, the
  tenant's later ticks are skipped, each counted failed and logged, so a hang holds one scope, not
  one per tick. Keep a job's budget below `Queue:Lease` (or the lease above the budget), or an
  abandoned drain pass leaves rows another drain claims again. A job
  that stops on its own cancellation or timeout is logged as the failure it threw.
- A job must be constructible with no tenant entered: anything that needs one (a `DbContext`) is
  resolved inside `Run`. A job that is not logs "could not be started" and never runs again in that
  process.
- The session is `Session.System()` with no organization, party or memberships, so ids travel in
  the messages the job sends.
- A pass holds a Postgres advisory lock per job and tenant, on a connection of its own, until the
  pass ends (an abandoned one, until it does). A second replica over the same database skips a
  pass that overlaps it: counted `skipped`, not failed, and the job is not overdue for it. The lock
  fences overlap only — replicas tick on their own clocks, so a job still runs once per replica per
  interval. Install-wide work under a per-tenant connection locks on the cluster's maintenance
  database; a host with no Npgsql `DbContext` takes no lock. The durable queue's drain is a job
  like any other, so a tenant's drain runs on one replica at a time and a sibling yields its pass.

**Deferred work** (`DeferredWork`):

- `Enqueue(kind, tenant, work)` returns at once. `kind` is a short stable metric tag, not a
  sentence. The tenant is re-entered on the item's own scope.
- One consumer, one item at a time, each with a `DeferredTimeout` budget; a failure or timeout is
  logged and counted and the queue continues. Work that ignores its token is abandoned after a
  short grace and logged.
- The queue is in memory: an item queued when the process dies is lost. Work that must survive
  goes through a `[Queue]` message instead ([Messaging](../Messaging/README.md)).
- `DrainAll(scopes, ct)` runs everything queued inline, for test hosts that start no runner;
  unlike the runner it lets exceptions escape.

**Metrics.** Names are public constants, so subscribers and alert rules derive from them:

| Meter | Instrument | Tags |
|---|---|---|
| `BackgroundJobMetrics.MeterName` (`NSail.Background`) | `Passes`, `Roster`, `Overdue` (intervals since the last completed pass) | `nsail.job`; `Passes` also `nsail.outcome` (`ok`/`failed`/`skipped`) |
| `DeferredWorkMetrics.MeterName` (`NSail.Background.Deferred`) | `Completed` | `nsail.deferred.kind`, `nsail.outcome` (`ok`/`failed`/`timeout`) |

Failures are metrics because log attributes that are strings are dropped at export; an alert has to
name what broke.

## How it fits

- [Data](../Data/README.md): `TenancyScope`, `Tenant`, `TenantRoster` and the tenant state check come
  from there.
- [Security](../Security/README.md): the system session.
- [Telemetry](../Telemetry/README.md): subscribes both meters; `AddBaseWebApi` composes it.
- [Configuration](../Configuration/README.md) and [Localization](../Localization/README.md): the
  language.
- [Messaging](../Messaging/README.md): the durable `[Queue]` drain is itself a per-tenant job.

## Going deeper

- Doctrine: [data-tenancy.md, Background jobs](../../../plugins/nsail-stack/doctrine/data-tenancy.md#background-jobs-the-runner-sweeps-the-job-says-whose-the-work-is),
  [baseservices.md, Telemetry](../../../plugins/nsail-stack/doctrine/baseservices.md#telemetry)
- Tests: [test/Stack/Background](../../../test/Stack/Background)
