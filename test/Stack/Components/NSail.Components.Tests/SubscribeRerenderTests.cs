// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>stories.md 2026-08-07: "la ficha del cliente no refleja el cambio de dirección" and
/// its sibling ("no se me refresca la tabla de OTs"). Both circuits close on paper — publisher
/// and subscriber both exist — and both die live. This is the mechanism reduced to its shape:
/// a page on the MAIN surface Subscribes in OnCreated and its handler only mutates a field
/// (exactly what ClientPage.Load/PartyContactCard.Load/WorkOrdersPage's lambda do); a page
/// opened in the ASIDE Publishes after a submit (exactly what UpdatePartyPage/
/// CreateWorkOrderPage do). The real Mediator (AddMessaging, no fake) proves whether the
/// message crosses the surface boundary; the rendered DOM proves whether the subscriber's own
/// component ever redraws once it does.</summary>
public sealed class SubscribeRerenderTests : BunitContext
{
    public SubscribeRerenderTests()
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
        Services.AddSingleton(new RouteTable(typeof(ProbeReaderPage).Assembly, []));

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
            .Add(x => x.AppAssembly, typeof(ProbeReaderPage).Assembly)
            .Add(x => x.AdditionalAssemblies, [])
            .Add(x => x.DefaultLayout, typeof(ProbeLayout)));
    }

    /// <summary>The whole circuit, live: open the aside from the main page (mirrors clicking the
    /// ficha's edit door), publish from inside it (mirrors the submit), and read the paragraph
    /// the main page never re-rendered a submit for before. A green bar here means the mediator
    /// and the surface cascade are both innocent — the story's grief lives somewhere else.</summary>
    [Fact]
    public async Task APublishFromTheAside_ReachesAndRedrawsTheSubscriberOnTheMainSurface()
    {
        var app = RenderApp("/probe/rerender");

        // Equivalent to clicking the ficha's own edit door (NsLink's actual click handler sits
        // on a wrapper span, not the anchor bUnit would find by class) — what matters here is
        // the surface opening without tearing down the main page underneath, exactly what
        // 1bb12a22 changed: the reader keeps its subscription alive across the navigation.
        Navigation.NavigateTo("/probe/rerender?aside=probe%2Frerender%2Fwriter");

        await app.InvokeAsync(() => app.Find("button.probe-publish").Click());

        Assert.Equal("updated", app.Find("p.probe-value").TextContent);
    }
}
