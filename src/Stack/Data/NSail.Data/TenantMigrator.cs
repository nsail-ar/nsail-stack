// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;

namespace NSail.Data;

/// <summary>First touch migrates: a tenant database carries no schema until the first request
/// that reaches it, and that request pays for the chain. Abstract so nothing outside the seam
/// has to name the app's <c>DbContext</c> to ask for it.</summary>
public abstract class TenantMigrator
{
    /// <summary>Brings this scope's own tenant database up to the chain, once per database and
    /// under a lock that crosses processes. The tenant is the scope's rather than a parameter
    /// because the context this migrates is the scope's: a database named from outside would
    /// migrate one thing and serve another. A scope with no tenant resolved does nothing.</summary>
    public abstract Task MigrateCurrent(CancellationToken cancellationToken = default);
}

sealed class TenantMigrator<TDbContext> : TenantMigrator
    where TDbContext : DbContext
{
    readonly TenancyProvider _tenancy;
    readonly TenantDatabases _databases;
    readonly IDbContextFactory<TDbContext> _contexts;
    readonly IEnumerable<ITenantCache> _caches;

    public TenantMigrator(
        TenancyProvider tenancy,
        TenantDatabases databases,
        IDbContextFactory<TDbContext> contexts,
        IEnumerable<ITenantCache> caches)
    {
        ArgumentNullException.ThrowIfNull(tenancy);
        ArgumentNullException.ThrowIfNull(databases);
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(caches);

        _tenancy = tenancy;
        _databases = databases;
        _contexts = contexts;
        _caches = caches;
    }

    public override Task MigrateCurrent(CancellationToken cancellationToken = default)
    {
        if (_tenancy.Current is not { IsResolved: true, Database: { } database } tenant)
        {
            return Task.CompletedTask;
        }

        return _databases.FirstTouch(database, token => Apply(tenant, token), cancellationToken);
    }

    async Task Apply(Tenant tenant, CancellationToken cancellationToken)
    {
        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);

        await context.Database.MigrateAsync(cancellationToken);

        // A seed migration's rows land beside every cache, which a job may have filled from this
        // database before any request reached it.
        foreach (var cache in _caches)
        {
            cache.Forget(tenant);
        }
    }
}
