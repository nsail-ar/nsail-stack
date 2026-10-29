# BaseServices

Host bootstrap. One call on the builder and one on the app give an ASP.NET Core host the Stack's
messaging runtime, error handling, telemetry, tenant and credential walls, throttling, push and
Swagger; a Blazor host adds the render pipeline and request localization; a WebAssembly client
adds its HTTP clients. Apps and kits compose on top of these calls, never around them.

## Packages

| Package | What it is | Referenced by |
|---|---|---|
| `NSail.BaseServices.WebApi` | `AddBaseWebApi` / `UseBaseWebApi`: the API host | Web host |
| `NSail.BaseServices.WebApp` | `AddBaseWebApp` / `UseBaseWebApp` / `MapRazorApp<TApp>`: the Blazor host | Web host that renders Blazor |
| `NSail.BaseServices.Wasm` | `AddHttpClients` for the WebAssembly builder | Wasm client |

`NSail.BaseServices` must not reference a kit or an app.

## Use it

Web host (`Microsoft.NET.Sdk.Web`):

```xml
<PackageReference Include="NSail.BaseServices.WebApi" Version="$(NSailStackVersion)" />
<PackageReference Include="NSail.BaseServices.WebApp" Version="$(NSailStackVersion)" />
```

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddBaseWebApi();
builder.AddBaseWebApp();                       // only if the host renders Blazor

builder.Services.AddSampleWebApi(builder.Configuration);   // your kits and app

var app = builder.Build();

await app.ApplyMigrations<SampleDbContext>();

app.UseBaseWebApi();
app.UseBaseWebApp();
app.MapRazorApp<App>();                        // without it the host serves no page

app.Run();
```

Wasm client:

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.AddHttpClients();
builder.Services.AddComponentServices();
builder.Services.AddMessaging();

await builder.Build().RunAsync();
```

A host that offers `backup` and `restore` calls `InstallConsole.Requested(args)` right after
`Build()` and before `ApplyMigrations`; see [Backup](../Backup/README.md).

## What each call does

`AddBaseWebApi` registers telemetry (`AddTelemetry`), the messaging runtime, the error handler,
the middleware below, `SecurityOptions` (from `Security`), the tenant feature provider and a
hosted service that publishes the declared features, the push server end with the tenant as its
audience, authorization, metadata, Swagger with schema ids derived from `MetadataProvider`, and
a DataProtection key ring persisted under `<content root>/data/keyring` (so a backup carries it).
It sets `RouteHandlerOptions.ThrowOnBadRequest` in every environment so a binding failure takes
the same exception path everywhere.

`UseBaseWebApi` builds the pipeline in this order:

1. Forwarded headers, only when `ASPNETCORE_FORWARDEDHEADERS_ENABLED` is true (`X-Forwarded-For`
   and `X-Forwarded-Proto`, any proxy trusted).
2. `BuildVersionMiddleware`: `BuildVersion.Current` on every response as `X-NSail-Version`.
3. Error handler, then HTTPS redirection.
4. `TenancyMiddleware` and `TenantStateMiddleware`, only where the host resolves a tenant.
5. Authentication and authorization.
6. `TenantClaimMiddleware`, only where the host resolves a tenant.
7. `TicketEpochMiddleware`, every host: refuses a ticket stamped with a generation other than
   `Security:TicketEpoch` (0 refuses nothing).
8. `ThrottleMiddleware`: a per-caller, per-message fixed window of one minute, read from the
   message's `[Throttled(perMinute)]` attribute; messages without it pass.
9. `CredentialMiddleware`: asks `CredentialProvider` whether the ticket still names a live party and signs the caller out and challenges if not (the default provider holds every ticket).
10. `EndpointGateMiddleware`: the security gate, ahead of model binding; a no-op unless a kit called
    `AddSecurityEnforcement`.
11. Swagger UI in Development, HSTS otherwise.
12. `MapEndpoints()` and `MapPush()`.

