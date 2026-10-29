# Messaging

Every operation is a message: a plain class that says what someone intends. A caller sends it
through one door, `Mediator`; a handler answers it; and the Stack derives the endpoint, the typed
client, the permission key and the form validation from the message alone, so none of them is
written twice. Events (`Publish`/`Subscribe`) and a durable queue (`[Queue]`) ride the same seam.

## Packages

| Package | What it is | Referenced by |
|---|---|---|
| `NSail.Messaging` | The contract: `IMessage`, `IMessage<TResult>`, `IHandler<,>`, `ISaved` | everything |
| `NSail.Messaging.Annotations` | Attributes read by the generators: `[Http]`, `[AsRoute]`, `[AsQuery]`, `[AsHeader]`, `[Pushed]`, `[Queue]`, `[Throttled]`, `[Unbounded]` | Sdk |
| `NSail.Messaging.Runtime` | `Mediator`, pipelines, in-process sender, publisher, unit of work, validators, headers, `MessageRegistry`, `PackReplayer`; `AddMessaging()` | Web, client, tests (reached through Http, Data, Security, Components) |
| `NSail.Messaging.Http` | `HttpSender`, `AddHttpClients`, the `HttpClients` section | Sdk (target of the generated clients), client (through `NSail.BaseServices.Wasm`) |
| `NSail.Messaging.WebApi` | `IEndpointEntry`, `MapEndpoints`, error middleware, the push hub and event-stream endpoints | Web (through `NSail.BaseServices.WebApi`) |
| `NSail.Messaging.Queue` | The durable queue: `AddQueue`, `AddQueueDelivery`, `QueueOptions` | Web (through `NSail.BaseServices.WebApi`); the project that declares the queue holders |
| `NSail.Messaging.SignalR` | Client side of the push over SignalR: `AddPush(origin)` | a client that listens |
| `NSail.Messaging.Sse` | Client side of the push over Server-Sent Events: `AddPush(origin)` | a client that listens |

A client picks one push transport; it never references both. The generator that reads these
attributes is `NSail.Messaging.SourceGenerator` ([Generation](../Generation/README.md)).

## Use it

The Sdk declares the contract and the holder that generates the client:

```xml
<ItemGroup>
  <PackageReference Include="NSail.SourceGeneration.Annotations" Version="$(NSailStackVersion)" />
  <PackageReference Include="NSail.Messaging.Annotations" Version="$(NSailStackVersion)" />
  <PackageReference Include="NSail.Messaging.Http" Version="$(NSailStackVersion)" />
  <PackageReference Include="NSail.Messaging.SourceGenerator" Version="$(NSailStackVersion)" PrivateAssets="all" />
  <Using Include="NSail.Messaging.Annotations" />
  <Using Include="NSail.Messaging.Annotations.Method" Static="true" />
</ItemGroup>
```

```csharp
[Http(Put, "api/tasks/projects/{id}")]
public class UpdateProject : IMessage
{
    [AsRoute] public required Guid Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
}

public static partial class Clients
{
    [Generated(Http.Clients)]
    public static partial void AddTasksHttpClients(this IServiceCollection services);
}
```

The host owns the handler and three generated holders (`Services.Registration`, `Http.Endpoints`,
`Http.InProcess`); `AddBaseWebApi()` already calls `AddMessaging()` and `UseBaseWebApi()` maps the
endpoints:

```csharp
[Injectable]
public sealed class ProjectHandler : IHandler<UpdateProject>
{
    public Task Handle(UpdateProject message, CancellationToken cancellationToken = default) { ... }
}
```

A WebAssembly client composes the runtime and the generated senders:

```csharp
builder.AddHttpClients();                // NSail.BaseServices.Wasm: reads "HttpClients"
builder.Services.AddMessaging();
builder.Services.AddTasksHttpClients();  // the Sdk's holder
```

Then `await mediator.Send(new UpdateProject { ... })`, `Publish`, or `Subscribe<T>` from anywhere.

## Configuration

### Which sender, and what runs around it

`Mediator.Send` resolves exactly one `ISender<TMessage>`; none is `SenderNotFound`, two is
`MultipleSenders`. The composition chooses:

