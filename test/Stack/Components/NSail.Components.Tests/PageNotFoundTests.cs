// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Context;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The story: an unknown address inside the app gets the splash's own face
/// (logo + a short message + a door back to Home) instead of Blazor's generic "Sorry,
/// there's nothing at this address." — wired through NsRouter's NotFound fragment, which the
/// app supplies (NsPageNotFound), never the Stack itself (a Stack component names no app
/// route or logo URL). HomeTestPage (RouteTableTests.cs, "/") stands in for HomePage — a
/// second page at "/" in this assembly would collide with it (routes are ambiguous per
/// assembly, not per test).</summary>
public sealed class PageNotFoundTests : BunitContext
{
    const string LogoUrl = "/probe/logo.svg";

    public PageNotFoundTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<MessageContextAccessor>();

        // NsPageNotFound resolves its RouteTable from DI, same seam AsideQueryTests already
        // proved for NsPage: the one AddRouteTable builds is the entry assembly's, a test
        // host's, which carries none of these fixtures' routes.
        Services.AddSingleton(new RouteTable(typeof(HomeTestPage).Assembly, []));

        AddAuthorization().SetAuthorized("probe");
    }

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    IRenderedComponent<NsRouter> RenderApp(string uri)
    {
        Navigation.NavigateTo(uri);

        return Render<NsRouter>(p => p
            .Add(x => x.AppAssembly, typeof(HomeTestPage).Assembly)
            .Add(x => x.AdditionalAssemblies, [])
            .Add(x => x.DefaultLayout, typeof(ProbeLayout))
            .Add(x => x.NotFound, NotFoundFragment));
    }

    // NsRouter forwards this fragment verbatim into Blazor's own <NotFound> -- exactly what
    // each app's Routes.razor supplies in production (NsPageNotFound, HomePage + LogoUrl).
    static RenderFragment NotFoundFragment => builder =>
    {
        builder.OpenComponent<NsPageNotFound>(0);
        builder.AddAttribute(1, nameof(NsPageNotFound.HomePage), typeof(HomeTestPage));
        builder.AddAttribute(2, nameof(NsPageNotFound.LogoUrl), LogoUrl);
        builder.CloseComponent();
    };

    [Fact]
    public void AnUnknownRoute_RendersTheLogoMessageAndHomeLink()
    {
        var app = RenderApp("/probe/this-route-does-not-exist");

        Assert.NotEmpty(app.FindAll("#ns-page-not-found"));
        Assert.NotEmpty(app.FindAll(".ns-remote-image"));
        // The catalog is empty in this fixture set (as everywhere else in this file) --
        // StringManager's own documented fallback is the key itself, so this is what proves
        // the message actually asks for Common.PageNotFound rather than some other text.
        Assert.Equal("Common.PageNotFound", app.Find("#ns-page-not-found h6").TextContent);

        var home = app.Find("a.ns-page-not-found-home");

        Assert.Equal("/", home.GetAttribute("href"));
    }

    [Fact]
    public void TheHomePage_DoesNotRenderTheNotFoundFace()
    {
        var app = RenderApp("/");

        Assert.Empty(app.FindAll("#ns-page-not-found"));
    }

    [Fact]
    public void AKnownRoute_DoesNotRenderTheNotFoundFace()
    {
        var app = RenderApp("/probe");

        Assert.Empty(app.FindAll("#ns-page-not-found"));
    }
}