`AddBaseWebApp` registers Razor components with server and WebAssembly render modes, the
`TenancyCircuit` that carries the tenant onto the circuit, `AddMessagePolicies` (so a page's
`[Authorize<TMessage>]` resolves), the per-request `IDeviceProvider` and the
`IProblemDocumentProvider` that draws the error page for a document navigation.
`UseBaseWebApp` adds request localization, antiforgery, `Cache-Control: no-cache` on every HTML
document and `MapStaticAssets`. `MapRazorApp<TApp>` maps the components with both render modes,
adds the assemblies of every `IRouteContributor` and installs the 404 fallback that lets the
router render its NotFound (it leaves `/api` and file-looking paths alone).

## Configuration

| Section | Keys | Read by |
|---|---|---|
| `Language` | `Base` (default `en`), `Default` (default `en`), `Offered` (default empty, meaning `Default` alone) | `UseBaseWebApp` request localization, the suspended page |
| `Security` | `TicketEpoch` (int, default 0) | `TicketEpochMiddleware` |
| `Tenancy` | `Mode`, `StateCacheSeconds`, `SupportContact`, ... | the tenant steps; options are `TenancyOptions` in `NSail.Data` |
| `Telemetry` | see [Telemetry](../Telemetry/README.md) | `AddBaseWebApi` |
| `HttpClients` | `<Area>:BaseUrl`, `<Area>:TimeoutSeconds` | `AddHttpClients` (Wasm), from `NSail.Messaging.Http` |

```json
{
  "Language": { "Base": "en", "Default": "es", "Offered": ["es", "en"] },
  "Security": { "TicketEpoch": 0 },
  "HttpClients": { "Tasks": { "BaseUrl": "{scheme}://{host}:{port}", "TimeoutSeconds": 30 } }
}
```

- **Language.** The supported set is every language some `IStringSource` carries; `Base` names the
  language of the base `strings.json`, `Default` is the install's preference, `Offered` is what a
  browser's `Accept-Language` may choose among. The language is the `.AspNetCore.Culture` cookie,
  else `Accept-Language` among `Offered`, else `Default`. Details: [Localization](../Localization/README.md).
- **HttpClients.** Each key is the Area the clients generator derived for an Sdk. `BaseUrl` is a
  template: `{scheme}`, `{host}` and `{port}` are filled at registration by the `IUrlResolver`;
  in a Wasm client `WasmUrlResolver` fills them from the address that served the client, so the
  client follows whatever port served it. A `BaseUrl` with no token is used as is.
- **Wasm transport.** `NSail.BaseServices.Wasm` references no push transport; a client that
  listens references `NSail.Messaging.Sse` or `NSail.Messaging.SignalR` and calls that project's
  `AddPush`. What the project references rides in every client's download, so
  `WasmDownloadClosureTests` pins its reference closure.

## How it fits

- Messaging: the runtime, error handler, throttle, gate and push come from
  [Messaging](../Messaging/README.md); a message declares `[Throttled]` there.
- Data: tenancy modes, tenant state and the migration step are [Data](../Data/README.md).
- Security: the gate, `SessionProvider` and credentials are [Security](../Security/README.md).
- Telemetry, Backup, Configuration: referenced directly: [Telemetry](../Telemetry/README.md),
  [Backup](../Backup/README.md) (key ring), [Configuration](../Configuration/README.md) (`Load<T>`).
- UI and strings: `NsApp`, the router and the catalogs are [Components](../Components/README.md)
  and [Localization](../Localization/README.md).

## Going deeper

- Guide: [Getting started, the host](../../../docs/getting-started.md#5-the-host)
- Doctrine: [baseservices.md](../../../plugins/nsail-stack/doctrine/baseservices.md),
  [data-tenancy.md](../../../plugins/nsail-stack/doctrine/data-tenancy.md)
- Sample: [NSail.Sample.Web](../Sample/NSail.Sample.Web/Program.cs) (host),
  [NSail.Sample.Wasm](../Sample/NSail.Sample.Wasm/Program.cs) (client)
- Tests: [test/Stack/BaseServices](../../../test/Stack/BaseServices)
