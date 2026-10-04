// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Mapping;

namespace NSail.Mapping.Tests;

// Synthetic pairs, deliberately not a kit's real types: the engine is being tested, not
// Directory. The shapes mirror the case the hand-written PartyHandler merge covers — an
// owned collection behind a link, plus a reference to a table nobody may rewrite.

public sealed class ItemSource
{
    public Guid Id { get; set; }

    public string? Name { get; set; }
}

public sealed class ItemTarget
{
    public Guid Id { get; set; }

    public string? Name { get; set; }
}

public sealed class OwnerSource
{
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public Guid? KindId { get; set; }

    public List<ItemSource>? Items { get; set; }
}

public sealed class OwnerTarget
{
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public Guid? KindId { get; set; }

    public KindTarget? Kind { get; set; }

    public List<ItemTarget> Items { get; set; } = [];
}

/// <summary>The association's far side: a reference row the mapper may point at and must
/// never write.</summary>
public sealed class KindTarget
{
    public Guid Id { get; set; }

    public string? Name { get; set; }
}

/// <summary>A tracked store standing in for the DbContext, so the graph semantics are unit
/// tested with no database and no EF: the tracker's three operations are the whole
/// dependency.</summary>
public sealed class FakeTracker : TrackerContext
{
    readonly Dictionary<(Type, object), object> _rows = [];

    public FakeTracker(IMapperResolver? mappers = null)
        : base(mappers ?? new EmptyResolver())
    {
    }

    public List<object> Added { get; } = [];

    public List<object> Removed { get; } = [];

    public void Seed(Type type, object key, object row)
    {
        _rows[(type, key)] = row;
    }

    public override object? GetKey(object entity)
    {
        return entity switch
        {
            ItemTarget item => item.Id,
            OwnerTarget owner => owner.Id,
            KindTarget kind => kind.Id,
            _ => null
        };
    }

    public override Task<object?> Find(Type type, object key, CancellationToken cancellationToken)
    {
        return Task.FromResult(_rows.TryGetValue((type, key), out var row) ? row : null);
    }

    public override void Add(object entity)
    {
        Added.Add(entity);
    }

    public override void Remove(object entity)
    {
        Removed.Add(entity);
    }

    sealed class EmptyResolver : IMapperResolver
    {
        public IMapper<TSource, TTarget> Get<TSource, TTarget>()
        {
            throw new InvalidOperationException($"No mapper registered for {typeof(TSource).Name} -> {typeof(TTarget).Name}.");
        }
    }
}

/// <summary>Stands in for what the generator will emit: member copying and dispatch, with
/// every graph decision delegated to the base.</summary>
public sealed class OwnerMapper : EntityMapper<OwnerSource, OwnerTarget>
{
    public override async Task<OwnerTarget> Map(OwnerSource source, OwnerTarget target, TrackerContext context, CancellationToken cancellationToken)
    {
        target.Id = source.Id;
        target.Name = source.Name;

        target.KindId = source.KindId;
        target.Kind = await Associate<KindTarget>(source.KindId, context, cancellationToken);

        await Compose(
            source.Items,
            target.Items,
            item => item.Id,
            item => item.Id,
            item => new ItemTarget { Id = item.Id },
            (item, entity) =>
            {
                entity.Name = item.Name;
                return Task.CompletedTask;
            },
            context,
            cancellationToken);

        return target;
    }
}
