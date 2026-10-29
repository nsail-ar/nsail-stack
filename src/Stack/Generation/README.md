# Generation

What can be derived from a declaration is never written by hand. Roslyn incremental generators read
the messages, handlers and entities of a project and emit the DI registrations, endpoints, typed
clients, push and queue wiring, permission handlers and entity mappers; a build-time tool does the
same for a TypeScript client. You write a partial method with `[Generated(...)]`; the compiler
fills its body.

## Packages

| Package | What it is | Referenced by |
|---|---|---|
| `NSail.SourceGenerator` | The generators and their diagnostics. **Published as `NSail.Messaging.SourceGenerator`**, an analyzer (no `lib/`, no dependencies) | every project that declares a `[Generated]` holder, with `PrivateAssets="all"` |
| `NSail.SourceGeneration.Annotations` | `[Generated]`, `[Source]` and the target enums `Services`, `Http`, `Push`, `Queue`, `Mappers`, `Policies` | Sdk, Web, Data (any project with a holder) |
| `NSail.TypeScriptGenerator` | A build-time tool, not an analyzer: writes a React client's `sdk.ts` from an Sdk | Sdk that ships a React client, `PrivateAssets="all"` |
| `NSail.SourceGeneration.Testing` | `CompilationUnitBuilder`, to build compilations for generator tests | test projects |

## Use it

Reference the generator once for the solution (`Directory.Build.props`), and the annotations in
each project that declares a holder:

```xml
<ItemGroup>
  <PackageReference Include="NSail.Messaging.SourceGenerator" Version="$(NSailStackVersion)" PrivateAssets="all" />
  <PackageReference Include="NSail.SourceGeneration.Annotations" Version="$(NSailStackVersion)" />
</ItemGroup>
```

A holder is a `partial` method on a `partial` class that takes `IServiceCollection`; the attribute
names what to emit into it:

```csharp
public static partial class Clients
{
    [Generated(Http.Clients)]
    public static partial void AddTasksHttpClients(this IServiceCollection services);
}
```

Then call the method from the project's composition. Nothing is generated for a method without the
attribute.

## Configuration

### Targets

| Holder | Declared in | Emits |
|---|---|---|
| `Services.Registration` | any project with `[Injectable]` types | `Add{Lifetime}` per injectable ([Injection](../Injection/README.md)) |
| `Http.Endpoints` | the host | an `IEndpointEntry` per `[Http]` message that a handler in scope handles |
| `Http.InProcess` | the host | `ISender<T>` to `InProcessSender<T>` per `[Http]` message |
| `Http.Clients` | the Sdk | an `HttpSender` per `[Http]` message, registered as its `ISender<T>` |
| `Push.Publishers` | the host's endpoints holder, with `[Source]` on the Sdk | `AddPush` plus an `IPublisher<T>` to `PushPublisher<T>` per `[Pushed]` event |
| `Push.Clients` | the Sdk, beside `Http.Clients` | a `PushedMessage<T>` per `[Pushed]` event: the closed list a client's feed publishes from |
| `Queue.Producers` | the host's endpoints holder, with `[Source]` on the Sdk | `AddQueue` plus an `ISender<T>` to `QueueSender<T>` per `[Queue]` message |
| `Queue.Consumers` | the host that drains, with `[Source]` on the Sdk | `AddQueueDelivery` plus a `QueueConsumer<T>` per `[Queue]` message |
| `Policies.Handlers` | the Sdk | a `MessageRegistration` and a policy handler per message ([Security](../Security/README.md)) |
| `Mappers.Entities` | the Data project | an `IEntityMapper` per `[MapFrom]` and an `IProjection` per `[MapTo]` ([Mapping](../Mapping/README.md)) |

The project declaring `Queue.Producers` or `Queue.Consumers` references `NSail.Messaging.Queue`:
the emitted code names it.

### Scope: `[Source]`

A holder scans the compilation that declares it. `[Source(Assembly = "...", Pattern = "...")]`
replaces that with the named referenced assembly (repeatable); `Pattern` matches the full type name
by dot-separated segments, `*` for one and `**` for any number. The push and queue holders use it
because the event or command lives in the Sdk and no handler names it for the default scan:

```csharp
[Generated(Push.Publishers)]
[Source(Assembly = "Acme.Tasks.Sdk")]
public static partial void AddTasksPushPublishers(this IServiceCollection services);
```

