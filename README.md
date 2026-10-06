# NSail Stack

[![NuGet](https://img.shields.io/nuget/vpre/NSail.Messaging.svg)](https://www.nuget.org/profiles/NSail)
[![CI](https://github.com/nsail-ar/nsail-stack/actions/workflows/ci.yml/badge.svg)](https://github.com/nsail-ar/nsail-stack/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A framework for modular monoliths on .NET 10. You declare an operation once, as a message, and
the Stack derives the rest of it: the HTTP endpoint, the dependency-injection wiring, the typed
client a Blazor page sends it through, the TypeScript a React client imports, and the permission
it demands. Data runs on EF Core over PostgreSQL; the UI is Blazor components (MudBlazor
underneath, never in your API) and React packages that speak the same messages.

It is the foundation of the [NSail](https://github.com/nsail-ar) products, published here as MIT
NuGet packages.

> **Status: pre-release (0.x).** Every merge to `main` ships a new version. The API still moves;
> pin a version and read the changes before you bump it.

## What a feature looks like

A message names its route, carries its validation, and says what it answers:

```csharp
[Http(Post, "api/sample/contacts")]
public class CreateContact : IMessage
{
    public required Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 100)]
    public int Rating { get; set; }
}

[Http(Get, "api/sample/contacts")]
public class ListContacts : PagedMessage, IMessage<DataPage<ContactRow>>
{
    [SearchTerm]
    public string? Search { get; set; }
}
```

A handler answers it. Nothing registers the handler or maps the endpoint by hand — the source
generator does both:

```csharp
[Injectable]
public sealed class ContactHandler : IHandler<CreateContact>, IHandler<ListContacts, DataPage<ContactRow>>
{
    public async Task Handle(CreateContact message, CancellationToken cancellationToken = default) { ... }

    public async Task<DataPage<ContactRow>> Handle(ListContacts message, CancellationToken cancellationToken = default) { ... }
}
```

A Blazor page sends the same type — over HTTP from WebAssembly, in process on the server — and
the form validates it with the message's own annotations, on both sides:

```razor
@page "/contacts/new"
@attribute [Authorize<CreateContact>]
@inherits NsPage

<NsForm TModel="CreateContact" Model="_model" OnSubmit="OnSubmit">
    <NsPanel>
        <Content>
            <NsTextField @bind-Value="_model.Name" AutoFocus />
            <NsNumericField TValue="int" @bind-Value="_model.Rating" />
        </Content>
        <Footer>
            <NsSubmit />
        </Footer>
    </NsPanel>
</NsForm>
```

And a React client imports the TypeScript the build writes from those same messages:

```tsx
import { useMediator } from "@nsail/stack";
import { DeleteContact } from "../sdk";

await mediator.send(DeleteContact, { id: row.id });
```

Labels, field names and refusals come from one string catalog keyed by the message, so the two
clients and the server say the same words.

## Try it: the Sample

[`src/Stack/Sample`](src/Stack/Sample/docs/sample.md) is a contacts app — one API, a Blazor
client and a React client over it — built from the Stack alone. It touches every field kind a
form draws and every way a save is refused: client validation, server validation, not found, and
a concurrency conflict.

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download), Node.js with npm, and a
PostgreSQL server on `localhost` (user `postgres`, password in `PGPASSWORD`).

```bash
npm install
dotnet build src/Stack/Sample/NSail.Sample.Web
dotnet run --project src/Stack/Sample/NSail.Sample.Web --launch-profile http
```

Open <http://localhost:4001> for Blazor and <http://localhost:4001/react> for React. The first
run creates the database.

## Use it in your app

Every Stack project is a package on [nuget.org](https://www.nuget.org/profiles/NSail). Pin one
version for all of them, and reference the source generator as an analyzer that does not flow to
your consumers:

```xml
<PropertyGroup>
  <!-- the latest version on the NuGet badge above -->
  <NSailStackVersion>0.1.x</NSailStackVersion>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="NSail.Messaging.WebApi" Version="$(NSailStackVersion)" />
  <PackageReference Include="NSail.Messaging.SourceGenerator" Version="$(NSailStackVersion)" PrivateAssets="all" />
</ItemGroup>
```

The published versions are this repository's `v*` tags.

| Area | Packages |
|---|---|
| Messaging | `NSail.Messaging`, `.Annotations`, `.Runtime`, `.Http`, `.WebApi`, `.SignalR` |
| Generation | `NSail.Messaging.SourceGenerator` (the analyzer), `NSail.TypeScriptGenerator`, `NSail.SourceGeneration.Annotations` |
| Data | `NSail.Data`, `NSail.Mapping`, `NSail.Mapping.Annotations` |
| Hosting | `NSail.BaseServices.WebApi`, `NSail.BaseServices.WebApp`, `NSail.BaseServices.Wasm` |
| UI | `NSail.Components`, `NSail.Components.Mud`, `NSail.Components.DaisyUI` |
| Security | `NSail.Security`, `NSail.Security.Annotations` |
| Services | `NSail.Background`, `NSail.Backup`, `NSail.Caching`, `NSail.Configuration`, `NSail.Localization`, `NSail.Metadata`, `NSail.Settings`, `NSail.Telemetry` |
| Foundations | `NSail.Types`, `NSail.BclExtensions`, `NSail.Injection.Annotations` |
| Testing | `NSail.Data.Testing`, `NSail.SourceGeneration.Testing` |

The React packages (`@nsail/stack`, `@nsail/ui`) live in [`src/Stack/React`](src/Stack/React)
and are consumed from source for now; they are not on npm yet.

## The doctrine, and a Claude Code plugin

The architecture is strict and it is written down: how a message is shaped, how data is mapped,
how tenancy filters, how a screen places its actions and its refusals. The pages live in
[`plugins/nsail-stack/doctrine`](plugins/nsail-stack/doctrine) — start at
[principles.md](plugins/nsail-stack/doctrine/principles.md), and the
[routing table](plugins/nsail-stack/skills/doctrine/SKILL.md) says which page answers which
area.

They ship as a [Claude Code](https://claude.com/claude-code) plugin, with skills for the
everyday work (`new-message`, `new-mapping`, `new-icon`, `convert-to-nsload`, `test-harness`), so
an agent building on the Stack reads the same rules a person does:

```bash
claude plugin marketplace add nsail-ar/nsail-stack
claude plugin install nsail-stack@nsail
```

Or for one session from a checkout: `claude --plugin-dir <checkout>/plugins/nsail-stack`. Pin the
plugin at the same tag as your packages, so the pages describe the code you compile against.

## Repository layout

| Path | What is there |
|---|---|
| `src/Stack/` | One folder per area, one project per package |
| `src/Stack/React/` | The `@nsail/stack` and `@nsail/ui` React packages |
| `src/Stack/Sample/` | The example app |
| `test/Stack/` | Unit, component (bUnit) and PostgreSQL-backed tests |
| `plugins/nsail-stack/` | The doctrine and the skills, as a Claude Code plugin |

## Contributing

```bash
npm install
dotnet build NSail.sln
dotnet test NSail.sln
```

The data tests need PostgreSQL as above (`PGHOST` and `PGPORT` override the host and port).
Read the doctrine page for the area before you change it, and change the page in the same pull
request as the code: [CLAUDE.md](CLAUDE.md) holds the rules every change keeps, including the
license header each source file opens with. Pull requests build, test and pack, and publish
nothing; a merge to `main` publishes every package as the next `0.1.<n>` and tags the commit
([ci.yml](.github/workflows/ci.yml)).

## License

[MIT](LICENSE) © Leonardo Porro and Emmanuel Arias.
