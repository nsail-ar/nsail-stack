// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Configuration;
using NSail.Data;
using NSail.Localization;
using NSail.Messaging.WebApi;
using NSail.Problems;

namespace NSail.BaseServices.WebApi;

/// <summary>Whether this install is serving this tenant at all, decided right after the tenant
/// is resolved and before anything else: a suspended customer must not reach a cookie, a
/// session, a throttle counter, an endpoint or an asset, exactly as an install that does not
/// exist does not. The state is the install's own record
/// (<see cref="TenantState"/>), written from outside the app by whoever operates the fleet —
/// so the answer is read per request and never remembered. A tenant with no row is active, and
/// so is one whose state this Stack cannot read: the gate refuses what is positively suspended
/// and nothing else. Installed by every mode that resolves a tenant and by no other.</summary>
public sealed class TenantStateMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        // Resolved per request rather than injected, as the gates beside it are: this middleware
        // is registered by every host and installed by only some, and a constructor dependency
        // on a service a mode does not register fails the container's own startup validation for
        // hosts that never install it. A host that composed no store resolves nothing here and
        // has no state to read.
        var states = context.RequestServices.GetService<TenantStates>();

        if (states is null || !await states.IsSuspended(context.RequestAborted))
        {
            await next(context);

            return;
        }

        await Refuse(context);
    }

    // Written here rather than thrown as a BusinessException for the reason the credential wall
    // writes its own: the error handler answers by clearing the response, and what this has to
    // say — a page with no asset behind it, and a status a monitor reads as "not the caller's
    // fault" — is not an error being reported, it is the install's answer.
    static async Task Refuse(HttpContext context)
    {
        var problem = TenancyProblem.Suspended();

        context.Response.StatusCode = problem.Status!.Value;

        // Nothing downstream will say it: the step that owns Cache-Control for a document runs
        // later in the pipeline than this gate, and a suspension cached by a browser or a proxy
        // would outlive the suspension itself.
        context.Response.Headers.CacheControl = "no-store";

        if (!context.Request.PrefersDocument())
        {
            await context.Response.WriteAsJsonAsync(problem, cancellationToken: context.RequestAborted);

            return;
        }

        var services = context.RequestServices;

        // The install's default language, and not the reader's: this gate stands ahead of the
        // step that resolves anybody's language (UseRequestLanguage), because a suspended tenant
        // is refused before the pipeline does any work on its behalf. The install's default is
        // then the one answer that is not a guess — the same one the background runner gives for
        // the same reason.
        var language = services.GetRequiredService<IConfiguration>().Load<LanguageOptions>().Default;

        services.GetRequiredService<LanguageProvider>().Current = language;

        var strings = services.GetRequiredService<StringManager>();

        var document = SuspendedDocument.For(
            language,
            strings.Translate("Tenancy.SuspendedTitle"),
            strings.Translate("Tenancy.SuspendedMessage"));

        context.Response.ContentType = "text/html; charset=utf-8";

        await context.Response.WriteAsync(document, context.RequestAborted);
    }
}
