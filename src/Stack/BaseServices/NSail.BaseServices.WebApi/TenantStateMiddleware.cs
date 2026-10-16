// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
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
/// (<see cref="TenantState"/>), written from outside the app by whoever operates the fleet — so
/// what the row says is memoed for minutes while whether its window is open is asked of the clock
/// on every request (<see cref="TenantStates.Suspension"/>). A tenant with no row is active, and
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

        if (states is null || await states.Suspension(context.RequestAborted) is not { } suspension)
        {
            await next(context);

            return;
        }

        await Refuse(context, suspension);
    }

    // Written here rather than thrown as a BusinessException for the reason the credential wall
    // writes its own: the error handler answers by clearing the response, and what this has to
    // say — a page with no asset behind it, and a status a monitor reads as "not the caller's
    // fault" — is not an error being reported, it is the install's answer.
    static async Task Refuse(HttpContext context, TenantState suspension)
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
        var work = IsWork(suspension.Reason);

        var document = SuspendedDocument.For(
            language,
            strings.Translate(work ? "Tenancy.PausedTitle" : "Tenancy.SuspendedTitle"),
            Paragraphs(suspension, work, strings, services));

        context.Response.ContentType = "text/html; charset=utf-8";

        await context.Response.WriteAsync(document, context.RequestAborted);
    }

    // The body, in the order it is read: the words the pause is explained in, when it ends, what
    // the writer wanted said, and who to reach. The reason only chooses the words — work on the
    // service says so and promises a return, and everything else (a customer who does not pay, a
    // reason this Stack does not know, no reason at all) gets the generic ones, which are the
    // only honest words for a pause whose cause the page cannot name.
    static IEnumerable<string> Paragraphs(
        TenantState suspension,
        bool work,
        StringManager strings,
        IServiceProvider services)
    {
        yield return strings.Translate(work ? "Tenancy.PausedMessage" : "Tenancy.SuspendedMessage");

        // In UTC, and saying so: this gate resolves no reader, which is the same reason it speaks
        // the install's language rather than the browser's — a zone it guessed would be worse
        // than a zone it names.
        if (suspension.SuspendedUntil is { } until && until > services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime)
        {
            yield return strings.Translate(
                "Tenancy.SuspendedUntil",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["when"] = until.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
                });
        }

        if (!string.IsNullOrWhiteSpace(suspension.Message))
        {
            yield return suspension.Message;
        }

        if (services.GetRequiredService<TenancyOptions>().SupportContact is { } contact
            && !string.IsNullOrWhiteSpace(contact))
        {
            yield return strings.Translate(
                "Tenancy.SuspendedContact",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["contact"] = contact });
        }
    }

    static bool IsWork(string? reason)
    {
        return string.Equals(reason, TenantState.Reasons.Maintenance, StringComparison.OrdinalIgnoreCase)
            || string.Equals(reason, TenantState.Reasons.Move, StringComparison.OrdinalIgnoreCase);
    }
}
