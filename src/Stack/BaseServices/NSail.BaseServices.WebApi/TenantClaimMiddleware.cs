// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Authentication;
using NSail.Data;
using NSail.Security;

namespace NSail.BaseServices.WebApi;

/// <summary>The identity half of the tenant wall: a credential is honoured only on the install
/// it was issued for. The connection wall already sends the work to the right database, so what
/// crosses without this is the actor — a ticket minted on one tenant's domain, replayed on
/// another's, arrives as somebody. Installed only under a per-tenant connection, and it runs
/// after authentication and ahead of every endpoint, so the refusal lands before a handler,
/// a page or a circuit sees the caller. The refusal takes the ticket with it: the sign-in is
/// behind this same wall, so a credential left standing would be one its holder cannot
/// replace.</summary>
public sealed class TenantClaimMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (Holds(context))
        {
            await next(context);

            return;
        }

        await Refuse(context);
    }

    static bool Holds(HttpContext context)
    {
        // Resolved per request rather than injected, as the two gates beside it are: this
        // middleware is registered by every host and installed by only some, and a constructor
        // dependency on a service a mode does not register fails the container's own startup
        // validation for hosts that never install it.
        var services = context.RequestServices;

        // A host that composed no identity at all has none to check: nothing there can carry a
        // tenant, so there is nothing that could arrive holding another one's.
        if (services.GetService<SessionProvider>()?.Session is not { IsAuthenticated: true } session)
        {
            return true;
        }

        var tenant = services.GetRequiredService<TenancyProvider>().Current;

        // Ordinal, and a missing claim is a mismatch: both words come through TenantSlug, so
        // equal ones are equal byte for byte, and a ticket that names no tenant is one this
        // install cannot place — minted before the wall existed, or somewhere that has no wall.
        // Deny-by-default answers both the same way rather than reading an absence as consent.
        return string.Equals(session.Tenant, tenant.Slug, StringComparison.Ordinal);
    }

    static async Task Refuse(HttpContext context)
    {
        // The ticket goes out with the refusal, and it cannot go out through an exception: the
        // error handler answers by clearing the response, which would wipe the Set-Cookie with
        // it. Signing out first is what makes the refusal actionable — this wall stands ahead
        // of the sign-in too, so a ticket refused and left standing is fourteen sliding days of
        // 401 on the one act that would fix it, including for the ticket that names no tenant
        // because it was minted before the wall existed and was carried nowhere.
        await context.SignOutAsync();

        // And then the install's own answer to a caller with no credential, which after the
        // sign-out is exactly what the holder is: 401 to an API, the sign-in page to a browser.
        // 401 and not 403 for the same reason — here the holder of another tenant's ticket is
        // nobody, and signing in is what fixes it, which is the difference the two refusals
        // exist to tell apart.
        await context.ChallengeAsync();
    }
}
