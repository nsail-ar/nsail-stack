// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;

namespace NSail.Data;

/// <summary>First touch bootstraps: where the wall is the column, a tenant is a slug the proxy
/// let through and nothing else — no database to create, no chain to apply, so onboarding is the
/// Caddy block plus this. The first request a slug ever makes plants its own rows
/// (<see cref="ITenantSeed"/>); every request after it costs nothing. Abstract so nothing
/// outside the seam has to name the app's <c>DbContext</c> to ask for it.
///
/// <para>The touch runs the seed's steps this tenant has not run yet, in order, so a step added
/// after the tenant existed reaches it — the tenant's own chain, where the install has its
/// migrations. The memo behind the lock (<see cref="FirstTouch"/>) is per process, which is the
/// right grain: a deploy restarts the process, so a step that deploy added runs on the first
/// touch after it.</para></summary>
public abstract class TenantBootstrap
{
    /// <summary>Plants this scope's own tenant, once per tenant and under a lock that crosses
    /// processes. A scope with no tenant resolved does nothing: there is nobody to plant, and
    /// the rows would be refused by the stamp anyway.</summary>
    public abstract Task TouchCurrent(CancellationToken cancellationToken = default);
}

sealed class TenantBootstrap<TDbContext> : TenantBootstrap
    where TDbContext : DbContext
{
    readonly TenancyProvider _tenancy;
    readonly IDbContextFactory<TDbContext> _contexts;
    readonly IEnumerable<ITenantSeed> _seeds;
    readonly FirstTouch _first;
    readonly IEnumerable<ITenantCache> _caches;

    public TenantBootstrap(
        TenancyProvider tenancy,
        IDbContextFactory<TDbContext> contexts,
        IEnumerable<ITenantSeed> seeds,
        FirstTouch first,
        IEnumerable<ITenantCache> caches)
    {
        ArgumentNullException.ThrowIfNull(tenancy);
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(seeds);
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(caches);

        _tenancy = tenancy;
        _contexts = contexts;
        _seeds = seeds;
        _first = first;
        _caches = caches;
    }

    public override async Task TouchCurrent(CancellationToken cancellationToken = default)
    {
        if (_tenancy.Current is not { IsResolved: true, Slug: { } slug } tenant || !_seeds.Any())
        {
            return;
        }

        // A context of its own rather than the scope's: the request's context is enlisted in the
        // ambient unit of work, and a bootstrap that joined it would be committed — or rolled
        // back — by whatever the request went on to do.
        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);

        await _first.Once(
            context.Database.GetConnectionString()!,
            $"bootstrap:{slug}",
            token => Sow(context, tenant, slug, token),
            cancellationToken);
    }

    async Task Sow(TDbContext context, Tenant tenant, string slug, CancellationToken cancellationToken)
    {
        var applied = await context.Set<AppliedSeedStep>()
            .Select(row => row.Step)
            .ToListAsync(cancellationToken);

        var steps = _seeds
            .SelectMany(seed => seed.Steps)
            .Where(step => !applied.Contains(step.Name, StringComparer.Ordinal));

        foreach (var step in steps)
        {
            await Apply(context, tenant, slug, step, cancellationToken);
        }

        // Whatever the steps wrote, they wrote beside every cache: a process can read a tenant
        // before its first touch here — a background job enters a tenant and touches nothing.
        foreach (var cache in _caches)
        {
            cache.Forget(tenant);
        }
    }

    // The record and the rows are one transaction, so a step that throws is not recorded and
    // runs again on the next touch — and a step that ran is never run twice, which is what keeps
    // a row the shop has since edited or deleted from coming back. The step is left to throw:
    // the ones after it depend on it having run, exactly as a migration chain's do.
    static async Task Apply(
        TDbContext context,
        Tenant tenant,
        string slug,
        TenantSeedStep step,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        await step.Sow(context, tenant, cancellationToken);

        context.Add(new AppliedSeedStep
        {
            Id = TenantKey.For(slug, $"seed-step:{step.Name}"),
            Step = step.Name,
            AppliedAt = DateTime.UtcNow,
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
