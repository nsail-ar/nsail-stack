// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Data;

namespace NSail.BaseServices.WebApi;

/// <summary>Who the tenant is, decided before anything else in the pipeline runs. The proxy
/// is the whitelist and this header is its word — the app never parses the Host, so a
/// wildcard demo label and a custom domain arrive indistinguishable, two sources of one
/// header. Under a per-tenant connection an unknown slug has no database, and no database is a
/// 404 answered here, ahead of authentication, so nothing about an install that does not exist
/// is reachable. Where the wall is the tenant column instead there is no registry to ask — the
/// proxy's whitelist is the roster (data-tenancy.md) — so a well-formed slug IS the tenant and its key
/// derives from it; a malformed one is still refused rather than repaired. Installed by every
/// mode that resolves a tenant: under <c>None</c> no header is read at all.</summary>
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
            : Resolve(slug);

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
        // has none of is rows. Onboarding a tenant here is the proxy's block plus this.
        if (services.GetService<TenantBootstrap>() is { } bootstrap)
        {
            await bootstrap.TouchCurrent(context.RequestAborted);
        }

        await next(context);
    }

    static Tenant? Resolve(string slug)
    {
        return TenantSlug.Normalize(slug) is { } normalized ? Tenant.For(normalized) : null;
    }
}
