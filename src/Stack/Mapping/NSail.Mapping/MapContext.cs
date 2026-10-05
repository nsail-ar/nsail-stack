// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Linq.Expressions;

namespace NSail.Mapping;

/// <summary>One mapping run: what was already mapped (so a cycle ends), the owners being mapped
/// (so a part can point back at its parent), and the hooks a store answers. This one answers
/// with no store at all — nothing is loaded and nothing is tracked — which is what mapping
/// plain objects needs; a store's context overrides the hooks.</summary>
public class MapContext
{
    // Reference identity, not value: two equal sources are two targets, and a back-reference is
    // the same instance arriving twice.
    readonly Dictionary<object, object> _mapped = new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<(Type, object), object> _entities = [];
    readonly List<object> _parents = [];

    public MapContext(MapParameters? parameters = null)
    {
        Parameters = parameters ?? new MapParameters();
    }

    public MapParameters Parameters { get; }

    /// <summary>Whether a store stands behind the hooks. Without one there is no row to find, so
    /// a missing root is simply a new object, never an error.</summary>
    public virtual bool Stores
    {
        get { return false; }
    }

    /// <summary>The root as the store holds it, with the parts the mapping will merge already
    /// loaded. Null is a row that does not exist yet.</summary>
    public virtual Task<TEntity?> Load<TEntity>(Expression<Func<TEntity, bool>> match, IReadOnlyList<string> includes, CancellationToken cancellationToken)
        where TEntity : class
    {
        return Task.FromResult<TEntity?>(null);
    }

    /// <summary>The entity an aggregation points at: the row the store already has, never one
    /// written from the source, or null where the store has none. With no store there is
    /// nothing to look up, and the stub — the key and nothing else — stands in.</summary>
    public virtual Task<TEntity?> Attach<TEntity>(object key, Func<TEntity> stub, CancellationToken cancellationToken)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(stub);

        return Task.FromResult<TEntity?>(stub());
    }

    /// <summary>The root the source names does not exist and the run may not create it. A
    /// store's context answers in its own vocabulary — a handler's NotFound.</summary>
    /// <summary>The owner's <paramref name="member"/> in hand before it is merged: a store reads
    /// what its root load did not bring. Nothing to do where there is no store.</summary>
    public virtual Task Loaded(object owner, string member, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public virtual Exception Missing(Type entity, object key)
    {
        return new MapperException($"{entity.Name} {key} does not exist.");
    }

    public virtual void Created(object entity, object? key)
    {
    }

    public virtual void Updated(object entity, object? key)
    {
    }

    public virtual void Deleted(object entity, object? key)
    {
    }

    /// <summary>The value the source says the row had when it was read: the store refuses the
    /// save if the row moved since.</summary>
    public virtual void ExpectToken(object entity, string member, object? value)
    {
    }

    public bool TryGetMapped<TTarget>(object source, out TTarget target)
    {
        if (_mapped.TryGetValue(source, out var found) && found is TTarget typed)
        {
            target = typed;

            return true;
        }

        target = default!;

        return false;
    }

    public void Mapped(object source, object target)
    {
        _mapped[source] = target;
    }

    /// <summary>The entity this run already produced for a type and key: an entity reached twice
    /// in one graph is one row, mapped once.</summary>
    public bool TryGetEntity<TEntity>(object key, out TEntity entity)
        where TEntity : class
    {
        if (_entities.TryGetValue((typeof(TEntity), key), out var found))
        {
            entity = (TEntity)found;

            return true;
        }

        entity = null!;

        return false;
    }

    public void Entity<TEntity>(object key, TEntity entity)
        where TEntity : class
    {
        _entities[(typeof(TEntity), key)] = entity;
    }

    public void PushParent(object parent)
    {
        _parents.Add(parent);
    }

    public void PopParent()
    {
        _parents.RemoveAt(_parents.Count - 1);
    }

    /// <summary>The nearest owner of this type being mapped, other than the entity asking — a
    /// tree's node is pushed before its own members and must not find itself — or null at the
    /// root.</summary>
    public TParent? Parent<TParent>(object self)
        where TParent : class
    {
        for (var i = _parents.Count - 1; i >= 0; i--)
        {
            if (_parents[i] is TParent parent && !ReferenceEquals(parent, self))
            {
                return parent;
            }
        }

        return null;
    }
}
