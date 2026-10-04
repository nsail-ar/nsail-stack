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

/// <summary>The story end to end, through the tree it actually broke in: the real router, the
/// layout that declares the aside, and a routed page hosted inside it. The unit tests next door
/// prove the surface reads the right string; only this one proves the string reaches a page
/// mounted the way an aside mounts one — the shape where [SupplyParameterFromQuery] handed back
/// null and the page listed everybody.</summary>
public sealed class AsideQueryTests : BunitContext
{
    const string PartyId = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";
    const string OtherId = "6f9619ff-8b86-d011-b42d-00c04fc964ff";

    public AsideQueryTests()
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

        // NsPage resolves its RouteTable from DI, and the one AddRouteTable builds is the
        // entry assembly's — a test host's, which carries none of these fixtures' routes.
        Services.AddSingleton(new RouteTable(typeof(ProbeQueryPage).Assembly, []));

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
            .Add(x => x.AppAssembly, typeof(ProbeQueryPage).Assembly)
            .Add(x => x.AdditionalAssemblies, [])
            .Add(x => x.DefaultLayout, typeof(ProbeLayout)));
    }

    static string Aside(string route)
    {
        return $"/probe?aside={Uri.EscapeDataString(route)}";
    }

    [Fact]
    public void APageHostedInTheAside_ReadsItsOwnQueryValue()
    {
        var app = RenderApp(Aside($"probe/query?PartyId={PartyId}"));

        Assert.Equal(PartyId, app.Find("p.probe-party").TextContent);
    }

    [Fact]
    public void TheSamePageOnTheMainSurface_ReadsTheAddressBar()
    {
        var app = RenderApp($"/probe/query?PartyId={PartyId}");

        Assert.Equal(PartyId, app.Find("p.probe-party").TextContent);
    }

    /// <summary>The discriminator: the page underneath carries a PartyId of its own, and the
    /// hosted page must not read it. Without it a test could pass on an accessor that simply
    /// always reads the browser's query.</summary>
    [Fact]
    public void APageHostedInTheAside_IgnoresTheQueryOfThePageUnderneath()
    {
        var app = RenderApp($"/probe?PartyId={PartyId}&aside={Uri.EscapeDataString("probe/query")}");

        Assert.Equal("none", app.Find("p.probe-party").TextContent);
    }

    [Fact]
    public void APageHostedInTheAside_ReadsARepeatedKeyAsAnArray()
    {
        var app = RenderApp(Aside($"probe/query?RoleIds={PartyId}&RoleIds={OtherId}"));

        Assert.Equal($"{PartyId},{OtherId}", app.Find("p.probe-roles").TextContent);
    }
}
