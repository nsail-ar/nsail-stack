// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Linq.Expressions;

namespace NSail.Mapping;

/// <summary>What a mapper needs to know about an entity type alone, with no source in sight: its
/// key, how to stand one in for a row the store holds, and how to find that row. Enough for an
/// aggregation named by a foreign key, which has no source object to map.</summary>
public abstract class EntityType<TTarget>
    where TTarget : class
{
    public abstract object? TargetKey(TTarget target);

    /// <summary>A new entity carrying this key and nothing else: what an aggregation attaches.</summary>
    public abstract TTarget Stub(object key);

    public abstract Expression<Func<TTarget, bool>> Match(object key);

    public abstract void AssignKey(TTarget target, object key);

    /// <summary>The same reference, named by a foreign key alone (<c>CustomerId</c> for
    /// <c>Customer</c>).</summary>
    public async Task<TTarget?> AggregateKey(object? key, TTarget? target, MapContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (key is null)
        {
            return null;
        }

        if (target is not null && Equals(key, TargetKey(target)))
        {
            return target;
        }

        if (context.TryGetEntity(key, out TTarget already))
        {
            return already;
        }

        var attached = await context.Attach(key, () => Stub(key), cancellationToken).ConfigureAwait(false)
            ?? throw context.Missing(typeof(TTarget), key);

        context.Entity(key, attached);

        return attached;
    }

    /// <summary>The referenced collection ends naming exactly the rows these keys name, in their
    /// order. A row it no longer names leaves the collection and stays in the store.</summary>
    public async Task AggregateKeys(IEnumerable<object?>? keys, ICollection<TTarget> target, MapContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(context);

        var existing = Index(target);
        var result = new List<TTarget>();

        foreach (var key in keys ?? [])
        {
            var match = key is not null && existing.Remove(key, out var found) ? found : null;
            var mapped = await AggregateKey(key, match, context, cancellationToken).ConfigureAwait(false);

            if (mapped is not null)
            {
                result.Add(mapped);
            }
        }

        Replace(target, result);
    }

    protected Dictionary<object, TTarget> Index(ICollection<TTarget> target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var index = new Dictionary<object, TTarget>();

        foreach (var item in target)
        {
            if (TargetKey(item) is { } key)
            {
                index.TryAdd(key, item);
            }
        }

        return index;
    }

    // In place rather than a new collection: the owner's collection is the one the store
    // tracks, and a store sees a part leave or arrive by comparing that instance's contents.
    protected static void Replace(ICollection<TTarget> target, List<TTarget> result)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(result);

        target.Clear();

        foreach (var item in result)
        {
            target.Add(item);
        }
    }
}
