# Generation

How NSail uses Roslyn source generation. If the compiler can infer structure from
attributes and conventions, generate the wiring — prefer it over manual wiring when the
pattern is stable.

The developer writes message types, handler classes, and partial setup methods with
`[Generated]`. The generator produces DI registration, minimal API endpoint mapping,
HTTP client senders, the two ends of the SignalR push, and the entity mappers.

---

## Projects

| Project | Role |
|---|---|
| `NSail.SourceGenerator` | Roslyn incremental generators |
| `NSail.SourceGeneration.Annotations` | `[Generated]`, `[Source]` attributes and enums |
| `NSail.SourceGeneration.Testing` | Test helpers for compilation-based generator tests |
| `NSail.Injection.Annotations` | `[Injectable]` attribute |
| `NSail.TypeScriptGenerator` | Build-time tool, not an analyzer: an Sdk runs it over its own sources to write a React client's `sdk.ts`, with `HttpModelFactory`'s model — [react.md](react.md) |

---

## The setup pattern

Kit and App projects use a **partial static setup class** (`Web.cs` → `Web`):

```csharp
public static partial class Web
{
    public static void AddOpticalWebApi(this IServiceCollection services)
    {
        services.AddOpticalServices();    // generated
        services.AddOpticalEndpoints();   // generated
        services.AddIamWebApi(iam);          // manual — compose kits
    }

    [Generated(Services.Registration)]
    private static partial void AddOpticalServices(this IServiceCollection services);

    [Generated(Http.Endpoints)]
    public static partial void AddOpticalEndpoints(this IServiceCollection services);
}
```

Sdk projects generate HTTP clients:

```csharp
[Generated(Http.Clients)]
public static partial void AddOpticalHttpClients(this IServiceCollection services);
```

---

## Generation targets

Defined in `NSail.SourceGeneration.Annotations`:

| Attribute | Enum value | Output |
|---|---|---|
| `[Generated(Services.Registration)]` | `Services.Registration` | `services.AddScoped<Handler>()` for `[Injectable]` types |
| `[Generated(Http.Endpoints)]` | `Http.Endpoints` | `IEndpointEntry` implementations for `[Http]` messages |
| `[Generated(Http.Clients)]` | `Http.Clients` | `HttpSender<TMessage>` subclasses registered as `ISender<TMessage[, TResult]>` |
| `[Generated(Http.InProcess)]` | `Http.InProcess` | `ISender<TMessage[, TResult]> → InProcessSender<...>` for `[Http]` messages |
| `[Generated(SignalR.Hubs)]` | `SignalR.Hubs` | `IPublisher<TMessage> → PushPublisher<TMessage>` for `[Pushed]` messages |
| `[Generated(SignalR.Clients)]` | `SignalR.Clients` | a `PushedMessage<TMessage>` entry per `[Pushed]` message, the closed list a client's `HubFeed` publishes from |
| `[Generated(Mappers.Entities)]` | `Mappers.Entities` | an `IEntityMapper<TSource, TEntity>` per `[MapFrom]` and an `IProjection<TEntity, TRow>` per `[MapTo]` in scope |

Each transport groups its artifacts in one enum. For HTTP: `Clients` is the remote send side, `Endpoints` the receive side, and `InProcess` the local counterpart of `Clients` — same `[Http]` contract, dispatched directly to the handler in the host that exposes the endpoints.

`Http.InProcess` is declared in the kit's WebApi setup (same assembly as the handlers) and called from `Add{Kit}WebApi()`. It makes `Mediator.Send` dispatch locally in the host that owns the handlers — required for server-side prerendering of components that send messages. There is no implicit sender fallback and `[Http]` is the gate — the sender-registration semantics live in [messaging.md](messaging.md) (In-process vs remote).

