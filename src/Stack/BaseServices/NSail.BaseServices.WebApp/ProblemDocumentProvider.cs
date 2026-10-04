// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components;
using NSail.Messaging.WebApi;
using NSail.Problems;

namespace NSail.BaseServices.WebApp;

/// <summary>Renders the error page for a request that died before anything else could draw
/// one. Registered by AddBaseWebApp, so only a host that composes a Blazor app answers a
/// document at all.</summary>
public sealed class ProblemDocumentProvider : IProblemDocumentProvider
{
    public async Task Write(HttpContext context, Problem problem)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(problem);

        // Executed against the HttpContext rather than through a bare HtmlRenderer: that one
        // leaves NavigationManager uninitialized, and the page injects one. The result carries
        // no StatusCode of its own, so the status the error handler already wrote stands.
        var document = new RazorComponentResult<NsProblemDocument>(new Dictionary<string, object?>
        {
            [nameof(NsProblemDocument.HomePage)] = HomePage(context),
            [nameof(NsProblemDocument.Handle)] = Handle(context),
        });

        // On a scope of its own, swapped onto the request for the length of the write. A
        // NavigationManager is one per scope and refuses a second Initialize, and the render
        // that just died is what initialized the request's: drawing the face on that scope threw
        // "'RemoteNavigationManager' already initialized" out of the error handler and masked the
        // exception this page exists to report (nsail#1837). A request that died BEFORE any
        // render never hit it, which is why every test of this page passed while a page's own
        // prerender answered a bare 500.
        var request = context.RequestServices;

        await using var scope = request.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();

        context.RequestServices = scope.ServiceProvider;

        try
        {
            await document.ExecuteAsync(context);
        }
        finally
        {
            // Put back before the handler writes anything else against this context, and before
            // the request's own scope is disposed by the host that owns it.
            context.RequestServices = request;
        }
    }

    // Nothing of the Problem crosses into the page: the reader gets the face's own words and a
    // reference, which is everything the machine's answer carried that a person can act on.
    static string Handle(HttpContext context)
    {
        return Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    }

    // The app's root page, asked of the route table like every other address in this codebase,
    // rather than a written "/" — and null where an install routes nothing there, which draws
    // the face with its remaining door instead of one that leads nowhere.
    static Type? HomePage(HttpContext context)
    {
        return context.RequestServices.GetService<RouteTable>()?.Match("")?.PageType;
    }
}
