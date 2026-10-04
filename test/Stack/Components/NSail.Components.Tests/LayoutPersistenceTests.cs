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

/// <summary>testing.md's action item #2 (calidad-2026-08.md cap.1): the regression net for
/// 1bb12a22, where NsRouteGate drew nothing while a gate's answer was in flight and tore down
/// MainLayout -- title, drawer and all -- on every single navigation. The cure lived in
/// OnboardingGate.Send answering from cache rather than in NsRouteGate itself, so there is no
/// red-first teardown to reproduce here (the gate's own contract -- IRouteGate's doc comment --
/// has read "answer from cache" since before this pin existed). What this pins instead is the
/// CURRENT good behaviour the cure depends on: with a gate registered that answers from an
/// already-completed Task (FakeRouteGate, the shape a settled OnboardingGate takes), navigating
/// between two MAIN-surface pages keeps the SAME layout instance -- calque of
/// SubscribeRerenderTests' Main+Aside tree (NsRouter + ProbeLayout), swapped for two plain
/// siblings (ProbePage, ProbeOtherPage) since this pin cares about the main route changing
/// underneath the layout, not about the aside.</summary>
public sealed class LayoutPersistenceTests : BunitContext
{
    public LayoutPersistenceTests()
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
        // entry assembly's -- a test host's, which carries none of these fixtures' routes.
        Services.AddSingleton(new RouteTable(typeof(ProbePage).Assembly, []));

        Services.AddSingleton<IRouteGate, FakeRouteGate>();

        AddAuthorization().SetAuthorized("probe");
    }

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    [Fact]
    public void NavigatingBetweenTwoMainPages_KeepsTheSameLayoutInstance()
    {
        Navigation.NavigateTo("/probe");

        var app = Render<NsRouter>(p => p
            .Add(x => x.AppAssembly, typeof(ProbePage).Assembly)
            .Add(x => x.AdditionalAssemblies, [])
            .Add(x => x.DefaultLayout, typeof(ProbeLayout)));

        var before = app.FindComponent<ProbeLayout>().Instance;

        // Sibling ("probe/other"), not an aside: the main route itself changes, exactly the
        // navigation 1bb12a22's tenant felt as a full-screen flicker.
        Navigation.NavigateTo("/probe/other");

        Assert.Equal("other", app.Find("p").TextContent);

        var after = app.FindComponent<ProbeLayout>().Instance;

        Assert.Same(before, after);
    }
}