For SignalR, the same pair over `[Pushed]` instead of `[Http]`, both from one walk so what the
hub sends is exactly what a client can name: `Hubs` is the server side and `Clients` the
client side. `SignalR.Clients` sits beside `Http.Clients` in the kit's Sdk `Clients.cs`
(`Add{Kit}SignalRClients`, called from each app's `Browser.cs`); `SignalR.Hubs` sits beside
`Http.Endpoints` in the WebApi `Endpoints.cs` (`Add{Kit}Hubs`, called from `Add{Kit}WebApi`)
with `[Source(Assembly = "NSail.{Kit}.Sdk")]` — a pushed event is published from the WebApi
but declared in the Sdk, and no handler there names it for the default scan to find. The
generated `Add{Kit}Hubs` composes the push's own services first (`AddPush`: SignalR and the
install's `PushAudience`), so a composition that mounts the kit alone — a handler harness —
resolves every publisher it registers; a host that composed the push earlier keeps its own.
`PushMountTests` (`NSail.Architecture.Tests`) holds both roots of every product to the
`[Pushed]` messages its host carries, so a forgotten holder fails the build instead of leaving
a screen stale. Runtime semantics: [messaging.md](messaging.md) (Pushed to clients).

`Mappers.Entities` sits in the kit's Data project `Mappings.cs` (`Add{Kit}Mappings`, called from
`Add{Kit}Data`), beside the entities that declare `[MapFrom]`/`[MapTo]`. Only keys, construction
and member copying are generated; every graph decision is the runtime's `EntityPair`, so the
emitted file stays a list of assignments a reader can follow. What a mapper does and when a
handler reaches for it: [data.md](data.md#mapping).

A TypeScript client is not a Roslyn target — a source generator can only add C# to the
compilation — so it is a tool an Sdk's build runs after its compile: the same sources, read
with `HttpModelFactory`, beside the `[Generated(Http.Clients)]` holder whose messages it
carries ([react.md](react.md)).

**Planned but not implemented:** `MassTransit.Consumers`, `MassTransit.Producers`. Do not use
these until generators exist.

---

## Injectable

Mark handler and service classes:

```csharp
[Injectable]
public class PrescriptionHandler : IHandler<CreatePrescription> { ... }
```

Optional parameters:

```csharp
[Injectable(Lifetime = ServiceLifetime.Singleton)]
[Injectable(As = [typeof(IMyService)])]
```

The `Services.Registration` generator scans the compilation for `[Injectable]` types and emits registration code.

---

## HTTP messages

Messages define their HTTP surface with attributes from `NSail.Messaging.Annotations`:

```csharp
[Http(Post, "api/prescriptions")]
public class CreatePrescription : IMessage
{
    [Required]
    public string? PatientName { get; set; }
}

[Http(Get, "api/prescriptions/{id}")]
public class GetPrescription : IMessage<Prescription>
{
    [AsRoute]
    public Guid Id { get; set; }
}
```

### Binding attributes

| Attribute | Binding |
|---|---|
| `[AsRoute]` | Path segment |
| `[AsQuery]` | Query string |
| `[AsHeader]` | Request header |
| (default on POST/PUT) | JSON body |

Use `Method` enum (Get, Post, Put, Delete, Patch, Head, Options) — not ASP.NET `[HttpGet]` etc.

Sdk projects import `static NSail.Messaging.Annotations.Method` via global usings.

### The message owns its defaults, the binding respects them

A query or header property that is not `required` binds as an **optional** parameter: the
generated endpoint widens it to carry "absent" and assigns it only when the request
supplied it, so an omitted parameter leaves whatever the message's own declaration gives
the property.

```csharp
public required Guid PartyId { get; set; }   // required in the query string
public int PageIndex { get; set; }           // optional; absent leaves 0
public int PageSize { get; set; } = 10;      // optional; absent leaves 10
```

The default itself is never copied into the generated code — it cannot be, since the
message usually lives in a referenced assembly whose symbols carry no syntax. The
generator emits *no assignment* and lets the initializer run.

**A body member follows the same rule.** A non-`required` member of a generated `*Body`
DTO can hold "absent", and is assigned onto the message only when the JSON supplied it:

| Message declares | The DTO declares | Omitted from the JSON |
|---|---|---|
| `required int Sequence` | `required int Sequence` | binds as required |
| `int Retries { get; set; } = 3` | `int? Retries` | leaves 3 |
| `DateTime? ExpiresOn` | `DateTime? ExpiresOn` | leaves the declared value |
| `string? Notes { get; set; } = "n/a"` | `string? Notes` | leaves `"n/a"` |
| `List<string> Tags { get; set; } = []` | `List<string> Tags = default!` | leaves the list |

**Every** non-`required` body member is optional, whatever its type, as on the query side.
A value type is widened because, declared non-nullable, an omitted member arrives as `0` or
`false` — indistinguishable from a caller who sent them; on the wire such a member is
`nullable: true` in the OpenAPI body schema.

Consequence: an omitted member and an explicit `null` are the same request, so a nullable
member with a non-null default cannot be cleared through the body. A member that must be
clearable needs a default of `null`.

**`required` carries through** to the generated DTO, so the deserializer enforces it and the
OpenAPI body schema lists it. Mark a member `required` when the operation genuinely has no
default for it.

### Enums cross the wire as names

`NSail.Serialization.JsonOptions` is the wire contract. The generated client serializes
with `JsonOptions.Wire`; the host copies `JsonOptions.Converters` into its own pipeline via
`AddMessagingJson()` (called by `AddBaseWebApi`). Enums are **written as names** in both
directions, and numbers are still accepted on read. A query-string enum was always a name,
so the two edges agree.

### The client name is derived, never declared

There is no `[ServiceEndpoint]` attribute — no such type exists. The name of the
`HttpClient` a generated sender asks for is **the Area of the class that declares the
`[Generated(Http.Clients)]` partial method**, resolved from that class's full name:

1. If the compilation declares `[assembly: MetadataTemplate("…")]`, the Area is whatever
   segment that template binds to `{Area}` (see [metadata.md](metadata.md)).
2. Otherwise — no template, an unparseable one, or one that binds no `{Area}` — the Area is
   the **second segment of the namespace**, which is what the default template
   `{Root}.{Area}.{Feature}.*.{Object}` yields anyway.

So `Clients` in `namespace NSail.Optical` produces senders whose name is `Optical`, and
nothing in the Sdk says the word:

```csharp
namespace NSail.Optical;

public static partial class Clients
{
    [Generated(Http.Clients)]
    public static partial void AddOpticalHttpClients(this IServiceCollection services);
}
```

The derived name is emitted as the sender's `Name`, which `HttpSender` passes to
`IHttpClientFactory.CreateClient`.

**The base URL is derived too.** `AddHttpClients` gives every named client the host's own
origin, so a kit served by the host that serves the app needs no configuration. The
`HttpClients` section is an **override** for a kit that lives elsewhere (URL tokens and
`TimeoutSeconds`: [baseservices.md](baseservices.md)):

```json
{
  "HttpClients": {
    "Optical": {
      "BaseUrl": "https://localhost:5001"
    }
  }
}
```

The key must equal the Area for an override to land; nothing has to be listed to work. If a
client still ends up with no base address, `HttpSender` throws
`MessagingProblem.ClientNotConfigured` naming the client and the message — a sender always
builds a relative URI, so that state can never reach the network and must not be quiet.

---

## Diagnostics

A generator that cannot honour what the code declares reports a build error rather than
generating something else. Ids are `NSG` + three digits, allocated sequentially in
`GeneratorDiagnostics` and tracked in `AnalyzerReleases.Unshipped.md`; an id is never
reused, so a suppression keeps meaning the same thing.

| Id | Severity | Meaning |
|---|---|---|
| `NSG001` | Error | An `[Http]` route token has no route-bound property on the message. |
| `NSG002` | Error | A `[Pushed]` type does not implement `IMessage` — only an event can be published, so nothing would ever be pushed. |
| `NSG003` | Error | A `[Pushed]` event declares a public property — the body reaches every seat of the tenant, so a property is a row crossing the org wall. |
| `NSG004` | Error | A `[MapFrom]` would write `OrganizationId` by name alone — moving a row between branches is a decision (org-map.md), so it is named on the member or `[MapIgnore]`d. |
| `NSG005` | Error | A `[Primitive]` member's type differs from its source's — copied whole, it cannot be converted. |
| `NSG006` | Error | A mapped member has no conversion from its source; for a projection, none a query can translate (no parsing). |
| `NSG007` | Error | A mapped type cannot be built: no parameterless constructor, or abstract with no discriminator to pick a derived type. |
| `NSG008` | Error | `[MapFrom]` names a type that is not a class. |
| `NSG009` | Error | A projection cannot carry one of its source's derived types: the row's base declares another discriminator, or no derived type for that value. |
| `NSG010` | Error | `[MapFrom]` or `[MapTo]` sits on an open generic type — generated code names one closed type. |

`NSG001` is an error, not a warning: an unbound token is dropped from the mapped route, so
the endpoint would answer at a URL the message never declared and the mismatch would only
surface as a 404 at runtime.

---

## Source scope

By default, generators scan the current compilation assembly. To scan another assembly,
use `[Source]` on the partial method:

```csharp
[Generated(Services.Registration)]
[Source(Assembly = "NSail.Optical.WebApi")]
private static partial void AddOpticalServices(this IServiceCollection services);
```

Use when registration must pull types from a referenced assembly.

---

## What gets generated

- **Endpoints** — for each `[Http]` message with a matching `[Injectable]` handler, an
  `IEndpointEntry` that maps the route to `Mediator.Send`. Every emitted entry carries two
  things the runtime reads off the route table, never a per-endpoint decision:
  `MessageEndpointMetadata`, which names the message before ASP.NET binds anything (so the
  security gate answers ahead of a binding failure — messaging.md), and `[GeneratedCode]`,
  which lets a sweep tell these apart from the hand-mapped `IEndpointEntry` classes a kit
  writes for a PDF, an asset's bytes or a capability-token feed. The entry also binds the
  request and hands the pipeline the delivery's headers (`X-NSail-*` — messaging.md).
- **HTTP clients** — for each `[Http]` message in an Sdk project with
  `[Generated(Http.Clients)]`, a typed `HttpSender` registered as `ISender<TMessage>`.
- **In-process senders** — for each `[Http]` message in scope of
  `[Generated(Http.InProcess)]`, `services.AddScoped<ISender<...>, InProcessSender<...>>()`.
- **Service registration** — for each `[Injectable]` class, the appropriate
  `IServiceCollection` calls.

---

## Testing generators

```
test/Stack/Generation/
  NSail.SourceGenerator.TestAssets/   # fixture types
  NSail.SourceGenerator.Tests/        # compilation-based tests
```

Use `NSail.SourceGeneration.Testing.CompilationUnitBuilder` to build Roslyn compilations in tests.

---

## Agent rules

- Follow existing generator patterns before inventing new ones; do not implement new
  generators unless explicitly requested.
- Handlers use `[Injectable]` — no hand-written `services.AddScoped<Handler>()` in kit/app
  setup where `[Generated(Services.Registration)]` exists.
- API operations are messages with `[Http]` — no ASP.NET controllers. Route strings are
  defined once, on the message, never duplicated in handlers or clients.
- Client calls: the Sdk has `[Generated(Http.Clients)]` and the client host calls
  `Add{Kit}HttpClients`.
- Server hosts that render components (prerender) need `Http.InProcess` wired via
  `Add{Kit}WebApi` — without it `Mediator.Send` fails with `SenderNotFound`.
- Never use the unimplemented `Generated` values (MassTransit).
- Kits and apps compose through setup chains.
