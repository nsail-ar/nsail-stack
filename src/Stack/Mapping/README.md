# Mapping

A handler's copy work — a message graph onto entities, entities back into rows — is declared on
the entity and generated at compile time by a Roslyn source generator: no reflection, no runtime
configuration, and a build error where a runtime mapper would throw. The graph semantics are
those of Detached Mapper, by the same author.

## Packages

| Package | What it is | Referenced by |
|---|---|---|
| `NSail.Mapping.Annotations` | The attributes: `[MapFrom]`, `[MapTo]`, `[MapIgnore]`, `[Composition]`, `[Aggregation]`, `[Parent]`, `[Primitive]`, `[Entity]`, `[DiscriminatorName]`, `[DiscriminatorValue]` | Data (entities) |
| `NSail.Mapping` | The runtime the generated code calls: `IEntityMapper<TSource, TTarget>`, `IProjection<TSource, TTarget>`, `MapContext`, `MapParameters`, `MapperException` | Data (through `NSail.Data`) |

Both are AOT-compatible. The store-bound half — loading roots, attaching aggregations, the
concurrency token — is `StoreMapContext` and `EntityMapper` in `NSail.Data`; `NSail.Mapping` alone
runs against `MapContext`, which has no store and maps plain objects.

## Use it

In the Data project: reference `NSail.Data` and the `NSail.SourceGenerator` analyzer, and declare
a holder the generator fills with the registrations:

```csharp
public static partial class Mappings
{
    [Generated(Mappers.Entities)]
    public static partial void AddAppMappings(this IServiceCollection services);
}
```

Call it from the project's registration beside `services.TryAddScoped<EntityMapper>();`. Then
declare on the entity — the entity names the message, never the reverse, because the Sdk that holds
the message cannot see the Data project:

```csharp
[MapFrom(typeof(CreateProject))]
[MapFrom(typeof(UpdateProject))]
[MapTo(typeof(ProjectRow))]
public class Project : IVersioned, IAudited
{
    public Guid Id { get; set; }
    public uint Version { get; set; }
    public string Name { get; set; } = string.Empty;

    [Composition]
    public List<TaskItem> Tasks { get; set; } = [];
}
```

The handler injects `EntityMapper`:

```csharp
await _mapper.Map<UpdateProject, Project>(message, cancellationToken);   // NotFound if the row is missing
await _mapper.Upsert<CreateProject, Project>(message, cancellationToken); // creates when missing
var rows = _mapper.Project<Project, ProjectRow>(query);                   // an expression the store runs
await _db.SaveChangesAsync(cancellationToken);                            // the mapper saves nothing
```

## Configuration

**Attributes** (namespace `NSail.Mapping.Annotations`)

| Attribute | On | Meaning |
|---|---|---|
| `[MapFrom(typeof(Source))]` | entity | Generate a mapper that writes `Source` onto the entity, graph included. Repeatable |
| `[MapFrom(typeof(Source), "Member")]` | property | Read this member from `Source.Member` where the names differ |
| `[MapFrom(..., Ignore = [...])]` | entity | Source members deliberately not stored (silences NSG011) |
| `[MapTo(typeof(Target))]` | entity | Generate the projection that reads the entity as `Target`. Repeatable |
| `[MapTo(typeof(Target), "Member")]` | property | Read into `Target.Member` where the names differ |
| `[MapTo(..., Ignore = [...])]` | entity | Target members deliberately left empty (silences NSG012) |
| `[MapIgnore]` | property | Never written by a mapper, in either direction; the handler writes it |
| `[Composition]` | property | The member is owned: created, merged, replaced and deleted with its root |
| `[Aggregation]` | property | A reference to an entity this one does not own; attached by key, never written. The default for an entity-typed member |
| `[Parent]` | property | The back reference to the composing owner; set by the mapper |
| `[Primitive]` | property | Assigned as is (same type on both sides), not mapped member by member |
| `[Entity]` | class | Marks a type as a row where the generator cannot see a `ModelBuilder.Entity<T>()` for it |
| `[DiscriminatorName("Member")]` / `[DiscriminatorValue(value, typeof(Derived))]` | base type | Which derived type a source becomes |

**Entity mapper calls** (`EntityMapper`, in `NSail.Data`)

| Call | Does |
|---|---|
| `Map<TSource, TEntity>` | Loads the root by the source's key with its graph, merges onto it; a key naming no row is `NotFound`; no key is a create |
| `Upsert<TSource, TEntity>` | The same, creating a missing root; also takes many sources |
| `Import<TSource, TEntity>` | Upsert for documents that cannot order their rows: a reference to a row not written yet is created from the document |
| `Project<TEntity, TRow>(query)` | The query read as rows; the store selects only the row's columns |

**`MapParameters`** (the defaults are what `Map` uses; `Upsert` and `Import` set the missing-row
behaviors): `CompositeCollectionBehavior` (`Merge` deletes what the source no longer carries,
`Append` keeps it), `MissingRootBehavior` (`Throw`, `Create`), `MissingAggregationBehavior`
(`Throw`, `Create`).

**Diagnostics** (the build is the reviewer)

| Id | Severity | Fires when |
|---|---|---|
| NSG004 | error | An organization member would be mapped by name alone; say `[MapFrom(typeof(M), "OrganizationId")]` or `[MapIgnore]` |
| NSG005 | error | A `[Primitive]` member's type differs from the source's |
| NSG006 | error | A paired member has no conversion between the two types |
| NSG007 | error | A created target has no parameterless constructor |
| NSG008 | error | `[MapFrom]` names a type that is not a class |
| NSG009 | error | A projection cannot carry a derived type |
| NSG010 | error | A mapped type is an open generic |
| NSG011 | warning | A `[MapFrom]` source member is written nowhere |
| NSG012 | warning | A settable `[MapTo]` row member is filled by nothing |
| NSG013 | error | An `Ignore` names a member that does not exist |

Warnings can be raised to errors in `.editorconfig` or `WarningsAsErrors`.

## How it fits

- Generation: the `[Generated(Mappers.Entities)]` holder is filled by the source generator ([../Generation/README.md](../Generation/README.md)).
- Data: `EntityMapper` runs the mappers over the scope's `DbContext`; an `IVersioned` root's `Version` rides in as the expected version, and an `IAudited` root is stamped by the save ([../Data/README.md](../Data/README.md)).
- Messaging: the messages and row models live in the Sdk and know nothing of the entities ([../Messaging/README.md](../Messaging/README.md)).

## Going deeper

- Guide: [mapping.md](../../../docs/mapping.md)
- Doctrine: the Mapping section of [data.md](../../../plugins/nsail-stack/doctrine/data.md), and the [`new-mapping` skill](../../../plugins/nsail-stack/skills/new-mapping/SKILL.md)
- Sample: [NSail.Sample.Data](../Sample/NSail.Sample.Data) (`Mappings.cs`, `Entities`)
- Tests: [NSail.Mapping.Tests](../../../test/Stack/Mapping/NSail.Mapping.Tests) (graph rules without a store), [NSail.Data.Tests/Mapping](../../../test/Stack/Data/NSail.Data.Tests/Mapping) (over EF), [NSail.SourceGenerator.Tests/Mappers](../../../test/Stack/Generation/NSail.SourceGenerator.Tests/Mappers) (every diagnostic)
