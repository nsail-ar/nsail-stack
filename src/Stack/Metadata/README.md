# Metadata

Area and feature information for a type, derived from its **namespace**. The namespace is the one
piece of metadata that cannot drift from the code, so localization keys, settings keys, Swagger
tags and schema ids are all computed from it through one door, `MetadataProvider`, instead of
being declared per type.

## Packages

| Package | What it is | Referenced by |
|---|---|---|
| `NSail.Metadata` | `MetadataProvider`, `TypeMetadata`, `MetadataTemplateAttribute`, `AddMetadata()` | Web, client (through Localization, Messaging, Security, Settings, Telemetry) |

It has no dependency beyond `Microsoft.Extensions.DependencyInjection.Abstractions`. The source
and TypeScript generators read the same template attribute from the compilation, so generated
code and runtime agree.

## Use it

Most apps never call it directly: `AddLocalization()` and the other Stack registrations call
`AddMetadata()` for you. To ask it something:

```csharp
services.AddMetadata(); // TryAdd: any number of callers, any order

var metadata = provider.Get(typeof(Acme.Tasks.Projects.Models.ProjectRow));
// Root = "Acme", Area = "Tasks", Feature = "Projects", Object = "ProjectRow"

provider.KeyFor(typeof(ProjectRow));        // "Tasks.ProjectRow"
provider.KeyFor(typeof(CreateProject), "Name"); // "Tasks.CreateProject.Name"
provider.KeyFor(ProjectStatus.Open);        // "Tasks.ProjectStatus.Open"
```

## Configuration

**The template** is declared per assembly. `AddMetadata()` takes no parameters.

```csharp
[assembly: MetadataTemplate("{Root}.{Area}.*.{Object}")]
```

- Tokens: `{Root}`, `{Area}`, `{Feature}`, `{SubFeature}`, `{Object}`. A token may appear once.
- At most one `*`, which swallows the segments nobody cares about (`Models`, `Handlers`, ...).
  Tokens before it bind from the start of the namespace, tokens after it from the end
  (`{Object}` is the type name). A token with no segment stays `null`.
- An assembly without the attribute follows `MetadataTemplateAttribute.Default`,
  `{Root}.{Area}.{Feature}.*.{Object}`, so foreign code with a different shape coexists with
  NSail-shaped code in one app.
- To cover every project of a solution, add the attribute once in `Directory.Build.props` as an
  `<AssemblyAttribute Include="NSail.Metadata.MetadataTemplateAttribute">` item.
- An invalid template (unknown token, repeated token, more than one `*`) throws
  `ArgumentException` the first time a type of that assembly is asked for.

**The policy** is `MetadataProvider`. Subclass it and override `Get(Type)` to patch an
inconsistency or map third-party types to an area; every consumer follows, because `KeyFor` is
defined in terms of `Get`. Register the subclass before any `AddMetadata()` runs so `TryAdd`
keeps it:

```csharp
services.AddSingleton<MetadataProvider, AcmeMetadataProvider>();
services.AddMetadata();
```

| Member | Role |
|---|---|
| `Get(Type)` | Virtual; the policy door. Cached per type; template from the type's assembly. |
| `KeyFor(Type, member?)` | Virtual; `{Area}.{Object}[.{member}]`, `{Object}` falling back to the type name. |
| `KeyFor(Enum)` | `{Area}.{Enum}.{Value}`. |
| `FromName(string)` | The default template applied to a bare full name; no policy, no assembly attribute. |
| `Parse` / `Bind` | Protected helpers for a provider that resolves templates its own way. |

Generic types drop their arity (`Foo<T>` is `Foo`).

## How it fits

- Localization keys come from `KeyFor`: see [Localization](../Localization/README.md).
- Components derive field and column labels from binding expressions through it:
  [Components](../Components/README.md).
- Messaging's registry renders a message's permission name `{Area}.{Feature}.{Object}` from it
  ([Messaging](../Messaging/README.md), [Security](../Security/README.md)); Swagger tags and
  schema ids follow it too, and Telemetry names spans with it.
- Settings storage keys are `{Area}.{Type}`: [Settings](../Settings/README.md).
- The generated HTTP clients take their `HttpClients:{Area}` config section name from the Sdk
  namespace at generation time; the generator honors the same attribute but not a runtime `Get`
  override ([Generation](../Generation/README.md)).

## Going deeper

- Doctrine: [metadata.md](../../../plugins/nsail-stack/doctrine/metadata.md)
- Tests: [test/Stack/Metadata](../../../test/Stack/Metadata)
