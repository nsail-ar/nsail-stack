// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Authentication;
using NSail.Security;

namespace NSail.BaseServices.WebApi;

/// <summary>The install's own cut across its sessions: a credential is honoured only while it
/// carries the generation the install still honours (<see cref="SecurityOptions.TicketEpoch"/>),
/// so raising that number signs out everyone holding an older ticket and nobody else.
/// <para>It exists because the DataProtection key ring is the SERVER's, shared by every install
/// on the box: there is no key to delete or revoke that would void one install's tickets without
/// making every secret sealed anywhere on that box unreadable for good. This cut is the
/// install's alone and unseals nothing — which is what makes a tenancy-mode flip survivable,
/// the one change that leaves every ticket decrypting perfectly and naming rows the row filter
/// now hides.</para>
/// <para>Installed by every host and under every tenancy mode, because an install with one
/// customer has sessions to void like any other. Zero — never raised — refuses nothing and
/// mints nothing, so a product that has never needed the cut is exactly as it was. From one
/// upward the comparison is equality: a ticket stamped with any other generation, the unstamped
/// one included, is one this install no longer honours, and deny by default reads the absence
/// as an absence rather than as consent.</para>
/// <para>Ahead of <see cref="CredentialMiddleware"/>, which re-mints a credential whose party
/// moved: a renewal stamps the CURRENT generation, so a refusal that ran after it would be
/// handed a ticket the cut had just made valid.</para></summary>
public sealed class TicketEpochMiddleware : IMiddleware
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
        // Resolved per request rather than injected, as the gates beside it are: this
        // middleware is registered by every host, and a constructor dependency on a service
        // only some compositions register fails the container's own startup validation.
        var services = context.RequestServices;

        var current = services.GetRequiredService<SecurityOptions>().TicketEpoch;

        // Never raised, so there is no generation to be behind and no claim to read. Asked
        // first because it is the answer on every install that has never needed the cut, and
        // asking it costs no session.
        if (current == 0)
        {
            return true;
        }

        // A host that composed no identity has no credential to place, and a request carrying
        // none names nobody a cut could reach.
        if (services.GetService<SessionProvider>()?.Session is not { IsAuthenticated: true } session)
        {
            return true;
        }

        return session.TicketEpoch == current;
    }

    static async Task Refuse(HttpContext context)
    {
        // The ticket goes out with the refusal, and it cannot go out through an exception: the
        // error handler answers by clearing the response, which would wipe the Set-Cookie with
        // it (TenantClaimMiddleware's own reason). This wall stands ahead of the sign-in too, so
        // a ticket refused and left standing is the whole of the cookie's life spent being
        // refused on the one act that would replace it.
        await context.SignOutAsync();

        // Then the install's own answer to a caller with no credential, which after the sign-out
        // is exactly what the holder is: 401 to an API, the sign-in page to a browser. The mark
        // rides the challenge because the session died rather than expired — the same thing the
        // credential wall's refusal says, and the person's own act is the same one.
        var properties = new AuthenticationProperties();

        properties.Items[SignInRoutes.StaleProperty] = "1";

        await context.ChallengeAsync(properties);
    }
}
