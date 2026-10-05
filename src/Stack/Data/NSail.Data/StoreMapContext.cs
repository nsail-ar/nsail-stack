// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
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

    public override async Task<TEntity?> Load<TEntity>(Expression<Func<TEntity, bool>> match, IReadOnlyList<string> includes, CancellationToken cancellationToken)
        where TEntity : class
    {
        // A row this unit of work created is not in the database yet, and all of it is in
        // memory: an import that names it twice merges onto it instead of adding it again.
        var created = _db.ChangeTracker.Entries<TEntity>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .FirstOrDefault(match.Compile());

        if (created is not null)
        {
            return created;
        }

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
    }

    public override void Deleted(object entity, object? key)
    {
        _db.Remove(entity);
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
