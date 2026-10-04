// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSail.Components;
using NSail.Configuration;
using NSail.Localization;
using NSail.Messaging.WebApi;

namespace NSail.BaseServices.WebApp;

public static class Setup
{
    public static void AddBaseWebApp(this WebApplicationBuilder builder)
    {
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        builder.Services.AddScoped<CircuitHandler, TenancyCircuit>();
        builder.Services.AddMessagePolicies();

        // The server is the only side that can see the request, so it is the side that answers
        // the device question and hands the answer down with the page.
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IDeviceProvider, RequestDeviceProvider>();

        // What turns a dead request's Problem into a page, for the callers that asked for one.
        // Registered here and nowhere else: composing an app is exactly the condition under
        // which there is a document to answer with, so a host without one resolves nothing and
        // keeps the machine's answer (ErrorMiddleware).
        builder.Services.AddScoped<IProblemDocumentProvider, ProblemDocumentProvider>();
    }

    public static void UseBaseWebApp(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }

        app.UseRequestLanguage();
        app.UseAntiforgery();

        // Deferred to OnStarting so it is the last word on Cache-Control regardless of what a
        // downstream middleware or the page's own render set — see DocumentCaching.
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                DocumentCaching.Apply(context);

                return Task.CompletedTask;
            });

            await next(context);
        });

        app.MapStaticAssets();
    }

    public static void MapRazorApp<TApp>(this WebApplication app) where TApp : IComponent
    {
        var builder = app.MapRazorComponents<TApp>()
               .AddInteractiveServerRenderMode()
               .AddInteractiveWebAssemblyRenderMode();

        var assemblies = app.Services.GetServices<IRouteContributor>()
                            .Select(r => r.Assembly)
                            .ToArray();

        if (assemblies.Length > 0)
        {
            builder.AddAdditionalAssemblies(assemblies);
        }

        // MapRazorComponents maps only the templates each @page declares -- an address that
        // matches none of them never reaches Blazor at all, so NsRouter never gets to render
        // its own NotFound: ASP.NET's routing 404s before a single component runs. This
        // fallback is what hands such a request to Blazor so the Router decides. Two things
        // stay OUTSIDE it, on purpose (an asset or a message endpoint that genuinely doesn't
        // exist keeps its own bare 404, unrelated to whether a PAGE exists): "{*path:nonfile}"
        // -- MapFallback's own default pattern -- already excludes anything whose last segment
        // looks like a filename, and the explicit check below excludes "api", where every
        // hand-mapped and generated endpoint in this codebase lives.
        app.MapFallback((Func<HttpContext, IResult>)(context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                return Results.NotFound();
            }

            return new RazorComponentResult<TApp> { StatusCode = StatusCodes.Status404NotFound };
        }));
    }

    // Where the language of a page is decided, once, before anything paints: the choice somebody
    // forced, else the browser's own among what this install offers, else the install's default.
    // Its own step rather than a line inside UseBaseWebApp so a host can raise this one piece —
    // MapStaticAssets below wants a manifest no test host produces.
    internal static void UseRequestLanguage(this WebApplication app)
    {
        app.UseRequestLocalization(BuildLocalization(app));
    }

    static RequestLocalizationOptions BuildLocalization(WebApplication app)
    {
        var language = app.Configuration.Load<LanguageOptions>();

        // The one derivation, shared with whatever answers a client's language question, so the
        // set this resolves a chosen culture against and the set the rest of the app knows can
        // never be two different sets. Every language the catalogs render, so a forced choice may
        // name any of them -- what the browser gets to vote among is the narrower Offered.
        string[] supported = [.. Languages.Supported(app.Services.GetServices<IStringSource>(), language)];

        var options = new RequestLocalizationOptions();

        options.SetDefaultCulture(language.Default);
        options.AddSupportedCultures(supported);
        options.AddSupportedUICultures(supported);

        // Two voters, in this order, in place of ASP.NET's query-cookie-header chain. The cookie
        // is the carrier a forced choice rides in on -- the server answers the first byte before
        // any code of ours runs -- and it wins outright. The browser speaks next, among the
        // languages this install offers rather than every language it can render. A query string
        // stays out: it would move this prerender without moving the client that replaces it,
        // which is two languages on one page.
        options.RequestCultureProviders =
        [
            new CookieRequestCultureProvider(),
            new OfferedLanguageProvider(Languages.Offered(language)),
        ];

        return options;
    }
}
