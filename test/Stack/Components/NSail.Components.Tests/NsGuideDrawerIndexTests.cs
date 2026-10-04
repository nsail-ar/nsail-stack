// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#928, ruling 5 — "en el celular se colapsa en el drawer exactamente como lo hace
/// el del onboarding". The guide's rail stands beside the content where the frame has room for
/// two columns and goes into the drawer where it does not, which is the same gutter and the same
/// edge the nav menu itself answers to. The channel is the surface's own announcement: the page
/// hands its index over and stops deciding where it goes.</summary>
public sealed class NsGuideDrawerIndexTests : BunitContext, IAsyncLifetime
{
    const string Assets = "_content/NSail.Components.Tests/guide/es";

    public NsGuideDrawerIndexTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Guide.Accounting"] = "Contabilidad",
            ["Guide.Sales"] = "Ventas",
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(ProbeGuidePage).Assembly, []));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        Services.AddScoped<PageGate>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddScoped(provider => new GuideRoute(
            typeof(ProbeGuidePage),
            provider.GetRequiredService<RouteTable>()));
        Services.AddScoped<GuideReader>();
        Services.AddScoped(_ => new HttpClient(new StaticAssets(new Dictionary<string, string>
        {
            [$"{Assets}/sales.md"] = "## Registrar una Venta",
            [$"{Assets}/accounting.md"] = "## Cobrar",
        })));
        Services.AddScoped<IGuideContributor>(_ => new FixedGuideContributor(
            new GuideItem { Name = "Sales", File = "sales", Weight = 10 },
            new GuideItem { Name = "Accounting", File = "accounting", Weight = 30 }));

        JSInterop.Mode = JSRuntimeMode.Loose;

        this.AddAuthorization();
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    IRenderedComponent<GuideFrameHost> Frame(string? chapter = null)
    {
        var surface = Services.GetRequiredService<RootSurface>().Surface;

        return Render<GuideFrameHost>(parameters => parameters
            .Add(host => host.Surface, surface)
            .Add(host => host.Chapter, chapter));
    }

    [Fact]
    public void TheDrawerDrawsTheOpenPagesIndexAboveTheAppsOwnMap()
    {
        var cut = Frame("accounting");

        Assert.Equal(
            ["Ventas", "Contabilidad"],
            cut.FindAll(".ns-nav-index .ns-guide-chapter").Select(entry => entry.TextContent));

        Assert.Equal(
            "Contabilidad",
            cut.Find(".ns-nav-index .ns-guide-chapter.ns-guide-entry-current").TextContent);
    }

    // The nav is not displaced by the page that borrowed its gutter: the drawer's own map is
    // still under the index, which is what makes sharing the drawer honest.
    [Fact]
    public void TheNavMenuIsStillReachableUnderIt()
    {
        var cut = Frame();

        Assert.NotEmpty(cut.FindAll(".ns-nav-index"));
        Assert.NotEmpty(cut.FindAll(".mud-navmenu"));
    }

    // The pair is one decision written twice (NsResponsive): the drawer's copy below the
    // drawer's own breakpoint, the column beside the content from it up, never both.
    [Fact]
    public void TheTwoPlacesTheRailCanStandTradeOnTheDrawersOwnBreakpoint()
    {
        var cut = Frame();

        Assert.Contains("d-md-none", cut.Find(".ns-nav-index").ClassName, StringComparison.Ordinal);
        Assert.Contains("d-none d-md-flex", cut.Find(".ns-guide-rail").ClassName, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSectionsThatArriveWithAChapterReachTheDrawerToo()
    {
        var cut = Frame("accounting");

        cut.WaitForAssertion(() => Assert.Equal(
            ["Cobrar"],
            cut.FindAll(".ns-nav-index .ns-guide-section").Select(entry => entry.TextContent)));
    }

    [Fact]
    public void ThePageThatAnnouncedNoIndexPutsNothingInTheDrawer()
    {
        var cut = Render<NsNavMenu>();

        Assert.Empty(cut.FindAll(".ns-nav-index"));
    }
}
