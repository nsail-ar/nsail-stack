// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Context;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#1221: window.nsapp.appReady says "the screen under the splash is
/// interactive", so where it is sent from is the whole of it. NsRouteGate's own render is the
/// render where the gate is still asking and nothing routed is in the tree at all, and the
/// browser's latch makes the first call the only call — a signal sent there leaves the splash
/// over a frame with no page in it. NsAppReady sends it where a page has landed, and both
/// landings count: the routed one and the address that matches nothing.</summary>
public sealed class AppReadySignalTests : BunitContext
{
    const string Signal = "window.nsapp.appReady";

    public AppReadySignalTests()
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
        Services.AddSingleton(new RouteTable(typeof(HomeTestPage).Assembly, []));

        AddAuthorization().SetAuthorized("probe");
    }

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    IReadOnlyList<JSRuntimeInvocation> Announcements
    {
        get { return [.. JSInterop.Invocations[Signal]]; }
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

    // What each app's Routes.razor supplies, same shape PageNotFoundTests renders it in.
    static RenderFragment NotFoundFragment => builder =>
    {
        builder.OpenComponent<NsPageNotFound>(0);
        builder.AddAttribute(1, nameof(NsPageNotFound.HomePage), typeof(HomeTestPage));
        builder.CloseComponent();
    };

    /// <summary>The anchor: a page that routed announces, and announces once — the latch on
    /// the browser side would drop the rest anyway, and a signal sent per render is noise in
    /// every transcript that reads the interop log.</summary>
    [Fact]
    public void ARoutedPage_AnnouncesTheAppOnce()
    {
        var app = RenderApp("/probe");

        Assert.NotEmpty(app.FindAll(".probe-plain"));
        Assert.Single(Announcements);
    }

    /// <summary>The claim itself. While the gate is still asking, nothing routed exists — a
    /// signal sent from the gate's own render announces exactly this frame, and announcing it
    /// takes the splash off a screen with no page under it.</summary>
    [Fact]
    public void AGateStillDeciding_AnnouncesNothing()
    {
        Services.AddSingleton<IRouteGate>(new PendingRouteGate());

        var app = RenderApp("/probe");

        Assert.Empty(app.FindAll(".probe-plain"));
        Assert.Empty(Announcements);
    }

    /// <summary>And the signal is not lost by waiting: the gate answers, the page lands, the
    /// app announces itself. Unpinned, a splash that leaves too early is one refusal away from
    /// a splash that never leaves.</summary>
    [Fact]
    public void TheGateAnswering_AnnouncesTheAppThatFollowsIt()
    {
        var gate = new PendingRouteGate();

        Services.AddSingleton<IRouteGate>(gate);

        var app = RenderApp("/probe");

        gate.LetThrough();

        app.WaitForAssertion(() => Assert.Single(Announcements));
        Assert.NotEmpty(app.FindAll(".probe-plain"));
    }

    /// <summary>An address that matches no page is never gated — it renders outside
    /// NsRouteGate entirely — so it carries its own arrival. A splash that waits on the
    /// gate's side of the tree sits over the not-found face for good.</summary>
    [Fact]
    public void AnAddressThatMatchesNoPage_AnnouncesTheAppAnyway()
    {
        var app = RenderApp("/probe/this-route-does-not-exist");

        Assert.NotEmpty(app.FindAll("#ns-page-not-found"));
        Assert.Single(Announcements);
    }
}
