// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSail.Mapping;

namespace NSail.Data;

/// <summary>What a handler writes a message onto its entities with, and reads them back as rows.
/// The mappers and projections are generated per [MapFrom] and [MapTo] and registered by the
/// kit's [Generated(Mappers.Entities)] holder; this runs them over the scope's DbContext. Nothing is saved: the caller's SaveChanges — or
/// the ambient unit of work — writes what the run decided.</summary>
public sealed class EntityMapper
{
    readonly DbContext _db;
    readonly IServiceProvider _services;

    public EntityMapper(DbContext db, IServiceProvider services)
    {
        _db = db;
        _services = services;
    }

    /// <summary>The source onto the row it names, graph included. A row that does not exist is
    /// NotFound; a source naming no row is a create.</summary>
    public Task<TEntity> Map<TSource, TEntity>(TSource source, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        return Run<TSource, TEntity>(source, new MapParameters(), cancellationToken);
    }

    /// <summary>The same, creating the row when it does not exist.</summary>
    public Task<TEntity> Upsert<TSource, TEntity>(TSource source, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        return Run<TSource, TEntity>(source, new MapParameters { MissingRootBehavior = MissingRootBehavior.Create }, cancellationToken);
    }

    /// <summary>Each source upserted, in order.</summary>
    public async Task<List<TEntity>> Upsert<TSource, TEntity>(IEnumerable<TSource> sources, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(sources);

        var result = new List<TEntity>();

        foreach (var source in sources)
        {
            result.Add(await Upsert<TSource, TEntity>(source, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }

    /// <summary>Rows from a document that cannot order them: each is upserted, and a reference to
    /// a row not written yet creates it from what the document says about it.</summary>
    public async Task<List<TEntity>> Import<TSource, TEntity>(IEnumerable<TSource> sources, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(sources);

        var parameters = new MapParameters
        {
            MissingRootBehavior = MissingRootBehavior.Create,
            MissingAggregationBehavior = MissingAggregationBehavior.Create,
        };

        var result = new List<TEntity>();

        foreach (var source in sources)
        {
            result.Add(await Run<TSource, TEntity>(source, parameters, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }

    /// <summary>The query read as rows, per the entity's [MapTo]: the store selects only what the
    /// row carries.</summary>
    public IQueryable<TRow> Project<TEntity, TRow>(IQueryable<TEntity> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var projection = _services.GetService<IProjection<TEntity, TRow>>()
            ?? throw new InvalidOperationException($"No projection reads {typeof(TEntity).Name} as {typeof(TRow).Name}. Declare [MapTo(typeof({typeof(TRow).Name}))] on {typeof(TEntity).Name} and call the kit's [Generated(Mappers.Entities)] holder.");

        return query.Select(projection.Expression);
    }

    Task<TEntity> Run<TSource, TEntity>(TSource source, MapParameters parameters, CancellationToken cancellationToken)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);

        var mapper = _services.GetService<IEntityMapper<TSource, TEntity>>()
            ?? throw new InvalidOperationException($"No mapper writes {typeof(TSource).Name} onto {typeof(TEntity).Name}. Declare [MapFrom(typeof({typeof(TSource).Name}))] on {typeof(TEntity).Name} and call the kit's [Generated(Mappers.Entities)] holder.");

        return mapper.Map(source, null, new StoreMapContext(_db, parameters), cancellationToken);
    }
}
