// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using Npgsql;

namespace NSail.Data;

/// <summary>Postgres is the registry. A tenant's database name derives as
/// <c>{product}_{cell}_{tenant}</c> — product and cell from the cell's own configuration,
/// tenant from whoever resolved it — and its row in <c>pg_database</c> IS the truth that the
/// tenant exists, as of at most <c>DatabaseHitSeconds</c> ago: the lookup is memoised, so a
/// database dropped or moved inside that window still resolves and the request fails on connect.
/// No catalog database, no third runtime dependency in a request's path. Nothing here creates anything; provisioning writes the registry, the request path only
/// reads it, and a slug with no database is simply not a tenant.</summary>
public class TenantDatabases
{
    // Postgres truncates an identifier past this silently, so a name that would be cut names
    // some other tenant's database. Refused instead.
    const int MaxIdentifierLength = 63;

    // Read from the database every cluster is initialized with, not from the cell's own: under
    // a per-tenant connection the cell's string names a database that no longer has to exist,
    // and the registry has to be readable before any tenant is known.
    const string MaintenanceDatabase = "postgres";

    readonly FirstTouch _first = new();

    // A name is only memoised after Resolve derived it within the identifier limit, and expired
    // entries are swept as new ones arrive, so what is held is what arrived since the last
    // sweep. Concurrent first lookups of one name each read the registry, as in TenantStateMemo.
    readonly ConcurrentDictionary<string, long> _known = new();
    readonly TimeProvider _clock;
    readonly long _hitTicks;
    readonly long _missTicks;
    long _swept;

    readonly string _installConnectionString;
    readonly string _product;
    readonly string _cell;

    public TenantDatabases(TenancyOptions tenancy, string installConnectionString, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(tenancy);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentException.ThrowIfNullOrWhiteSpace(installConnectionString);

        _installConnectionString = installConnectionString;
        _clock = clock;
        _hitTicks = tenancy.DatabaseHitSeconds * 1000L;
        _missTicks = tenancy.DatabaseMissSeconds * 1000L;
        _product = Named(tenancy.Product, nameof(tenancy.Product));
        _cell = Named(tenancy.Cell, nameof(tenancy.Cell));
    }

    public string? NameFor(string? slug)
    {
        if (TenantSlug.Normalize(slug) is not { } tenant)
        {
            return null;
        }

        var database = $"{_product}_{_cell}_{tenant}";

        return database.Length > MaxIdentifierLength ? null : database;
    }

    /// <summary>The cluster's maintenance database: the one place install-wide work has to
    /// coordinate on when every other connection is a tenant's.</summary>
    public string MaintenanceConnection => On(MaintenanceDatabase);

    /// <summary>The prefix every database of this cell carries, and so what keeps a cluster-wide
    /// key from reaching a neighbouring cell or product.</summary>
    public string CellPrefix => $"{_product}_{_cell}_";

    public string ConnectionFor(string database)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(database);

        return On(database);
    }

    /// <summary>The registry read whole: every tenant this cell holds a database for. What a
    /// request never needs — it arrives with a slug — and what work outside one has no other
    /// way to know.</summary>
    public async Task<IReadOnlyList<Tenant>> All(CancellationToken cancellationToken = default)
    {
        var prefix = CellPrefix;

        await using var registry = new NpgsqlConnection(On(MaintenanceDatabase));

        await registry.OpenAsync(cancellationToken);

        // starts_with rather than LIKE: the composed name is full of underscores and every one
        // of them is a single-character wildcard to LIKE, so the pattern would also match a
        // neighbouring cell whose name differs in exactly those positions.
        await using var query = new NpgsqlCommand(
            "SELECT datname FROM pg_database WHERE starts_with(datname, @prefix) ORDER BY datname",
            registry);

        query.Parameters.AddWithValue("prefix", prefix);

        var tenants = new List<Tenant>();

        await using var reader = await query.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var database = reader.GetString(0);

            // A name whose tail is not a slug is some other database that shares the prefix,
            // not a tenant: the same refusal the header gets, applied to the registry.
            if (TenantSlug.Normalize(database[prefix.Length..]) is { } slug)
            {
                tenants.Add(Tenant.For(slug, database));
            }
        }

        return tenants;
    }

    public async Task<Tenant?> Resolve(string? slug, CancellationToken cancellationToken = default)
    {
        var tenant = TenantSlug.Normalize(slug);

        if (tenant is null || NameFor(tenant) is not { } database)
        {
            return null;
        }

        var exists = await Remembered(database, cancellationToken);

        return exists ? Tenant.For(tenant, database) : null;
    }

    // The registry is read once per window per name: a flood of one unknown slug costs one round
    // trip, and a database provisioned meanwhile is seen once the shorter miss window lapses.
    async Task<bool> Remembered(string database, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().ToUnixTimeMilliseconds();

        if (_known.TryGetValue(database, out var until) && now < Math.Abs(until))
        {
            return until > 0;
        }

        var exists = await Exists(database, cancellationToken);

        now = _clock.GetUtcNow().ToUnixTimeMilliseconds();

        // Swept every few miss windows: often enough that a slug flood does not pile up, rarely
        // enough that the walk is not paid per request.
        if (now - Interlocked.Read(ref _swept) > _missTicks * 4)
        {
            Interlocked.Exchange(ref _swept, now);

            foreach (var entry in _known)
            {
                if (now >= Math.Abs(entry.Value))
                {
                    _known.TryRemove(entry);
                }
            }
        }

        var ttl = (exists ? _hitTicks : _missTicks) + now;

        _known[database] = exists ? ttl : -ttl;

        return exists;
    }

    /// <summary>Runs <paramref name="migrate"/> at most once per tenant database, and never
    /// twice at the same moment anywhere — the lock is taken on the tenant's own database, so
    /// it crosses processes and machines with no infrastructure behind it. Two first touches
    /// produce one migration and one wait.</summary>
    public Task FirstTouch(string database, Func<CancellationToken, Task> migrate, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(database);

        return _first.Once(On(database), database, migrate, cancellationToken);
    }

    async Task<bool> Exists(string database, CancellationToken cancellationToken)
    {
        await using var registry = new NpgsqlConnection(On(MaintenanceDatabase));

        await registry.OpenAsync(cancellationToken);

        await using var query = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", registry);

        query.Parameters.AddWithValue("name", database);

        return await query.ExecuteScalarAsync(cancellationToken) is not null;
    }

    string On(string database)
    {
        return ConnectionStrings.Complete(
            new NpgsqlConnectionStringBuilder(_installConnectionString) { Database = database }.ConnectionString);
    }

    static string Named(string? configured, string setting)
    {
        return TenantSlug.Normalize(configured)
            ?? throw new InvalidOperationException(
                $"Tenancy:{setting} is '{configured}', which cannot name a database. A per-tenant connection derives {{product}}_{{cell}}_{{tenant}}, so the cell states both in lowercase alphanumerics and hyphens.");
    }
}
