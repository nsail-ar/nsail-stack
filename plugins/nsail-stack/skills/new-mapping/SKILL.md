---
name: new-mapping
description: Write or convert a handler so the generated entity mapper copies the data — [MapFrom] a Create/Update message onto an entity graph, [MapTo] a Row/Model/Ref as a projection, EntityMapper.Map/Upsert/Project, compositions and aggregations. Use when a handler would assign `entity.X = message.X` by hand, build `new Entity { ... }` from a message, merge a child collection by key, or `.Select(e => new XRow { ... })`; when adding a Create/Update/List/Get/Lookup to a kit; or when a build says NSG004 to NSG009.
---

# New mapping

A handler's copy work is declared on the entity and generated; the handler keeps the
rules. Read `${CLAUDE_PLUGIN_ROOT}/doctrine/data.md`, *Mapping* (the graph rules, one table of calls)
before you declare anything. The mapper is Detached Mapper's graph semantics generated at
compile time: no reflection, nothing saved until the handler's `SaveChanges`.

## The three pieces

1. **The holder, once per Data project.** `Mappings.cs` with
   `[Generated(Mappers.Entities)] public static partial void Add{Kit}Mappings(this IServiceCollection services);`,
   the `NSail.SourceGenerator` analyzer reference in the csproj (`OutputItemType=Analyzer`),
   and in the project's `Persistence.cs`: `services.Add{Kit}Mappings();` plus
   `services.TryAddScoped<EntityMapper>();`. Precedent: `NSail.Accounting.Data`.
2. **The declaration, on the entity.** The entity names the message, never the reverse (the
   Sdk that holds the message cannot see the Data types):
   - `[MapFrom(typeof(CreateX))]` and `[MapFrom(typeof(UpdateX))]` — writes
   - `[MapTo(typeof(XRow))]`, `[MapTo(typeof(XModel))]`, `[MapTo(typeof(XRef))]` — reads
   - `[Composition]` on a collection the root owns, `[Parent]` on the child's way back,
     `[MapFrom(typeof(XChildModel))]` on the child entity
   - `[MapIgnore]` on a member the handler writes by rule; `[MapFrom(typeof(M), "OrganizationId")]`
     on the member to say it out loud (NSG004 refuses it by name alone)
3. **The call, in the handler.**

| Handler act | Call |
|---|---|
| `Create` with a caller-supplied `Id` | `await _mapper.Upsert<CreateX, X>(message, ct)` — `Map` on an `Id` that names no row is `NotFound` |
| `Update` | guards first, then `await _mapper.Map<UpdateX, X>(message, ct)` — a missing row is `NotFound` for free |
| `List`/`Get`/`Lookup` | `_mapper.Project<X, XRow>(query...)` — an expression tree, the store selects only the row's columns |
| a document whose rows cannot be ordered | `Import` |

## What stays in the handler

The mapper copies; it does not decide. Keep, in this order: **guards** (`Ensure*`, uniqueness,
exists), then **the call**, then **what the mapper does not write** — the constants a Create
sets (`IsEnabled = true`, `IsSystem = false`), anything `[MapIgnore]`d — then `SaveChanges`. Never
the stamps: an entity with `CreatedAt` and `UpdatedAt` implements `IAudited` and the save writes
them (an entity not yet on `IAudited` gets it as part of converting its handler).

- A guard that needs the row's old state (`channel.IsEnabled` before the merge) loads it first;
  the mapper's load returns the same tracked instance and merges onto it.
- A claimed parent the message carries only to be checked (`FromOrganizationId`, `InsurerId`)
  is `[MapIgnore]`d: it is verified against the row, never written.
- Side effects that need the old value (`previousPartyId` for a publish) capture it before the call.

## Graph boundaries

**Aggregations are the boundary of a map; compositions are its nodes.** A member typed as an
entity (or a `CustomerId`) is attached by key and never written: an `UpdateInvoice` cannot
rewrite the `InvoiceType` it points at. A `[Composition]` is the root's own: matched by key,
what the message no longer carries is deleted, a child with no key (`Guid.Empty`) is created
with a new id. So a message carries the **whole** set of a composition, never a diff.

## One message per act

C# has no *undefined*, so `null` cannot mean "leave it". The house answer is Detached's first
one: small specific messages, one per act (`RetargetTenderMethod` carries one field), never a
`Save` and never a patch type. Create and Update are separate messages with their own
`[MapFrom]`, which is also the answer to Detached's *profiles*: what differs between them (a
constant the create sets) is the handler's step after the call. The audit stamps are not: an
`IAudited` entity's save writes them.

## What not to convert

Leave hand-written what is a rule and not a copy: credentials and sealed fields (`User`, the
settings handlers), priced or numbered documents (a sale, a voucher, a work order), state
machines and ledgers, writes that split or snapshot rows, orchestration across kits, and any
projection with a subquery, aggregate or merged source (`Select` that computes). A flat
`Select(e => new XRow { Id = e.Id, Name = e.Name })` is a conversion; a `Select` with a
correlated count is not.

## Prove it with a unit test

A mapping is a copy, so a unit test over `HandlerHost` proves it — no browser. For each
converted handler: create-then-get-then-update round trip; for a composition, the set rules
(kept row, new row gets an id, dropped row gone, stamps kept); for an `IVersioned` entity, a
stale version refused and a zero version not held; a missing row is `NotFound`. Precedents in
`test/Kits/Accounting/NSail.Accounting.WebApi.Tests`: `CurrencyCrudTests`,
`TenderMethodModalitiesTests`, `SalesChannelConcurrencyTests`. A build error is the generator
speaking: `NSG004` an organization axis mapped by name alone, `NSG005` a `[Primitive]` of another
type, `NSG006` a member with no conversion, `NSG007` no parameterless constructor, `NSG008`
a `[MapFrom]` that names a non-class, `NSG009` a projection that cannot carry a derived type,
`NSG010` an open generic.
