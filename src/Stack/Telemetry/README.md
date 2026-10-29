# Telemetry

Traces, metrics and logs for a server host, on one OTLP exporter and one resource, so a request's
logs carry its trace's `traceId`. An app has observability by being a Stack app: `AddBaseWebApi`
calls `AddTelemetry` and nothing else is wired. The rule underneath is that no personal data
reaches telemetry; ids, timings and codes only, enforced at the export seam rather than by
discipline.

## Packages

| Package | What it is | Referenced by |
|---|---|---|
| `NSail.Telemetry` | `AddTelemetry`, `TelemetryOptions`, the message span and interceptor, the scrubbers, `IFailureLog` | `NSail.BaseServices.WebApi`; server hosts only, never a Wasm client |

## Use it

A host on `NSail.BaseServices.WebApi` needs nothing:

```csharp
builder.AddBaseWebApi();    // calls builder.AddTelemetry(...)
```

To compose it alone, name the meters of modules telemetry does not reference:

```csharp
builder.AddTelemetry("MyModule.Meter");
```

Turn export on with configuration and one environment variable:

```json
{
  "Telemetry": {
    "Endpoint": "https://collector.example.com/otlp",
    "Tenant": "acme",
    "SamplingRatio": 1
  }
}
```

```bash
NSAIL_OTLP_AUTH="Basic ..."    # the finished Authorization header value, used verbatim
```

With no credential nothing is exported and nothing is collected: no exporter is registered, so a
dev machine and CI are silent without any environment branch.

## Configuration

`TelemetryOptions`, bound from the `Telemetry` section:

| Key | Default | Meaning |
|---|---|---|
| `Endpoint` | none | The collector's base URL; each signal posts to `{Endpoint}/v1/traces`, `/v1/metrics`, `/v1/logs` (protocol HTTP/protobuf). Export needs this and the credential. |
| `Tenant` | machine name | Resource attribute `nsail.tenant`, the install. |
| `SamplingRatio` | `1` | 0 to 1, parent-based. Never a branch on the environment. |
| `ServiceName` | derived | Overrides `service.name`; by default the product, taken from the host assembly's Area. |
| `InstanceId` | none | For a human reading the section; nothing sends it. |
| `SearchEndpoint` | none | Base URL of the store's search API, where spans are read back. Not the same address as `Endpoint`. |
| `SearchOrganization` | `default` | The store's organization. |
| `SearchStream` | `default` | The store's stream name. |
| `SearchStreamType` | `traces` | Which signal is searched. |

Credentials never come from the section; the setup overwrites `Authorization` and
`SearchAuthorization` from the environment:

| Variable | Use |
|---|---|
| `NSAIL_OTLP_AUTH` | Write credential for the exporter. |
| `NSAIL_OBSERVE_READ` | Read credential for the search; independent of the write one. |

Resource attributes are the backend's filters: `service.name`, `service.namespace` (`nsail`),
`deployment.environment`, `nsail.tenant`.

**Instrumented:** ASP.NET Core, HttpClient, EF Core, Blazor's own sources and meters, the
background module's two meters (`BackgroundJobMetrics.MeterName`, `DeferredWorkMetrics.MeterName`)
and any meter names passed to `AddTelemetry`. `AddBaseWebApi` passes `TenantPolicies.MeterName` and
`QueueMetrics.MeterName`.

**The message span.** `TelemetryInterceptor<>` registers open-generic and wraps every send as a
span named `{Area}.{Feature}.{Object}` (source `NSail.Messaging`), registered whatever the
configuration. It carries only `nsail.outcome` (`ok`, `refused`, `failed`), `nsail.user`, and on a
refusal `nsail.problem` (the Problem's code) and `nsail.rule`, or on a crash `nsail.failure` (the
exception type). It never reads a message's members.

**Scrubbing.**

- `Scrubber` (span processor) drops `url.query`, `db.statement`, `db.query.text` and every
  `db.query.parameter.*`, and cuts the query string off `url.full`.
- `LogScrubber` (log processor, at the exporter only, so console and file keep the full line)
  keeps the category, level, event id and message template. An attribute travels only if its type
  cannot carry prose (`Guid`, numbers, `bool`, enums, dates, durations); every string is dropped.
  An exception travels as `exception.type` and its frames, never its message.
- A log line therefore cannot carry an alert. What the backend must name rides a metric attribute.

**Reading failures back.** `IFailureLog.Read(FailureLogQuery(UserId, Since, Max), ct)` answers one
person's own recent failures, newest first, as `LoggedFailure` (time, operation, problem code, rule,
failure type, trace id). It is registered (as `OpenObserveFailureLog`) only when both
`SearchEndpoint` and `NSAIL_OBSERVE_READ` are set, so resolve it as optional. A store that cannot be
read throws `FailureLogException`.

## How it fits

- [BaseServices](../BaseServices/README.md): composes it, and passes the meters of modules this one
  cannot reference.
- [Background](../Background/README.md): its job and deferred-work metrics are subscribed here.
- [Messaging](../Messaging/README.md): the interceptor sits in the send pipeline, outside the security
  interceptor.
- [Security](../Security/README.md): the acting user on the span.
- [Metadata](../Metadata/README.md): span names and the product derive from `MetadataProvider`.
- [Configuration](../Configuration/README.md): `Load<TelemetryOptions>`.

## Going deeper

- Doctrine: [baseservices.md, Telemetry](../../../plugins/nsail-stack/doctrine/baseservices.md#telemetry)
- Tests: [test/Stack/Telemetry](../../../test/Stack/Telemetry)
