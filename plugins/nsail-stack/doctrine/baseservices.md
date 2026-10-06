# BaseServices

How NSail bootstraps application hosts. `BaseServices` projects provide shared setup for
API servers, Blazor hosts, and WASM clients; apps and kits call them before their own setup
methods. BaseServices must not reference Kits or Apps.

| Project | Host type |
|---|---|
| `NSail.BaseServices.WebApi` | ASP.NET Core API (minimal APIs, Swagger, error handling) |
| `NSail.BaseServices.WebApp` | Blazor interactive server/WebApp host |
| `NSail.BaseServices.Wasm` | Blazor WebAssembly client |

---

## Composition order

In app hosts:

1. `AddBaseWebApi` / `AddBaseWebApp` / Wasm equivalents
2. `AddHttpClients` (clients only)
3. Kit setup (`AddIamWebApi`, etc.)
4. App setup (`AddOpticalWebApi`, etc.)
5. After `Build()`, the pipeline: `UseBaseWebApi`/`UseBaseWebApp`, and a Blazor host
   closes with `app.MapRazorApp<App>()` — skip it and the host serves no page.

---

## WebApi bootstrap

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddBaseWebApi();
builder.Services.AddOpticalWebApi(builder.Configuration);   // app + kits

var app = builder.Build();
app.UseBaseWebApi();
app.Run();
```

`AddBaseWebApi(builder)` registers: telemetry (`AddTelemetry`, first — see below), messaging
runtime (`AddMessaging`), error handler middleware, authorization services, Swagger/OpenAPI,
and the push's server end — SignalR, the hub, and the tenant as its audience
(`TenantPushAudience`; messaging.md, Pushed to clients).

`UseBaseWebApi(app)` configures, in order:

- `BuildVersionMiddleware` — the build this host is running (`BuildVersion.Current`) on
  **every** response, under `X-NSail-Version`. Outermost, and stamped through
  `Response.OnStarting` rather than on the way in, because `ErrorMiddleware` clears the
  response it rewrites — and a refusal is exactly the answer a client left open across a
  deploy is likeliest to get. It is the whole server half of the handshake: no endpoint, no
  polling. Client half: [messaging.md](messaging.md).
- Error handler
- HTTPS redirection
- `TenancyMiddleware` — **only where a tenant is resolved** (`host.ResolvesTenants()`: either
  switch), so under `Tenancy:Mode=None` the pipeline has no tenancy step. Before authentication
  on purpose: under a per-tenant connection a slug that names no database answers 404 and
  nothing about it is reachable by signing in. See [data-tenancy.md](data-tenancy.md#caddy-assigns-the-tenant-postgres-is-the-registry).
- Authentication + authorization middleware (schemes are registered by kits, e.g. Iam registers cookie auth in `AddIamWebApi`)
- `TenantClaimMiddleware` — the identity half of the tenant wall, **only where a tenant is
  resolved**, like the one above. After authentication, because the claim only exists once the ticket is read,
  and before every endpoint, so a credential minted for another tenant is refused ahead of the
  page, the handler and the circuit alike. The refusal signs the ticket out and challenges, so
  the sign-in behind this same wall stays reachable. See [data-tenancy.md](data-tenancy.md#the-credential-is-stamped-and-checked-on-every-request).
- `ThrottleMiddleware` — the ceiling on a public door, in FRONT of the gate: volume is not a
  permission question, and a flood is refused before any policy evaluation. It reads
  `[Throttled(perMinute)]` off the message the matched route names, so an endpoint whose
  message declares none passes untouched; see messaging.md.
- `CredentialMiddleware` — the wall between a signed ticket and the rows it names. A credential
  outlives them (a party merged away, a user disabled or deleted), and until it is asked the
  session keeps naming the dead id and writes it into every row it touches — a foreign key
  refusal, then a 500 on every page, with nothing telling the person to sign in again. Behind the
  throttle, because the question costs a query and a flood must be refused before it buys one;
  ahead of the gate and every endpoint, because the gate, the page and the circuit all read the
  session this step may have re-issued. It reads the matched endpoint first and skips a static
  asset — `MapStaticAssets` maps behind this middleware, and a cold WebAssembly load is dozens of
  files that can neither write a row nor draw a notice. Installed by every host; what it asks is
  `CredentialProvider`, whose permissive floor leaves a host that composed no identity exactly as
  it was. See [permissions.md](permissions.md#session).
- `EndpointGateMiddleware` — the security gate, ahead of model binding. After authentication so
  it reads the session the pipeline's own gate will read, and before `MapEndpoints` so a caller
  with no permission is refused before a missing body or query member can be. It no-ops unless
  a kit called `AddSecurityEnforcement`; see messaging.md.
- Swagger (development)
- HSTS (production)
- `MapEndpoints()` — maps all generated `IEndpointEntry` implementations
- `MapPush()` — the `PushHub` at `PushFeed.Path`, behind the same tenant wall and
  authentication as every endpoint, so a host that publishes always has the door its clients
  listen on

**The pipeline answers identically in Development and Production.** A framework default keyed
to `IsDevelopment()` is a latent production divergence — a class of shipped defect, not a
hypothetical. Any environment-conditional behavior in the host needs a justifying comment at
its registration site, and the Production smoke probes are the net. That is why the error
handler is registered outside the environment branch: `ErrorMiddleware` is the **only**
exception path, the same in both; a second registration (ASP.NET Core's `UseExceptionHandler`
with a re-execute path) would give production a route Development never exercises.

**A request that dies before render answers a person with a page.** The one exception path
asks `Accept` who is calling: a document navigation gets `NsPageError` under static SSR — the
message, the door home, a Reload anchor and the trace handle a report is filed by — and every
other caller gets the `Problem` JSON (types.md). What draws it is `IProblemDocumentProvider`,
declared in `NSail.Messaging.WebApi` and registered by `AddBaseWebApp`, so a host that composes
no Blazor app resolves none and answers JSON to everybody. `Response.HasStarted` still
rethrows: a render that died mid-stream cannot be rewritten into a page.

**The face renders on a scope of its own, and a face that cannot be drawn is not the request's
fault.** A `NavigationManager` is one per scope and refuses a second `Initialize`, so drawing the
page on the request's own scope works only until the thing that died was a *render* — then the
second one throws `'RemoteNavigationManager' already initialized`, leaves the handler, and the
exception the page exists to report is gone. So `ProblemDocumentProvider` swaps a fresh scope onto
the request for the length of the write, and `ErrorMiddleware` catches whatever the write still
fails at: its own log line, then the `Problem` JSON, which needs no renderer. Pinned by
`ProblemDocumentTests` (`NSail.BaseServices.WebApp.Tests`), whose repro initializes the request's
`NavigationManager` as `EndpointHtmlRenderer` does and then throws.

### Telemetry

**An app has observability by being an NSail app.** `AddBaseWebApi` calls `AddTelemetry`
(`NSail.Telemetry`) and no product wires anything: traces, metrics and logs leave a server
host on one OTLP exporter, over one resource, so a request's logs carry its trace's `traceId`.
Instrumented: ASP.NET Core, EF Core, HttpClient, NSail's own Mediator span, and **Blazor's
own** — .NET 10 ships it on the sources/meters `Microsoft.AspNetCore.Components`,
`.Components.Lifecycle` and `.Components.Server.Circuits`, so there is nothing to install,
only to subscribe.

**The background runner's metrics are subscribed by reference, not literal.**
`NSail.Telemetry` references `NSail.Background` and subscribes `BackgroundJobMetrics.MeterName`
beside the Blazor meters — a subscription to a meter nobody emits on fails silently in
production with a green build, so the name is derived from the declaring constant. The
reference points this way because the reverse would put the OTLP exporter packages behind
every kit that ships a job. The three instruments and why they are metrics rather than log
lines: data-tenancy.md, *Background jobs*.

**The Mediator span is an interceptor**, registered open-generic like `SecurityInterceptor`
and *before* it, so it is the outermost link of `SendPipeline`'s chain and a refusal falls
inside the span. Its name is `{Area}.{Feature}.{Object}` — the permissions rendering, not
`MetadataProvider.KeyFor`, which drops Feature. **It does not wrap the unit of work**:
interceptors are built from the scope `IAmbientSender.Run` opens, so the transaction's
`COMMIT` closes after the span does. Moving that boundary means moving the span out to
`SendPipeline.Execute` — a decision, not a fix. `PublishPipeline` reads the same
interceptors, so a publish is a span for free.

**The destination is configuration; the credential is not.**

```json
{
  "Telemetry": {
    "Endpoint": "COLLECTOR_BASE_URL",
    "InstanceId": "INSTANCE_ID",
    "Tenant": "INSTALL_SLUG",
    "SamplingRatio": 1
  }
}
```

The live values are the operations ledger's (nsail-ops `providers/grafana.md`).

- **`Endpoint`** is a collector's **base**; each signal is posted to `{Endpoint}/v1/traces`,
  `/v1/metrics`, `/v1/logs`, composed by `TelemetryOptions.EndpointFor` — the exporter
  appends a signal path only for an endpoint it read from `OTEL_EXPORTER_OTLP_ENDPOINT`
  itself; one set in code is taken as the whole URL, and every signal would post to the
  gateway's root.
- **`InstanceId`** is for a human debugging the section. Nothing sends it.
- **`ServiceName`** overrides `service.name`; neither shipped host sets it (the product is
  derived from the host assembly). Setting it changes one of the backend's four filters
  (below).
- **`Tenant`** is `nsail.tenant`, the install. A host that names none answers with its
  machine name.
- **`SamplingRatio`** is the whole sampling story: 0 to 1, parent-based, free to differ per
  install and environment. **Never a branch on `IsDevelopment()`** (rule above).
- **`NSAIL_OTLP_AUTH`, in the environment, holds the finished `Authorization` header**,
  scheme included (`Basic …`), used verbatim — nothing concatenated, no instance id read to
  build it. Same seam as `DefaultSmtp` and `ConnectionStrings`.
- **With no credential nothing is collected and nothing is exported.** No exporter is
  registered, so the `ActivitySource` has no listener and costs a `HasListeners` check. A
  dev machine and CI are silent by having no variable, never by a branch. The Mediator
  interceptor is registered either way — the pipeline's shape does not move with config.

**Resource attributes are the backend's filters, so they are not free to invent**:
`service.name` (the product, derived from the host assembly — `NSail.Optical.Web` binds
Area `Optical`), `service.namespace` = `nsail`, `deployment.environment`, `nsail.tenant`
(the `Tenant` setting, else the machine name).

**No personal data reaches telemetry — ids, timings and codes only.** This is shape, not
discipline. NSail's own span never reads a message's members: it carries `nsail.outcome`
(`ok`/`refused`/`failed`) and, on a refusal, `nsail.problem` — the Problem's **Code**, never
its Title, and no `RecordException` and no status description, either of which could quote
the value that caused the refusal. For code NSail did not write, `Scrubber` is a span
processor that drops `url.query`, `db.statement`, `db.query.text` and every
`db.query.parameter.*`, and cuts the query string off `url.full`. A processor rather than
vendor options on purpose: an instrumentation option can be removed or renamed by the next
package version (`SetDbStatementForText`, which suppressed SQL text, was removed from the EF
Core instrumentation), and then fails open silently.

**The rule binds all three signals, and logs are where it bites**: any `ILogger` in the tree
writes log lines, and the OTLP exporter sends every attribute, the exception's message and a
stack trace rendered from `ToString()` (which opens with that message). `ErrorMiddleware` logs
`"Exception mapped to problem {Code}: {Message}"`, and for a refusal that `{Message}` **is**
the Problem's Title. So `LogScrubber`, the logs-side sibling of `Scrubber`, cuts at the export
seam:

- What travels is what the **source** declares — category, level, event id and the **message
  template** (the record's body, a literal at every call site).
- An attribute's value travels only if its **type cannot carry prose**: `Guid`, a number, a
  `bool`, an enum, a date or a duration. Every string is dropped, a code included — a
  Problem's Code and a patient's name are the same type, so the Code rides the span and the
  `traceId` joins the two.
- An exception travels as `exception.type` and its **frames**; the message does not.
- The cut is at the exporter, behind every other logging provider, so the console and the
  file a developer reads keep the whole line.

**Corollary: a log line cannot carry an alert.** Whatever the backend has to *name* — which
job died, which entity a sweep skipped — rides a **metric attribute**, which nothing scrubs,
or does not travel. `BackgroundJobMetrics` applies this (data-tenancy.md, *Background jobs*): the
runner's three failure modes are logged for the console and counted for Grafana, and alerts
are written against the counts. Cardinality bound: a metric attribute is a series per
distinct value, so a job name, an outcome and a tenant slug are fine; a person, an item or an
exception message never are.

### Install verbs: backup and restore

A host answers two verbs before it serves anything. `InstallConsole.Requested(args)` is the
first thing after `builder.Build()` — ahead of `ApplyMigrations`, because a restore replaces
the schema those migrations would run against:

```csharp
if (InstallConsole.Requested(args))
{
    Environment.ExitCode = await InstallConsole.Run<OpticalDbContext>(app, args);

    return;
}
```

```
optical backup [<file or directory>]
optical restore <archive> --confirm RESTORE
```

The product is read from the context type (`OpticalDbContext` is Optical), so no host repeats
its own name.

- **One archive is the install.** `manifest.json` (product, database, Postgres version, last
  applied migration id, whether keys travel), `database.dump` (`pg_dump --format=custom`) and
  `keyring/*.xml`. The key ring lives in `<content root>/data/keyring`
  (`PersistKeysToFileSystem`, wired in `AddBaseWebApi`) rather than the framework default,
  which is machine-dependent and could not travel — without it every sealed secret in a
  restored install is unreadable. That folder is git-ignored.
- **`pg_dump`, not a structured export**: a backup returns an install to its exact state, byte
  for byte; an export that survives a schema change is a different feature. So the runtime
  image carries `postgres-client` at the production server's major.
- **A restore refuses before it writes.** `RestorePlan.For` throws `BackupFailure` on an
  archive whose schema this build does not know (newer), whose product is another product,
  whose Postgres major differs, or whose archive format this build cannot read. Then it says
  what it will overwrite — database and key ring — and stops unless the operator passes
  `--confirm RESTORE` (and types the word again when stdin is a terminal). Exit codes: `0`
  done, `2` usage, `3` not confirmed, `4` refused.
- **Restore is replace, not merge.** The public schema is dropped whole and rebuilt from the
  dump — `pg_restore --clean` would leave behind anything the install grew after the backup —
  and the key ring is emptied before the archive's keys land in it.
- **Both operations are audited** into `nsail_backup_audit`: who, when, product, both version
  stamps, and which archive. It is created if absent by raw SQL rather than being an entity
  with a migration, because it must be writable into a database whose schema is whatever the
  archive carried.

---

## WebApp bootstrap

Blazor host setup for interactive server rendering. Apps call `AddBaseWebApp` and
`UseBaseWebApp` alongside WebApi setup when serving both API and Blazor from the same host.
Entry component: `NsApp.razor`.

- `AddBaseWebApp` registers `TenancyCircuit`, the `CircuitHandler` that pins the request's
  tenant onto the circuit's scope while the connect request is still on the stack — a circuit
  outlives that request, and every later render runs in its own flow. It no-ops under
  `Tenancy:Mode=None`; see [data-tenancy.md](data-tenancy.md#the-credential-is-stamped-and-checked-on-every-request).
- `AddBaseWebApp` also calls `AddMessagePolicies`, which lets a page's `[Authorize<TMessage>]`
  resolve. It lives here rather than in `AddBaseWebApi` because it serves pages: an API-only
  host has none and should not reference a Razor library. The Wasm client registers the same
  pair — see permissions.md.

### MapRazorApp

The Blazor host closes with `app.MapRazorApp<App>()`: it maps the razor components with the
server + WebAssembly render modes, adds every `IRouteContributor`'s assemblies, and installs
the 404 fallback. The fallback is the non-obvious half: `MapRazorComponents` maps only each
`@page`'s templates, so without it ASP.NET answers 404 before any component runs and
`NsRouter` never renders its NotFound. The fallback leaves out `/api` and file-looking paths
(`{*path:nonfile}`), so a missing endpoint or asset keeps its own honest 404.

### Request localization

`UseBaseWebApp` configures it. There is no `UseLocalization` to call and no culture list to
keep: a host that renders Blazor supports localization, and the supported set is **every
language some `IStringSource` carries** — embed `strings.pt.json` and Portuguese exists. A
source whose languages are not known at startup (a database one) returns an empty
`Languages`: `UseRequestLocalization` builds its middleware once, so a language added at
runtime could never reach it.

Three things are not in any source's `Languages` and are declared instead:

```json
{
  "Language": { "Base": "en", "Default": "es", "Offered": ["es", "en"] }
}
```

- **`Base`** is the language the base `strings.json` is written in — the one language that
  cannot be derived (the base file carries no code in its name); every source excludes it
  from `Languages` by contract.
- **`Default`** is a *preference*, not the base: the language the install renders in when
  nobody has chosen another and the browser asks for nothing offered. Conflating the two is a
  real defect: with `Default` doubling as the base, an install defaulting to `es` had a
  supported set of exactly `[es]` and could never serve its own English strings.
- **`Offered`** is what the install serves somebody who has chosen nothing — the set the
  browser votes among. **Unset means `Default` alone**, the common case: an óptica argentina
  offers `[es]`; a practice that attends in two languages says so.

Two derivations in `NSail.Localization` read them. **`Languages.Supported`** (called by the
middleware and `GetLanguageSettings`) is every language the catalogs can render, English
included *by construction* (`Base` is `en`); it is the settings picker's list and what a
forced choice is validated against. **`Languages.Offered`** (middleware only) is the
narrower one, and the only one a browser is resolved against — conflating them served
English, beside a Spanish menu, to anyone whose Chrome was in English.

The language is decided **once per page load, on the server**:

1. the **forced choice** — the `.AspNetCore.Culture` cookie, which may name any `Supported` language;
2. else the **browser's `Accept-Language`**, among `Offered`;
3. else the install's **`Default`**.

**The server decides; the client adopts.**
- `UseRequestLanguage` sets `RequestCultureProviders` to `CookieRequestCultureProvider` then
  `OfferedLanguageProvider` — the stock `AcceptLanguageHeaderRequestCultureProvider` narrowed
  to `Offered`, because the middleware resolves an unfiltered header against
  `SupportedCultures`, which always carries English. A query-string culture stays out of the
  chain: it would move the prerender without moving the client that replaces it.
- `GetLanguageSettings` answers with the language **this request is being served in**
  (`IRequestCultureFeature`, so both the cookie and the browser's vote count), and the WASM
  boot runs `Languages.Negotiate(preference, served)` over it. The client never re-derives
  what the server answered — recomputing it from configuration is how the two halves drift.
  The browser is counted **once**, on the server.
- A saved preference is written by `SaveLanguageSettings`, which stores the row **and** writes
  the `.AspNetCore.Culture` cookie on its own response, persistent for a year: the row is
  what the client boots from, the cookie what the server prerenders from, and one handler
  writes both so they cannot drift.
- The cookie is a **carrier, not a preference** — the server must answer the first byte before
  any of our code runs — and it follows the row: an **authenticated** `GetLanguageSettings`
  sets the device's cookie equal to the user's row (planted, corrected, or deleted), so a
  language forced on one machine reaches every machine that account signs in on. An
  **anonymous** read never touches it (the sign-in page reads it signed out, where a delete
  would erase the choice it came to serve). Accepted gap: signing in on a new device, that
  first session reads in the install's language and the next load in the user's — a forced
  reload is not worth its loading screen.
- `Language` **null means "nobody has chosen"**, never "guess".
- The language never moves mid-session — `Languages.Adopt` is the only thing that sets one;
  it moves `CurrentCulture` and `CurrentUICulture` together, and it is a no-op off the
  browser (a thread default on the server would outlive its request). A late write that moved
  the words without the separators is a bug.

---

## Wasm bootstrap

```csharp
builder.AddHttpClients();              // the Wasm extension — builds the WasmUrlResolver from HostEnvironment
builder.AddPush();                     // the server's push, on the same origin (HubFeed)
builder.Services.AddComponentServices();
builder.Services.AddMessaging();
builder.Services.AddOpticalWasm();     // app-specific
```

Client hosts need HTTP client configuration for remote `Send` via `ISender`.

---

## HTTP clients configuration

`NSail.Messaging.Http` reads `HttpClients` from configuration:

```json
{
  "HttpClients": {
    "Optical": {
      "BaseUrl": "{scheme}://{host}:{port}",
      "TimeoutSeconds": 30
    }
  }
}
```

Each key must match the Area the clients generator derived for that Sdk — `NSail.Optical`
gives `Optical`. Nothing declares it, and the section is an override, not a required list;
see [generation.md](generation.md).

`BaseUrl` is a template. `AddHttpClients` takes an `IUrlResolver` and fills the three
`UrlTokens` — `{scheme}`, `{host}`, `{port}` — once per named client, at registration. A
`BaseUrl` with no token resolves to itself, so an absolute URL still works.

The Wasm client is served by the host it calls, so `WasmUrlResolver` fills the tokens from
`IWebAssemblyHostEnvironment.BaseAddress`: the client follows whatever port served it, and
no session configures its own. When
the port is the scheme's default the resolver drops the colon with it, because `Uri.Port`
reports 443 rather than nothing.
