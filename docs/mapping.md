# Mapping

A handler's copy work — a message onto an entity graph, entities back into rows — is declared on
the entity and generated at compile time. The handler keeps only the rules.

This is [Detached Mapper](https://github.com/leonardoporro/Detached-Mapper)'s model, by the same
author, rebuilt as a Roslyn generator: the same graph semantics, no reflection, no runtime
configuration, and a build error where Detached threw at runtime. Detached is no longer
maintained; this is its successor.

## The problem

What arrives over HTTP is disconnected: the `DbContext` does not track it, and an update is a
whole document — a project and its tasks — not a list of changes. Writing it by hand means
loading the graph, matching children by key, adding the new ones, deleting the missing ones and
copying every field, for every entity. That code is long, repetitive and where bugs hide.

## Declare it on the entity

```csharp
[MapFrom(typeof(CreateProject))]
[MapFrom(typeof(UpdateProject))]
[MapTo(typeof(ProjectRow))]
[MapTo(typeof(ProjectModel))]
public class Project : IVersioned, IAudited
{
    public Guid Id { get; set; }

    public uint Version { get; set; }

    public string Name { get; set; } = string.Empty;

    [Composition]
    public List<TaskItem> Tasks { get; set; } = [];

    public Customer? Customer { get; set; }
    ...
}
```

The entity names the message, never the reverse: the Sdk that holds the message cannot see the
Data project. A `[Generated(Mappers.Entities)]` holder in the Data project writes an
`IEntityMapper` per `[MapFrom]` and a projection per `[MapTo]`; `EntityMapper` runs them over the
scope's `DbContext`.

## Call it from the handler

| Call | Does |
|---|---|
| `Map<UpdateProject, Project>(message)` | loads the root by the message's key, graph included, and merges onto it; a missing row is `NotFound` |
| `Upsert<CreateProject, Project>(message)` | the same, creating the row when it does not exist |
| `Import(rows)` | upserts a document whose rows reference each other in no particular order |
| `Project<Project, ProjectRow>(query)` | the query read as rows — an expression tree, so the database selects only the row's columns |

Nothing is saved by the mapper. The handler's order is: **guards**, **the call**, **what the
mapper does not write** (a constant a create sets), **`SaveChanges`**.

## Aggregation and composition

The heart of the model is telling apart what an entity *points at* from what it *owns*.

- **An aggregation is a reference.** `Customer` above is another entity: the message may say
  *which* customer (`CustomerId`), and the mapper attaches it by key — it never writes the
  customer. An `UpdateProject` cannot rename the customer it points at.
- **A composition is owned.** `[Composition] Tasks` are the project's own: created, merged,
  replaced and deleted with it. A collection is matched by key; a child with no key is created
  with a new id; **a child the message no longer carries is deleted**. `[Parent]` on the child
  receives the owner.

**Aggregations are the boundary of a map; compositions are its nodes.** So a message carries the
*whole* set of a composition, never a diff: a client that sends a project without its tasks has
asked for a project without tasks.

## One message per act

C# has no *undefined*: in a general-purpose save, `null` cannot say "leave this alone". Detached
answered with partial-update types and profiles; the Stack answers with small specific messages.
`CreateProject` and `UpdateProject` each carry their own `[MapFrom]`, and an act that changes one
field is its own message with its own mapping. What differs between a create and an update — a
flag a create sets — is a line in the handler after the call, not a mapping profile.

## Concurrency and stamps

- An `IVersioned` entity's `Version` rides in from the message as the expected version, so a
  stale screen's save answers `Conflict` instead of overwriting newer data.
- An `IAudited` entity's `CreatedAt` and `UpdatedAt` are written by the save, never by the
  mapper or the handler.

## What stays hand-written

The mapper copies; it does not decide. Credentials and sealed fields, priced or numbered
documents, state machines and ledgers, and projections that compute (a count, a sum, a subquery)
stay in the handler. A flat `Select(e => new Row { Id = e.Id, Name = e.Name })` is a mapping; a
`Select` with a correlated count is not.

## The compiler as reviewer

A mapping that cannot work does not build: a member with no conversion (`NSG006`), a target with
no parameterless constructor (`NSG007`), a projection that cannot carry a derived type
(`NSG009`), an organization field mapped by name alone (`NSG004`) — the last because writing an
`OrganizationId` is a permission question and has to be said out loud.

Full rules: [data.md](../plugins/nsail-stack/doctrine/data.md) (Mapping), the
[`new-mapping` skill](../plugins/nsail-stack/skills/new-mapping/SKILL.md).