| Host | Holder | Path |
|---|---|---|
| Owns the handlers | `[Generated(Http.InProcess)]` | `InProcessSender` to the `IHandler` |
| Remote client | `[Generated(Http.Clients)]` | `HttpSender` over HTTP to the generated endpoint |
| Any, for `[Queue]` messages | `[Generated(Queue.Producers)]` | `QueueSender` writes a durable row |

Around the sender, in this order on every host: `IInterceptor<TMessage>` /
`IInterceptor<TMessage, TResult>` (registered as open generics, chained in registration order;
the security gate is one), then the message's DataAnnotations, then every `IValidator<TMessage>`.
Validation is innermost by construction, so an unauthorized caller never reaches a rule.

```csharp
services.AddScoped(typeof(IInterceptor<>), typeof(MyInterceptor<>));
services.AddScoped(typeof(IInterceptor<,>), typeof(MyInterceptor<,>));

[Injectable(As = [typeof(IValidator<UpdateProduct>)])]
public class ProductRule : IValidator<UpdateProduct>   // a Problem refuses, null allows
{ public Task<Problem?> Validate(UpdateProduct message, CancellationToken cancellationToken) { ... } }
```

### Message attributes

| Attribute | On | Meaning |
|---|---|---|
| `[Http(Method, path)]` | class | Verb (`Get`, `Post`, `Put`, `Delete`, `Patch`, `Head`, `Options`) and route. By default `{tokens}` bind from the route, the rest from the query on Get/Delete and from the JSON body otherwise |
| `[AsRoute]` `[AsQuery]` `[AsHeader]` | property | Override where a member binds. A non-`required` query/header/body member is optional: absent leaves the message's own default |
| `[Pushed]` | event | A server publish also reaches every open client of the tenant. The event may carry no public property |
| `[Queue]` | command | The send is durable. Only `IMessage`, never `IMessage<T>`; never beside `[Http]` |
| `[Throttled(perMinute)]` | class | Fixed-window cap per caller over HTTP, for a door reachable by someone never authenticated |
| `[Unbounded(reason)]` | property | Records why a number or code declares no bound; read by a test, no runtime effect |

Validation attributes in `NSail.Messaging.Runtime.Validation`: `[NotFuture]`, `[NotNegative]`,
`[NotPositive]`, `[PositiveAmount]`, `[NotAbove(nameof(Other))]`, `[SearchTerm]` (a `MaxLength`
of `SearchTermAttribute.MaximumLength`), and `[Validated]` to walk into a nested model or each
element of a collection. An attribute implementing `ICodedValidation` names its own problem code
and the catalog sentence that goes with it.

### Headers, unit of work, events

- Headers are an argument, `Send(message, headers, token)` / `Publish(message, headers, token)`,
  read back through the injected `MessageContextAccessor`. Over HTTP they travel as
  `X-NSail-{name}`; `X-NSail-Tenant` and `X-NSail-Version` are reserved. A header is caller
  input: correlate with it, never authorize by it. `MessageHeaders.Source` is the one name the
  Stack writes itself.
- The outermost in-process send opens one scope and one transaction; every nested send joins
  it. `IUnitOfWork` is the store's side; `AfterCommit.Register(Func<Task>)` runs work right
  after the commit and never after a rollback. Parallel sends inside one operation are refused.
- `Publish` is broadcast: `InProcessPublisher<T>` calls every registered `IHandler<T>` and every
  `Subscribe<T>(SubscriptionDelegate<T>)` subscription (dispose it to unsubscribe). `ISaved`
  marks a `*Saved` event that carries the saved row's `Id`.

### `HttpClients` section

`AddHttpClients(IConfiguration, IUrlResolver)` gives every named client the host's own origin, so
nothing has to be listed. An entry overrides one client; its key must equal the Sdk's Area.

```json
{ "HttpClients": { "Tasks": { "BaseUrl": "{scheme}://{host}:{port}", "TimeoutSeconds": 30,
                              "DefaultHeaders": { "X-Example": "value" } } } }
```

`BaseUrl` is a template: `IUrlResolver` fills `{scheme}`, `{host}` and `{port}` (`UrlTokens`);
`WasmUrlResolver` fills them from the page's own address. A client left with no base address
throws `ClientNotConfigured` naming the client and the message.

### `Queue` section (`QueueOptions`)

