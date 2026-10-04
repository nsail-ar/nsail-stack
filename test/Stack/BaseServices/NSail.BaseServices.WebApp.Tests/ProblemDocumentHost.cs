// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSail.Components;
using NSail.Localization;
using NSail.Messaging.WebApi;
using NSail.Problems;

namespace NSail.BaseServices.WebApp.Tests;

/// <summary>A real host behind the real pipeline: <c>UseErrorHandler</c> outermost and a
/// middleware under it that dies, which is the story's own reproduction. Two shapes, because
/// the seam's whole point is that they answer differently — one that composes a Blazor app
/// (<c>AddBaseWebApp</c>) and one that composes none.</summary>
public sealed class ProblemDocumentHost : IAsyncDisposable
{
    public const string Dead = "/gone";

    /// <summary>The same death one render later: a request that got far enough to initialize
    /// the NavigationManager its page injects, which is what every prerender does before it
    /// draws anything.</summary>
    public const string DeadMidRender = "/gone-mid-render";

    // Thrown by the middleware under the error handler, so its words are what must NOT reach
    // the reader: the page carries the face's own message and a handle, nothing else.
    public const string Secret = "row 41ff0b6c of tenant lumina";

    readonly WebApplication _app;

    ProblemDocumentHost(WebApplication app)
    {
        _app = app;
    }

    public HttpClient Client { get; private set; } = null!;

    public static async Task<ProblemDocumentHost> Start(bool composesApp, bool documentFails = false)
    {
        // Production, and not Development as the tenancy suites run: Development turns on the
        // container's build-time validation, and AddBaseWebApp's message policies reach a
        // SecurityManager only an identity stack registers — a dependency this suite has no
        // business composing to ask a dead request what it answers with.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production,
        });

        builder.WebHost.UseTestServer();

        builder.Services.AddErrorHandler();

        if (composesApp)
        {
            builder.AddBaseWebApp();
            builder.Services.AddRouteTable();
            builder.Services.AddRoutesFromAssembly(typeof(ProblemDocumentHost).Assembly);
            builder.Services.AddStringsFromAssembly(typeof(NsPageError).Assembly);
        }

        // Last registration wins the single resolve: the real provider stays composed, so what
        // this swaps out is the render and nothing around it.
        if (documentFails)
        {
            builder.Services.AddScoped<IProblemDocumentProvider, UnrenderableDocumentProvider>();
        }

        var app = builder.Build();

        // UseErrorHandler and nothing else of UseBaseWebApp: the document arm hangs off the
        // registration, not off the pipeline, and MapStaticAssets wants a manifest only a real
        // web host builds. What the reader gets is rendered either way.
        app.UseErrorHandler();

        app.Use(async (context, next) =>
        {
            if (context.Request.Path == Dead)
            {
                throw new KeyNotFoundException(Secret);
            }

            if (context.Request.Path == DeadMidRender)
            {
                // What a page's prerender does before anything of its own renders:
                // EndpointHtmlRenderer initializes the request's NavigationManager, and the one
                // AddInteractiveServerComponents registers refuses a second initialization.
                // Done from a middleware rather than by mapping a razor page that throws,
                // because MapRazorComponents wants a static-asset manifest no test host builds —
                // and the state this leaves the request in is the state the real prerender
                // leaves it in, which is the whole of what the error path has to survive.
                var navigation = (IHostEnvironmentNavigationManager)context.RequestServices
                    .GetRequiredService<NavigationManager>();

                navigation.Initialize("http://localhost/", $"http://localhost{DeadMidRender}");

                throw new KeyNotFoundException(Secret);
            }

            await next(context);
        });

        await app.StartAsync();

        var host = new ProblemDocumentHost(app)
        {
            Client = app.GetTestClient(),
        };

        return host;
    }

    /// <summary>A request for the dead path, asking with <paramref name="accept"/> verbatim —
    /// null sends no Accept header at all, which is what curl and the generated client do.</summary>
    public Task<HttpResponseMessage> Get(string? accept)
    {
        return Get(accept, Dead);
    }

    public Task<HttpResponseMessage> Get(string? accept, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (accept is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept", accept);
        }

        return Client.SendAsync(request);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();

        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

[Route("/")]
public sealed class HomeTestPage : ComponentBase
{
}

/// <summary>A document provider that cannot draw, standing in for whatever a second render on
/// a dead request may fail at. Its words are the ones that must NOT become the request's
/// answer.</summary>
public sealed class UnrenderableDocumentProvider : IProblemDocumentProvider
{
    public const string Masking = "the renderer itself fell over";

    public Task Write(HttpContext context, Problem problem)
    {
        throw new InvalidOperationException(Masking);
    }
}
