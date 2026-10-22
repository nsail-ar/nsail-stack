// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#2338, through the tree the arrival focus actually runs in: the real router, the
/// layout that declares the aside, and a routed page hosted in each. The unit tests next door
/// pin what NsArrivalFocus decides; only this one pins the PREMISE under them — Blazor's router
/// re-renders the whole Found branch, NsArrivalFocus included, on a location change that never
/// left the page, which is how an aside opening over a list reached a call meant for an arrival
/// (Vigía, nsail#2338 round 1: on a coarse pointer that call took the cursor to the heading and
/// closed the keyboard over the box that had just written the query).</summary>
public sealed class ArrivalInPlaceTests : BunitContext
{
    const string Signal = "nsapp.arrivalFocus";

    public ArrivalInPlaceTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddMessaging();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();

        // NsPage resolves its RouteTable from DI, and the one AddRouteTable builds is the
        // entry assembly's — a test host's, which carries none of these fixtures' routes.
        Services.AddSingleton(new RouteTable(typeof(ProbePage).Assembly, []));

        Services.AddSingleton<IRouteGate, FakeRouteGate>();

        AddAuthorization().SetAuthorized("probe");
    }

    IReadOnlyList<JSRuntimeInvocation> Calls => [.. JSInterop.Invocations[Signal]];

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    IRenderedComponent<NsRouter> RenderApp(string uri)
    {
        Navigation.NavigateTo(uri);

        return Render<NsRouter>(p => p
            .Add(x => x.AppAssembly, typeof(ProbePage).Assembly)
            .Add(x => x.AdditionalAssemblies, [])
            .Add(x => x.DefaultLayout, typeof(ProbeLayout)));
    }

    [Fact]
    public void OpeningASurfaceOverThePage_IsNoSecondArrival()
    {
        var app = RenderApp("/probe");

        Assert.Single(Calls);

        // The address a surface open writes (SurfaceContext.Follow, in place): same path, the
        // aside's own route in the query.
        Navigation.NavigateTo("/probe?aside=probe%2Fnew");

        // The control for the whole test: the page inside the aside is on screen, so the router
        // DID hand a fresh RouteData down the branch — the second call is missing because the
        // arrival is read off the path, not because nothing re-rendered.
        Assert.NotEmpty(app.FindAll("a.probe-inside"));
        Assert.Single(Calls);

        Navigation.NavigateTo("/probe");

        Assert.Empty(app.FindAll("a.probe-inside"));
        Assert.Single(Calls);
    }

    [Fact]
    public void MovingToASiblingPage_IsAnArrival()
    {
        var app = RenderApp("/probe");

        Navigation.NavigateTo("/probe/other");

        Assert.Equal("other", app.Find("p").TextContent);
        Assert.Equal(2, Calls.Count);
    }
}
