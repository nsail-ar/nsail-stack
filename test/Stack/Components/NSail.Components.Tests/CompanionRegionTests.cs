// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The frame's fourth region (nsail#2420): an agent that stays on screen while the
/// person works, which is why it is a region of the layout and not a surface. Nothing routes
/// here — so what is pinned is that a navigation on main leaves it standing with its content's
/// own state intact, that the service is the one way it opens and closes, and that the way out a
/// full-screen region owes is on screen.
///
/// <para>Calque of LayoutPersistenceTests' tree (NsRouter + a layout + two plain sibling pages),
/// because the claim is about the main route changing UNDER the region.</para></summary>
public sealed class CompanionRegionTests : BunitContext, IAsyncLifetime
{
    const string Key = "ns.companion.open";

    readonly FakeDeviceStorage _storage = new();

    public CompanionRegionTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Before AddComponentServices, whose own registration of the seam is a TryAdd: the
        // browser store is this test's dictionary, while everything else the tree asks of JS
        // (the vendor's) keeps answering through bUnit's loose runtime.
        Services.AddScoped(_ => new DeviceMemory(_storage));

        Services.AddComponentServices();
        Services.AddMessaging();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();

        // The X is the house's wordless inline button, which wraps itself in the vendor's
        // tooltip — a popover, and no provider stands in a layout fixture.
        Services.AddMudServices(mud => mud.PopoverOptions.CheckForPopoverProvider = false);

        // NsPage resolves its RouteTable from DI, and the one AddRouteTable builds is the
        // entry assembly's — a test host's, which carries none of these fixtures' routes.
        Services.AddSingleton(new RouteTable(typeof(ProbePage).Assembly, []));

        Services.AddSingleton<IRouteGate, FakeRouteGate>();