### What an `[Http]` message derives

- Members bind by `[Http]`'s route tokens, then by verb, then by `[AsRoute]`/`[AsQuery]`/`[AsHeader]`.
  A route token with no route-bound member is `NSG001`.
- A non-`required` query, header or body member is optional: an omitted one is never assigned, so the
  message's own initializer supplies the value. `required` carries into the generated body type.
- Enums cross the wire as names (`JsonOptions.Wire`).
- The client's name is derived: the Area of the class that declares the `Http.Clients` holder, taken
  from `[assembly: MetadataTemplate]` when declared, else the second namespace segment
  ([Metadata](../Metadata/README.md)). The `HttpClients:{Area}` section overrides its address.

### Diagnostics

Ids are `NSG` plus three digits, never reused, tracked in `AnalyzerReleases.Unshipped.md`
(`GeneratorDiagnostics.cs` is the registry). A declaration the generator cannot honour is an error,
not a silent skip.

| Id | Severity | Meaning |
|---|---|---|
| `NSG001` | Error | An `[Http]` route token has no route-bound property |
| `NSG002` | Error | A `[Pushed]` type is not an `IMessage` |
| `NSG003` | Error | A `[Pushed]` event declares a public property |
| `NSG014` | Error | A `[Queue]` message returns a result (`IMessage<T>`) |
| `NSG015` | Error | A `[Queue]` message also carries `[Http]` |
| `NSG004`-`NSG013` | Error / Warning | Entity mapping: see [Mapping](../Mapping/README.md). `NSG011` and `NSG012` are warnings; raise them in `.editorconfig` or `WarningsAsErrors` to refuse them |

### TypeScript client

An Sdk that ships a React client names where `sdk.ts` goes; after every compile the tool reads the
Sdk's own sources and writes the file from the same `HttpModelFactory` the C# client uses:

```xml
<PropertyGroup>
  <TypeScriptOutput>../Acme.Tasks.React/src/sdk.ts</TypeScriptOutput>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="NSail.TypeScriptGenerator" Version="$(NSailStackVersion)" PrivateAssets="all" />
</ItemGroup>
```

The Sdk must declare an `Http.Clients` holder; its messages are the ones carried. The target is
incremental and an unchanged `sdk.ts` is not rewritten. A compile that does not resolve, two
reachable types with one simple name, or an Sdk with no holder fails with `NSTS001`. Inside this
repository an Sdk imports `NSail.TypeScriptGenerator.targets` from the project folder instead of
the package ([NSail.Sample.Sdk](../Sample/NSail.Sample.Sdk/NSail.Sample.Sdk.csproj)).

### Testing a generator

```csharp
var compilation = new CompilationUnitBuilder()
    .AddSourceFiles("./Services/Registration/Sources/**/*.cs")
    .AddFrameworkReferences("net10.0")
    .AddContainingAssembly<GeneratedAttribute>()
    .Build("TestAssembly");

GeneratorDriver driver = CSharpGeneratorDriver.Create(new ServiceRegistrationSourceGenerator());
driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
```

## How it fits

- [Messaging](../Messaging/README.md) owns the attributes the Http, Push and Queue targets read and
  the runtime types they emit against; [Injection](../Injection/README.md) owns `[Injectable]`.
- [Security](../Security/README.md) reads what `Policies.Handlers` registers; [Data](../Data/README.md)
  and [Mapping](../Mapping/README.md) hold `[MapFrom]`/`[MapTo]` and the mapper runtime.
- [React](../React/README.md) consumes `sdk.ts`; [Metadata](../Metadata/README.md) supplies the naming
  template the generators mirror.

## Going deeper

- Guides: [Messages](../../../docs/messages.md), [Getting started](../../../docs/getting-started.md)
- Doctrine: [generation.md](../../../plugins/nsail-stack/doctrine/generation.md),
  [react.md](../../../plugins/nsail-stack/doctrine/react.md) (the TypeScript client)
- Sample: [NSail.Sample.Sdk](../Sample/NSail.Sample.Sdk) (`Http.Clients`, `Policies.Handlers`),
  [NSail.Sample.Web](../Sample/NSail.Sample.Web) (`Services.Registration`, `Http.Endpoints`, `Http.InProcess`),
  [NSail.Sample.Data](../Sample/NSail.Sample.Data) (`Mappers.Entities`)
- Tests: [test/Stack/Generation](../../../test/Stack/Generation)
