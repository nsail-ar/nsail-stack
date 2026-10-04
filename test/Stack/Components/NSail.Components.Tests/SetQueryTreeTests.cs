// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

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

/// <summary>SetQuery through the tree it has to survive: the real router, the layout that
/// declares the aside, and a routed page writing its own query from inside it. The unit tests
/// next door prove the address is composed right; only this one proves the page hosted in the
/// aside is the page that reads the write back, and that the page underneath keeps its own.</summary>
public sealed class SetQueryTreeTests : BunitContext
{
    public SetQueryTreeTests()
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
        Services.AddSingleton(new RouteTable(typeof(ProbeQueryWriterPage).Assembly, []));

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
            .Add(x => x.AppAssembly, typeof(ProbeQueryWriterPage).Assembly)
            .Add(x => x.AdditionalAssemblies, [])
            .Add(x => x.DefaultLayout, typeof(ProbeLayout)));
    }

    static string Aside(string route)
    {
        return $"/probe?aside={Uri.EscapeDataString(route)}";
    }

    [Fact]
    public async Task APageOnTheMainSurface_WritesToTheAddressBar()
    {
        var app = RenderApp("/probe/query/write");

        await app.InvokeAsync(() => app.Find("button.probe-write").Click());

        Assert.Equal("http://localhost/probe/query/write?Tab=contacts", Navigation.Uri);
        Assert.Equal("contacts", app.Find("p.probe-tab").TextContent);
    }

    [Fact]
    public async Task APageHostedInTheAside_WritesInsideItsOwnRoute()
    {
        var app = RenderApp(Aside("probe/query/write"));

        await app.InvokeAsync(() => app.Find("button.probe-write").Click());

        Assert.Equal(
            "http://localhost/probe?aside=probe%2Fquery%2Fwrite%3FTab%3Dcontacts",
            Navigation.Uri);
        Assert.Equal("contacts", app.Find("p.probe-tab").TextContent);
    }

    /// <summary>The discriminator: the page underneath is the SAME component reading the same
    /// key, so a write that landed on the browser's query instead of the aside's own route would
    /// light up both paragraphs.</summary>
    [Fact]
    public async Task APageHostedInTheAside_LeavesTheQueryOfThePageUnderneathAlone()
    {
        var app = RenderApp($"/probe/query/write?Tab=underneath&aside={Uri.EscapeDataString("probe/query/write")}");

        await app.InvokeAsync(() => app.FindAll("button.probe-write")[^1].Click());

        var tabs = app.FindAll("p.probe-tab");

        Assert.Equal("underneath", tabs[0].TextContent);
        Assert.Equal("contacts", tabs[^1].TextContent);
    }

    [Fact]
    public async Task ClearingFromTheAside_LeavesTheAsideOpenOnItsBareRoute()
    {
        var app = RenderApp(Aside("probe/query/write?Tab=contacts"));

        Assert.Equal("contacts", app.Find("p.probe-tab").TextContent);

        await app.InvokeAsync(() => app.Find("button.probe-clear").Click());

        Assert.Equal("http://localhost/probe?aside=probe%2Fquery%2Fwrite", Navigation.Uri);
        Assert.Equal("none", app.Find("p.probe-tab").TextContent);
    }
}