| Key | Default | Meaning |
|---|---|---|
| `Transport` | empty | Empty or `InProcess`: the durable row is the queue, drained by a background job of this process. `Postgres`, `RabbitMq`, `AmazonSqs`, `AzureServiceBus` are refused at startup |
| `MaxAttempts` | `5` | Attempts before the row is dead-lettered |
| `Backoff` | `00:00:30` | Wait before the first retry; doubles per attempt |
| `Interval` | `00:00:10` | How often the drain looks for due rows, per tenant |
| `BatchSize` | `50` | Rows delivered per pass |

`AddQueue()` (send side) and `AddQueueDelivery()` (drain) are called by the generated
`Queue.Producers` and `Queue.Consumers` holders, so a product without a `[Queue]` message gains no
table (`QueuedMessages`). The project declaring either holder references `NSail.Messaging.Queue`.
Every row the drain finishes is counted on the `nsail.queue.delivered` metric (meter
`NSail.Messaging.Queue`, tagged by message and outcome); `QueueMetrics` names them. Delivery is
at least once.

### Push to clients

- **Server:** `AddPush()` registers both transports and `PushAudience` (the install, or the tenant
  under `AddBaseWebApi`); `MapPush()` maps the hub at `api/push` and the event stream at
  `api/push/sse`, both behind authorization. `AddBaseWebApi`/`UseBaseWebApi` call both.
  Per-kit publishers come from the `Push.Publishers` holder.
- **Client:** the `Push.Clients` holder lists the events a feed may publish; the transport is the
  one project referenced, with `services.AddPush(origin)`. `NSail.Messaging.Sse` takes an optional
  `Action<SseOptions>` (`HttpMessageHandlerFactory`, `Headers`), `NSail.Messaging.SignalR` an
  optional `Action<HttpConnectionOptions>`. A client that composes no transport gets the closed
  `PushFeed` from `AddMessaging()`. Nothing is replayed: a listener re-reads on `PushConnected`.
  The reconnect delays are in `PushRetry`. One process only: a second replica needs a backplane.

### Registry and packs

`AddMessageRegistry()` (called by `AddMessaging()`) builds `MessageRegistry` from the
`MessageRegistration`s the `Policies.Handlers` generator emits, never from a scan. Its key is
`{Area}.{Feature}.{Object}`; `Keys` lists them and `Resolve(key)` returns the type.
`PackReplayer.Replay(pack, PackFailure.Stop | PackFailure.Collect)` sends a `Pack`
(`name`, `description`, `steps` of `message` and `body`) in order; `AlreadyExists` counts as
skipped, so a rerun resumes.

## How it fits

- [Generation](../Generation/README.md) writes the endpoints, clients, senders, push and queue
  holders from these attributes; [Injection](../Injection/README.md) registers the handlers.
- [Security](../Security/README.md) gates every send as an interceptor and over HTTP before binding;
  a publish and what it causes are not gated.
- [Data](../Data/README.md): `SetDefaultDbContext<T>()` enlists the `DbContext` in the unit of work,
  and the queue's row is an entity of the same model. [Mapping](../Mapping/README.md) copies a
  message onto an entity.
- [Types](../Types/README.md) holds `Problem` and `BusinessException`, the refusal every layer speaks;
  [Metadata](../Metadata/README.md) derives a message's name and the Area of its client.
- [BaseServices](../BaseServices/README.md) composes all of it for a host (`AddBaseWebApi`) and a client;
  [Background](../Background/README.md) runs the queue drain; [Telemetry](../Telemetry/README.md) exports its meter.
- [Components](../Components/README.md): a page sends, publishes and subscribes through `NsPartial`.
  [React](../React/README.md) sends the same messages from the generated `sdk.ts`.

## Going deeper

- Guides: [Messages](../../../docs/messages.md), [Sending](../../../docs/sending.md),
  [Events](../../../docs/events.md), [Getting started](../../../docs/getting-started.md)
- Doctrine: [messaging.md](../../../plugins/nsail-stack/doctrine/messaging.md),
  [generation.md](../../../plugins/nsail-stack/doctrine/generation.md)
- Sample: [NSail.Sample.Sdk](../Sample/NSail.Sample.Sdk) (messages, client holder),
  [NSail.Sample.Web](../Sample/NSail.Sample.Web) (handler, host holders)
- Tests: [test/Stack/Messaging](../../../test/Stack/Messaging)
