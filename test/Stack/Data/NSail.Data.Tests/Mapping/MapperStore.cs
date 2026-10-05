// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSail.SourceGeneration.Annotations;

namespace NSail.Data.Tests.Mapping;

/// <summary>The holder a kit's Data project declares, over this assembly's test entities.</summary>
public static partial class Mappings
{
    [Generated(Mappers.Entities)]
    public static partial void AddDataTestMappings(this IServiceCollection services);
}

/// <summary>Detached's TestDbContext.Create: a context over its own SQLite database in memory,
/// created from the model, with the mapper a handler would get.</summary>
public sealed class MapperStore<TContext> : IAsyncDisposable
    where TContext : DbContext
{
    static readonly ServiceProvider Mappers = BuildMappers();

    readonly SqliteConnection _connection;

    MapperStore(SqliteConnection connection, TContext db)
    {
        _connection = connection;
        Db = db;
        Mapper = new EntityMapper(db, Mappers);
    }

    public TContext Db { get; }

    public EntityMapper Mapper { get; }

    public static async Task<MapperStore<TContext>> Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");

        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<TContext>().UseSqlite(connection).Options;
        var db = (TContext)Activator.CreateInstance(typeof(TContext), options)!;

        await db.Database.EnsureCreatedAsync();

        return new MapperStore<TContext>(connection, db);
    }

    /// <summary>A second context over the same database: what was saved, read cold.</summary>
    public TContext Fresh()
    {
        var options = new DbContextOptionsBuilder<TContext>().UseSqlite(_connection).Options;

        return (TContext)Activator.CreateInstance(typeof(TContext), options)!;
    }

    /// <summary>The mapper over another context of the same database: a second request.</summary>
    public static EntityMapper MapperOver(TContext db)
    {
        return new EntityMapper(db, Mappers);
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    static ServiceProvider BuildMappers()
    {
        var services = new ServiceCollection();

        services.AddDataTestMappings();

        return services.BuildServiceProvider();
    }
}