        AddAuthorization().SetAuthorized("probe");
    }

    // The tooltip above resolves a MudBlazor service that is IAsyncDisposable-only, so bUnit's
    // synchronous teardown cannot dispose it (NsLookupCreateEntryTests' own note).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    Companion Region
    {
        get { return Services.GetRequiredService<Companion>(); }
    }

    IRenderedComponent<NsRouter> RenderApp()
    {
        Navigation.NavigateTo("/probe");

        return Render<NsRouter>(p => p
            .Add(x => x.AppAssembly, typeof(ProbePage).Assembly)
            .Add(x => x.AdditionalAssemblies, [])
            .Add(x => x.DefaultLayout, typeof(CompanionLayout)));
    }

    /// <summary>A region nobody asked for draws nothing — and costs its content's own reads
    /// nowhere, the slot not being rendered at all.</summary>
    [Fact]
    public void TheRegionStandsClosedUntilSomethingOpensIt()
    {
        var app = RenderApp();

        Assert.Empty(app.FindAll(".ns-companion"));
        Assert.Empty(app.FindAll(".probe-companion"));
    }

    /// <summary>The service is the one way in and out: no link targets the region, and nothing
    /// in the URL says whether it stands open.</summary>
    [Fact]
    public async Task TheToggleOpensItAndClosesItAgain()
    {
        var app = RenderApp();

        await app.InvokeAsync(Region.Toggle);

        Assert.Single(app.FindAll(".ns-companion"));
        Assert.Single(app.FindAll(".probe-companion"));

        await app.InvokeAsync(Region.Toggle);

        Assert.Empty(app.FindAll(".ns-companion"));
    }

    /// <summary>The claim the whole region exists for: the page under it changes and the agent
    /// standing beside it is the same component, with what it had said still said. A surface
    /// would have been rebuilt from its route — this one is not addressed by one.</summary>
    [Fact]
    public async Task ANavigationOnMainLeavesTheRegionStandingWithItsContentsStateIntact()
    {
        var app = RenderApp();

        await app.InvokeAsync(Region.Open);

        var before = app.FindComponent<CompanionProbe>().Instance;

        app.Find(".probe-said").Click();
        app.Find(".probe-said").Click();

        Assert.Equal("2", app.Find(".probe-count").TextContent);

        // Sibling ("probe/other"), not an aside: the main route itself changes, which is the
        // navigation a region in the layout has to survive.
        Navigation.NavigateTo("/probe/other");

        Assert.Equal("other", app.Find("p").TextContent);
        Assert.Single(app.FindAll(".ns-companion"));
        Assert.Same(before, app.FindComponent<CompanionProbe>().Instance);
        Assert.Equal("2", app.Find(".probe-count").TextContent);
    }

    /// <summary>The open-with-text seam, which is what the menu's no-match entry carries in: the
    /// words reach the content, and a close takes them back — they are one door's handover, not a
    /// field of the region's.</summary>
    [Fact]
    public async Task ADoorMayOpenItCarryingWhatWasTyped()
    {
        var app = RenderApp();

        await app.InvokeAsync(() => Region.Open("lentes de contacto"));

        Assert.Equal("lentes de contacto", app.Find(".probe-text").TextContent);

        await app.InvokeAsync(Region.Close);
        await app.InvokeAsync(Region.Open);

        Assert.Equal(string.Empty, app.Find(".probe-text").TextContent);
    }

    /// <summary>Remembered per browser, so a person who works with the agent open finds it open
    /// on the next load. JS interop cannot run while a page prerenders, so the recall is the
    /// layout's first interactive render.</summary>
    [Fact]
    public void AStoredAnswerOpensTheRegionOnTheFirstRender()
    {
        _storage.Write(Key, "true");

        var app = RenderApp();

        app.WaitForAssertion(() => Assert.Single(app.FindAll(".probe-companion")));
        Assert.True(Region.IsOpen);
    }

    /// <summary>The other end of the same memory: the answer is written as it moves, under the
    /// frame's own key.</summary>
    [Fact]
    public async Task OpeningItIsWhatGetsRemembered()
    {
        var app = RenderApp();

        await app.InvokeAsync(Region.Open);

        app.WaitForAssertion(() => Assert.Equal("true", _storage.Items[Key]));

        await app.InvokeAsync(Region.Close);

        app.WaitForAssertion(() => Assert.Equal("false", _storage.Items[Key]));
    }

    /// <summary>A browser that cannot be read answers "closed", which is the region's own
    /// default: a machine with no memory behaves exactly like one that was never written to.</summary>
    [Fact]
    public void ABrowserThatRefusesStorageLeavesItClosed()
    {
        _storage.Write(Key, "true");
        _storage.Refusal = new Microsoft.JSInterop.JSException("storage is disabled");

        var app = RenderApp();

        Assert.Empty(app.FindAll(".ns-companion"));
        Assert.False(Region.IsOpen);
    }

    /// <summary>Escape is the way back from a region that is the whole screen, through the same
    /// wrapper the aside takes it from — which closes what it is NAMED here, the surface around
    /// this one being the main one.</summary>
    [Fact]
    public async Task EscapeClosesIt()
    {
        var app = RenderApp();

        await app.InvokeAsync(Region.Open);

        await app.Find(".ns-companion").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(app.FindAll(".ns-companion"));
        Assert.False(Region.IsOpen);
    }

    /// <summary>And the X beside it, drawn below the drawer's own breakpoint and nowhere else:
    /// docked, the page was never covered and the door that opened the region is still on
    /// screen. The gate is the WINDOW's width, the same edge the nav drawer docks on.</summary>
    [Fact]
    public async Task TheWayOutIsDrawnWhereTheRegionIsTheWholeScreen()
    {
        var app = RenderApp();

        await app.InvokeAsync(Region.Open);

        var exit = app.Find(".ns-companion-exit");

        Assert.Contains("d-flex", exit.ClassName);
        Assert.Contains("d-md-none", exit.ClassName);

        exit.QuerySelector("button")!.Click();

        Assert.False(Region.IsOpen);
    }

    /// <summary>The region's geometry is a promise CSS keeps and bUnit paints nothing, so it is
    /// read off the file the app ships (AccentInkTests' own idiom). Docked it is a flex item of a
    /// declared width — IN FLOW, which is what narrows the content instead of covering it — and
    /// below the nav's own breakpoint it is the whole viewport.</summary>
    [Fact]
    public void DockedItNarrowsTheContentAndOnAPhoneItFillsTheScreen()
    {
        var css = HouseStylesheet.Read();

        Assert.Contains(
            ".ns-companion {\n    flex: 0 0 var(--ns-companion-width);",
            css,
            StringComparison.Ordinal);

        Assert.Contains(
            "@media (max-width: 959.98px) {\n    .ns-companion {\n        position: fixed;\n        inset: 0;",
            css,
            StringComparison.Ordinal);
    }

    /// <summary>And the aside keeps opening over the content, to the companion's left: it is a
    /// fixed overlay pinned to the right edge, so a region in flow cannot push it — the offset is
    /// stated, for the band of widths where the two stand on screen together.</summary>
    [Fact]
    public void TheAsideOpensToTheCompanionsLeft()
    {
        var css = HouseStylesheet.Read();

        Assert.Contains(
            ".mud-layout:has(.ns-companion) .ns-drawer.mud-drawer.mud-drawer-pos-right.mud-drawer--open {\n        right: var(--ns-companion-width);",
            css,
            StringComparison.Ordinal);
    }
}
