// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Mapping;

/// <summary>A source type mapped onto an entity type. The generated subclass knows the two types
/// — their keys, how to build one, how to copy members — and this half decides what happens to
/// the graph: create, merge, replace, delete or attach. A semantics bug is fixed here, never by
/// regenerating.</summary>
public abstract class EntityPair<TSource, TTarget> : EntityType<TTarget>
    where TSource : class
    where TTarget : class
{
    /// <summary>The source's key converted to the target's key type, or null when the source
    /// names no row (no key member, or the key's default value).</summary>
    public abstract object? SourceKey(TSource source);

    /// <summary>A new entity carrying the source's key, when it has one.</summary>
    public abstract TTarget Create(TSource source);

    /// <summary>The composed members, as include paths, that a root load must bring.</summary>
    public virtual IReadOnlyList<string> Includes
    {
        get { return []; }
    }

    public abstract Task MapMembers(TSource source, TTarget target, MapContext context, CancellationToken cancellationToken);

    /// <summary>The root: the row the source names, loaded when the caller has none in hand, then
    /// merged — or created, when there is no row and the run may create one.</summary>
    public async Task<TTarget> MapRoot(TSource source, TTarget? target, MapContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);

        var key = SourceKey(source);

        if (target is null && key is not null)
        {
            target = await context.Load(Match(key), Includes, cancellationToken).ConfigureAwait(false);

            if (target is null && context.Stores && context.Parameters.MissingRootBehavior == MissingRootBehavior.Throw)
            {
                throw context.Missing(typeof(TTarget), key);
            }
        }

        if (target is null)
        {
            return await Created(source, key, context, cancellationToken).ConfigureAwait(false);
        }

        // A target in hand with no key yet is a row being written for the first time: it takes
        // the one the source names.
        if (key is not null && TargetKey(target) is null)
        {
            AssignKey(target, key);
        }

        return await Merged(source, target, key ?? TargetKey(target), context, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A part the owner holds: no source deletes it, a different key replaces it, the
    /// same key merges it.</summary>
    public async Task<TTarget?> Compose(TSource? source, TTarget? target, MapContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (source is null)
        {
            if (target is not null)
            {
                context.Deleted(target, TargetKey(target));
            }

            return null;
        }

        var key = SourceKey(source);

        if (key is not null && context.TryGetEntity(key, out TTarget already))
        {
            return already;
        }

        if (target is null)
        {
            return await Created(source, key, context, cancellationToken).ConfigureAwait(false);
        }

        var targetKey = TargetKey(target);

        if (key is null || Equals(key, targetKey))
        {
            return await Merged(source, target, targetKey, context, cancellationToken).ConfigureAwait(false);
        }

        var replacement = await Created(source, key, context, cancellationToken).ConfigureAwait(false);

        context.Deleted(target, targetKey);

        return replacement;
    }

    /// <summary>A reference to a row the owner does not hold: only which row moves. The same key
    /// keeps what is there; another one is attached as the store has it.</summary>
    public async Task<TTarget?> Aggregate(TSource? source, TTarget? target, MapContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var key = source is null ? null : SourceKey(source);

        // An import that cannot order its rows creates the referenced one from what the source
        // carries, instead of refusing a reference to a row that is simply not written yet.
        if (key is not null
            && context.Parameters.MissingAggregationBehavior == MissingAggregationBehavior.Create
            && !(target is not null && Equals(key, TargetKey(target)))
            && !context.TryGetEntity<TTarget>(key, out _)
            && await context.Attach(key, () => Stub(key), cancellationToken).ConfigureAwait(false) is null)
        {
            return await Created(source!, key, context, cancellationToken).ConfigureAwait(false);
        }

        return await AggregateKey(key, target, context, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The owned collection ends as the source says, in its order: matches merged in
    /// place, new keys created, and the rest deleted — or kept, when the run appends.</summary>
    public async Task ComposeMany(IEnumerable<TSource>? source, ICollection<TTarget> target, MapContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(context);

        var existing = Index(target);
        var result = new List<TTarget>();
        var seen = new HashSet<TTarget>(ReferenceEqualityComparer.Instance);

        foreach (var item in source ?? [])
        {
            var key = item is null ? null : SourceKey(item);
            var match = key is not null && existing.Remove(key, out var found) ? found : null;
            var mapped = await Compose(item, match, context, cancellationToken).ConfigureAwait(false);

            // A key named twice is one row, mapped once per run, as anywhere in the graph: the
            // first occurrence says what it holds, and the collection holds it once.
            if (mapped is not null && seen.Add(mapped))
            {
                result.Add(mapped);
            }
        }

        foreach (var left in existing.Values)
        {
            if (context.Parameters.CompositeCollectionBehavior == CompositeCollectionBehavior.Append)
            {
                result.Add(left);
            }
            else
            {
                await Compose(null, left, context, cancellationToken).ConfigureAwait(false);
            }
        }

        Replace(target, result);
    }

    /// <summary>The referenced collection ends naming exactly the rows the source names. A row
    /// it no longer names leaves the collection and stays in the store.</summary>
    public async Task AggregateMany(IEnumerable<TSource>? source, ICollection<TTarget> target, MapContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(context);

        var existing = Index(target);
        var result = new List<TTarget>();
        var seen = new HashSet<TTarget>(ReferenceEqualityComparer.Instance);

        foreach (var item in source ?? [])
        {
            var key = item is null ? null : SourceKey(item);
            var match = key is not null && existing.Remove(key, out var found) ? found : null;
            var mapped = await Aggregate(item, match, context, cancellationToken).ConfigureAwait(false);

            // A key named twice is one row, mapped once per run, as anywhere in the graph: the
            // first occurrence says what it holds, and the collection holds it once.
            if (mapped is not null && seen.Add(mapped))
            {
                result.Add(mapped);
            }
        }

        Replace(target, result);
    }

    async Task<TTarget> Created(TSource source, object? key, MapContext context, CancellationToken cancellationToken)
    {
        var target = Create(source);

        if (key is not null)
        {
            context.Entity(key, target);
        }

        context.Created(target, key ?? TargetKey(target));

        context.PushParent(target);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Maps.Descend().ConfigureAwait(false);
            await MapMembers(source, target, context, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            context.PopParent();
        }

        return target;
    }

    async Task<TTarget> Merged(TSource source, TTarget target, object? key, MapContext context, CancellationToken cancellationToken)
    {
        if (key is not null)
        {
            context.Entity(key, target);
        }

        context.PushParent(target);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Maps.Descend().ConfigureAwait(false);
            await MapMembers(source, target, context, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            context.PopParent();
        }

        context.Updated(target, key);

        return target;
    }

}
