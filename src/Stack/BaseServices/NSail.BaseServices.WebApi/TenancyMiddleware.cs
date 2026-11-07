// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Data;

namespace NSail.BaseServices.WebApi;

/// <summary>Who the tenant is, decided before anything else in the pipeline runs. The proxy
/// is the whitelist and this header is its word — the app never parses the Host, so a
/// wildcard demo label and a custom domain arrive indistinguishable, two sources of one
/// header. Under a per-tenant connection an unknown slug has no database, and no database is a
/// 404 answered here, ahead of authentication, so nothing about an install that does not exist
/// is reachable. Where the wall is the tenant column instead there is no database to find, so a
/// slug's key derives from it and the install's declared roster (<see cref="TenantRoster"/>) is
/// what it is checked against: off the roster is the same 404, before anything is planted for it,
/// and a roster that cannot be read fails the request. A cell that declares none has only the
/// proxy's whitelist, and a well-formed slug IS its tenant. A malformed one is refused in every
/// mode rather than repaired. Installed by every mode that resolves a tenant: under
/// <c>None</c> no header is read at all.</summary>
public sealed class TenancyMiddleware : IMiddleware
{
    public const string TenantHeader = "X-NSail-Tenant";

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        // Resolved per request rather than injected, exactly as the gate beside it is: this
        // middleware is registered by every host and installed by only some, and a constructor
        // dependency on a service that a mode does not register fails the container's own
        // startup validation for hosts that never install it.
        var services = context.RequestServices;

        // ToString rather than the first value: a request carrying the header twice renders as
        // one comma-joined word, which no slug can be, so a smuggled second copy is refused
        // instead of silently outranking — or being outranked by — the proxy's own.
        var slug = context.Request.Headers[TenantHeader].ToString();

        var tenant = services.GetService<TenantDatabases>() is { } databases
            ? await databases.Resolve(slug, context.RequestAborted)
            : await Resolve(slug, services.GetService<TenantRoster>(), context.RequestAborted);

        if (tenant is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;

            return;
        }

        services.GetRequiredService<ResolvedTenancyProvider>().Enter(tenant);

        // Only a per-tenant connection has a database of its own to bring up to the chain; a
        // shared one was migrated at startup like any install's.
        if (services.GetService<TenantMigrator>() is { } migrator)
        {
            await migrator.MigrateCurrent(context.RequestAborted);
        }

        // And the shared one's own first touch: the schema is already there, what a new slug
        // has none of is rows.
        if (services.GetService<TenantBootstrap>() is { } bootstrap)
        {
            await bootstrap.TouchCurrent(context.RequestAborted);
        }

        await next(context);
    }

    static async Task<Tenant?> Resolve(string slug, TenantRoster? roster, CancellationToken cancellationToken)
    {
        if (TenantSlug.Normalize(slug) is not { } normalized)
        {
            return null;
        }

        var tenant = Tenant.For(normalized);

        // A site the proxy still routes here after its tenant moved away is the case this
        // refuses: planted, it would be a second, empty copy of a tenant that lives elsewhere.
        if (roster is { Declares: true } && !await roster.Holds(tenant, cancellationToken))
        {
            return null;
        }

        return tenant;
    }
}
