// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Mapping;

/// <summary>A mapping run that reaches persisted entities. The three operations below are
/// everything the graph semantics need from persistence, which is what keeps this assembly
/// free of a data vendor and the semantics testable without a database.
///
/// The key is asked for here and declared nowhere else: the store already knows it, so a
/// mapper that re-declared it would be a second copy free to drift.</summary>
public abstract class TrackerContext : MapperContext
{
    protected TrackerContext(IMapperResolver mappers)
        : base(mappers)
    {
    }

    public abstract object? GetKey(object entity);

    public abstract Task<object?> Find(Type type, object key, CancellationToken cancellationToken);

    public abstract void Add(object entity);

    public abstract void Remove(object entity);
}
