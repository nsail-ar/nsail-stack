// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Mapping;

/// <summary>The hand-written half of an entity mapper: generated code copies members and
/// dispatches, this decides what happens to the graph. The split is the one PolicyHandler
/// already makes — a semantics bug is fixed in this file, never by regenerating.</summary>
public abstract class EntityMapper<TSource, TTarget> : IEntityMapper<TSource, TTarget>
{
    public abstract Task<TTarget> Map(TSource source, TTarget target, TrackerContext context, CancellationToken cancellationToken);

    /// <summary>An association is a reference to something the aggregate does not own, so
    /// only the foreign key moves. The referenced row is never created, updated or deleted
    /// here — pointing at a location type must not be able to rewrite the location type.
    /// Returns the loaded entity when the caller needs the navigation filled.</summary>
    protected static async Task<TReference?> Associate<TReference>(
        object? key,
        TrackerContext context,
        CancellationToken cancellationToken)
        where TReference : class
    {
        ArgumentNullException.ThrowIfNull(context);

        if (key is null)
        {
            return null;
        }

        return await context.Find(typeof(TReference), key, cancellationToken).ConfigureAwait(false) as TReference;
    }

    /// <summary>A composition is a part the aggregate owns, so the target collection ends the
    /// call looking exactly like the source: matched items are updated in place, items the
    /// source no longer carries are removed, and the rest are added.
    ///
    /// Every selector is supplied by the generated caller, which knows both types statically —
    /// the engine reflects over nothing.</summary>
    protected static async Task Compose<TItemSource, TItemTarget, TKey>(
        IEnumerable<TItemSource>? source,
        ICollection<TItemTarget> target,
        Func<TItemSource, TKey> sourceKey,
        Func<TItemTarget, TKey> targetKey,
        Func<TItemSource, TItemTarget> create,
        Func<TItemSource, TItemTarget, Task> map,
        TrackerContext context,
        CancellationToken cancellationToken)
        where TItemTarget : class
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(sourceKey);
        ArgumentNullException.ThrowIfNull(targetKey);
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(context);

        // A null collection is "not sent", not "empty": a partial source must not be read as
        // an instruction to delete every part the caller never mentioned.
        if (source is null)
        {
            return;
        }

        var items = source as IReadOnlyCollection<TItemSource> ?? source.ToList();
        var incoming = items.ToDictionary(sourceKey);

        foreach (var existing in target.ToList())
        {
            if (incoming.TryGetValue(targetKey(existing), out var item))
            {
                await map(item, existing).ConfigureAwait(false);
                continue;
            }

            target.Remove(existing);
            context.Remove(existing);
        }

        var kept = target.Select(targetKey).ToHashSet();

        foreach (var item in items)
        {
            if (kept.Contains(sourceKey(item)))
            {
                continue;
            }

            var added = create(item);

            await map(item, added).ConfigureAwait(false);

            target.Add(added);
            context.Add(added);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}
