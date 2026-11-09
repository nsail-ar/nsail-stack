// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NSail.Mapping;
using NSail.Problems;

namespace NSail.Data;

/// <summary>A mapping run over a DbContext: the root is loaded with the parts it will merge, a
/// reference is the row the store has (tracked first, then queried — the tenant wall and the
/// org filter included), and what the graph creates or deletes is added to or removed from the
/// context, to be written by the caller's SaveChanges.</summary>
public sealed class StoreMapContext : MapContext
{
    readonly DbContext _db;
    readonly List<object> _written = [];
    readonly Dictionary<Type, CreatedRows> _created = [];

    sealed class CreatedRows
    {
        public Dictionary<object, object> ByKey { get; } = [];

        public List<object> Unkeyed { get; } = [];
    }

    public StoreMapContext(DbContext db, MapParameters? parameters = null)
        : base(parameters)
    {
        ArgumentNullException.ThrowIfNull(db);

        _db = db;
    }

    public override bool Stores
    {
        get { return true; }
    }

    public override async Task<TEntity?> Load<TEntity>(EntityType<TEntity> type, object key, IReadOnlyList<string> includes, CancellationToken cancellationToken)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(key);

        // A row this unit of work created is not in the database yet, and all of it is in
        // memory: an import that names it twice merges onto it instead of adding it again.
        if (Created<TEntity>(type, key) is { } created)
        {
            return created;
        }

        return await Query(type.Match(key), includes, cancellationToken).ConfigureAwait(false);
    }

    public override async Task<TEntity?> Load<TEntity>(Expression<Func<TEntity, bool>> match, IReadOnlyList<string> includes, CancellationToken cancellationToken)
        where TEntity : class
    {
        var created = _db.ChangeTracker.Entries<TEntity>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .FirstOrDefault(match.Compile());

        if (created is not null)
        {
            return created;
        }

        return await Query(match, includes, cancellationToken).ConfigureAwait(false);
    }

    // The Added rows of a type, by key, built from the tracker once and kept current by Created.
    // A row whose key the store had not given yet waits in Unkeyed until a Load asks.
    TEntity? Created<TEntity>(EntityType<TEntity> type, object key)
        where TEntity : class
    {
        if (!_created.TryGetValue(typeof(TEntity), out var state))
        {
            state = new CreatedRows();
            _created[typeof(TEntity)] = state;

            foreach (var entry in _db.ChangeTracker.Entries<TEntity>().Where(e => e.State == EntityState.Added))
            {
                state.Unkeyed.Add(entry.Entity);
            }
        }

        if (state.Unkeyed.Count > 0)
        {
            state.Unkeyed.RemoveAll(row =>
            {
                if (type.TargetKey((TEntity)row) is not { } rowKey)
                {
                    return false;
                }

                state.ByKey.TryAdd(rowKey, row);

                return true;
            });
        }

        return state.ByKey.TryGetValue(key, out var found) && _db.Entry(found).State == EntityState.Added
            ? (TEntity)found
            : null;
    }

    async Task<TEntity?> Query<TEntity>(Expression<Func<TEntity, bool>> match, IReadOnlyList<string> includes, CancellationToken cancellationToken)
        where TEntity : class
    {
        IQueryable<TEntity> query = _db.Set<TEntity>();

        foreach (var path in includes)
        {
            query = query.Include(path);
        }

        // One query per collection rather than their product: a root with two owned lists of
        // ten rows each would otherwise come back as a hundred.
        if (includes.Count > 1)
        {
            query = query.AsSplitQuery();
        }

        return await query.FirstOrDefaultAsync(match, cancellationToken).ConfigureAwait(false);
    }

    public override async Task<TEntity?> Attach<TEntity>(object key, Func<TEntity> stub, CancellationToken cancellationToken)
        where TEntity : class
    {
        var values = key is ITuple tuple
            ? Enumerable.Range(0, tuple.Length).Select(i => tuple[i]).ToArray()
            : [key];

        return await _db.FindAsync<TEntity>(values, cancellationToken).ConfigureAwait(false);
    }

    public override async Task Loaded(object owner, string member, CancellationToken cancellationToken)
    {
        if (_db.Model.FindEntityType(owner.GetType()) is null)
        {
            return;
        }

        var entry = _db.Entry(owner);

        // A row this run is creating has nothing in the store to read.
        if (entry.State is EntityState.Added or EntityState.Detached)
        {
            return;
        }

        var navigation = entry.Navigations.FirstOrDefault(n => n.Metadata.Name == member);

        if (navigation is not null && !navigation.IsLoaded)
        {
            await navigation.LoadAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public override Exception Missing(Type entity, object key)
    {
        return new BusinessException(BusinessProblem.NotFound(entity, key));
    }

    public override void Created(object entity, object? key)
    {
        if (_db.Entry(entity).State == EntityState.Detached)
        {
            _db.Add(entity);
        }

        foreach (var (type, state) in _created)
        {
            if (!type.IsInstanceOfType(entity))
            {
                continue;
            }

            if (key is null)
            {
                state.Unkeyed.Add(entity);
            }
            else
            {
                state.ByKey.TryAdd(key, entity);
            }
        }

        _written.Add(entity);
    }

    public override void Updated(object entity, object? key)
    {
        _written.Add(entity);
    }

    public override void Deleted(object entity, object? key)
    {
        _db.Remove(entity);
        _written.Add(entity);
    }

    /// <summary>The root's concurrency token stands for the whole graph. Postgres moves a row's
    /// version only when that row is written, so a run that changed only the parts writes the
    /// root as well: the save checks the version the caller read, and moves it for the next
    /// caller. A run that changed nothing writes nothing.</summary>
    public void WriteRootWithParts(object root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var entry = _db.Entry(root);

        if (entry.State is not (EntityState.Unchanged or EntityState.Modified)
            || !entry.Metadata.GetProperties().Any(p => p.IsConcurrencyToken)
            || entry.Properties.Any(p => p.IsModified && Written(p.Metadata)))
        {
            return;
        }

        if (!_written.Any(part => !ReferenceEquals(part, root) && _db.Entry(part).State != EntityState.Unchanged))
        {
            return;
        }

        // A stale token alone marks the root modified, but the store generates it: EF sends no
        // UPDATE for a row whose only change is a column it never writes, and with no UPDATE
        // nothing checks the token. One column of the root's own, written back as it is, is
        // the statement that carries the check.
        var carrier = entry.Properties.FirstOrDefault(p => !p.Metadata.IsKey() && Written(p.Metadata));

        if (carrier is not null)
        {
            carrier.IsModified = true;
        }
    }

    static bool Written(IReadOnlyProperty property)
    {
        return property.GetAfterSaveBehavior() == PropertySaveBehavior.Save;
    }

    // The value the caller read the row at. The type's default is "no opinion", as
    // ExpectVersion reads 0, and a row being created has nothing to have moved.
    public override void ExpectToken(object entity, string member, object? value)
    {
        if (value is null || (value.GetType().IsValueType && value.Equals(Activator.CreateInstance(value.GetType()))))
        {
            return;
        }

        var entry = _db.Entry(entity);

        if (entry.State == EntityState.Added)
        {
            return;
        }

        entry.Property(member).OriginalValue = value;
    }
}
