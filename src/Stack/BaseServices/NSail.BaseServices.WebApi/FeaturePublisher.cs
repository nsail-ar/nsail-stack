// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSail.Data;
using NSail.Security;

namespace NSail.BaseServices.WebApi;

// What this build can be sold, published to the install's own table (DeclaredFeature) for whoever
// operates the fleet: the box that writes a tenant's entitlements bag then offers an operator the
// keys the version ON THAT INSTALL knows, instead of the ones the box's own build happens to
// carry — which is a different list the moment an install runs a version behind.
//
// Asked once at startup and never again, which is the whole of when it can change: the catalog is
// gathered from the modules composed into the process, so what this build declares is a
// compile-time fact and a deploy is the only event that moves it.
//
// Here rather than in NSail.Data because this is the one composition that reaches both the
// catalog and the context (NSail.Data references no NSail.Security); the table is NSail.Data's,
// beside TenantStates.
sealed class FeaturePublisher : IHostedService
{
    readonly IServiceScopeFactory _scopes;

    public FeaturePublisher(IServiceScopeFactory scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        _scopes = scopes;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();

        // Two compositions have nowhere to publish to, and neither is a failure. A host with no
        // store composes no context at all. And a mode that connects PER TENANT has no database
        // of its own: there the table rides each tenant's database, where this process holds no
        // connection until a request names one. What a reader is owed in both cases is the same
        // answer it is owed for an install running a version older than the table — nothing
        // reported — so nothing is written and nothing is claimed.
        if (scope.ServiceProvider.GetService<TenancyOptions>() is not { } tenancy
            || tenancy.Connection == TenancyScope.Tenant
            || scope.ServiceProvider.GetService<DbContext>() is not { } db)
        {
            return;
        }

        var declared = scope.ServiceProvider.GetRequiredService<Features>().All;

        var held = await db.Set<DeclaredFeature>()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Amended rather than emptied and refilled: the table is read by a program outside this
        // process, and a delete-then-insert would show it empty to whoever read between the two.
        foreach (var row in held)
        {
            if (!declared.Any(item => Same(item.Key, row.Key)))
            {
                db.Remove(row);
            }
        }

        foreach (var item in declared)
        {
            if (held.Find(row => Same(row.Key, item.Key)) is { } published)
            {
                published.Kind = item.Kind.ToString();
                published.Enabled = item.Enabled;
                published.Limit = item.Limit;

                continue;
            }

            db.Add(new DeclaredFeature
            {
                Key = item.Key,
                Kind = item.Kind.ToString(),
                Enabled = item.Enabled,
                Limit = item.Limit,
            });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    // The catalog's own rule for two spellings of one key, which the column's collation answers
    // the same way: asked here because the rows in hand are a list and not that dictionary.
    static bool Same(string one, string other)
    {
        return string.Equals(one, other, StringComparison.OrdinalIgnoreCase);
    }
}
