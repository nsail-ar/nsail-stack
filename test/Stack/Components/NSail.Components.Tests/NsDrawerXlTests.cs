// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>Doctrine (ui/surfaces.md, The size seam): "`Xl` in the drawer means as wide as the
/// surface may be — never full-bleed", and "No aside covers the nav gutter on desktop."
/// GetWidth's Xl case asks for 100% of the drawer's own box; what turns that into a real
/// number is .ns-drawer's max-width in ns-mud.css — the viewport less the nav drawer's own
/// declared width above the docking breakpoint, the whole viewport below it. That half is
/// CSS and no bUnit assertion can reach it: these tests pin the width the component declares
/// and the mechanism around it, and the cap is verified against the served stylesheet and by
/// eye. These render NsDrawer through a real NsSurfaceContext (mirrors Directory.Shared.
/// Tests' UpdatePartyHost) so close-by-X and the unsaved-changes guard are proven against the
/// real SurfaceContext, not a stand-in — and mirrored at Md/Lg/Xxl to show nothing about that
/// mechanism moved with the width.</summary>
public sealed class NsDrawerXlTests : BunitContext, IAsyncLifetime
{
    public NsDrawerXlTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();

        // Replaces MudBlazor's real viewport service (JS-backed; bUnit's loose JSInterop
        // can't answer it meaningfully) with a fake that reports a wide desktop breakpoint
        // synchronously — the width/backdrop tests below need NsDrawer's IsDocked (Md..Xxl)
        // to resolve deterministically rather than depend on whatever a mocked JS call
        // happens to return.
        Services.AddScoped<IBrowserViewportService>(_ => new FixedBreakpointViewportService(Breakpoint.Lg));
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static RouteTable BuildRouteTable()
    {
        return new(typeof(NsDrawerXlTests).Assembly, Array.Empty<System.Reflection.Assembly>());
    }

    // MudDrawer paints Width through the --mud-drawer-width custom property (the comment
    // at NsDrawer.razor the story pointed at), not a plain CSS width — asserting the exact
    // property/value pair, not a loose Contains, so a match against "--mud-drawer-width:960px"
    // (the old value, still containing the substring "width:...") can't pass for the wrong
    // reason. MudDrawer appends NsDrawer's own Style after that custom property, so the full
    // string also carries the drawer's stacking tier. This host renders with Mode=Overlay,
    // which is never docked, so that tier is the vendor's sheet tier — a docked drawer takes
    // --mud-zindex-drawer instead, and SurfaceStackLayeringTests owns both facts.
    const string SheetZIndex = "z-index: calc(var(--mud-zindex-appbar) + 2);";

    static string DrawerWidthStyle(IRenderedComponent<NsDrawerHost> host)
    {
        return host.Find(".mud-drawer").GetAttribute("style") ?? string.Empty;
    }

    [Theory]
    [InlineData(NsSize.Xs, "320px")]
    [InlineData(NsSize.Sm, "400px")]
    [InlineData(NsSize.Md, "480px")]
    [InlineData(NsSize.Lg, "720px")]
    [InlineData(NsSize.Xxl, "1140px")]
    public async Task SizesOtherThanXl_keepTheirOwnFixedPixelWidth_unchangedByThisStory(NsSize size, string expectedWidth)
    {
        var host = Render<NsDrawerHost>(p => p.Add(x => x.RouteTable, BuildRouteTable()));

        await host.InvokeAsync(() => host.Instance.Surface!.SetSize(size));
        host.Render();

        Assert.Equal($"--mud-drawer-width:{expectedWidth};{SheetZIndex}", DrawerWidthStyle(host));
    }

    [Fact]
    public async Task Xl_asksForTheWholeSurface_andLetsTheStylesheetCapIt()
    {
        var host = Render<NsDrawerHost>(p => p.Add(x => x.RouteTable, BuildRouteTable()));

        await host.InvokeAsync(() => host.Instance.Surface!.SetSize(NsSize.Xl));
        host.Render();

        Assert.Equal($"--mud-drawer-width:100%;{SheetZIndex}", DrawerWidthStyle(host));

        var drawer = host.Find(".mud-drawer");

        // ClipMode.Always is untouched by these stories: the app bar's own chrome sits above
        // the drawer (MainLayout renders Header outside the drawer entirely), and the
        // drawer keeps "mud-drawer-clipped-always" regardless of width — a fullscreen modal
        // would instead cover the app bar, which is deliberately not what this is.
        Assert.Contains("mud-drawer-clipped-always", drawer.ClassList);
    }

    [Fact]
    public async Task TheNavGutterTheCapLeavesIsTheLayoutsOwnCustomProperty()
    {
        // .ns-drawer's max-width is calc(100vw - var(--mud-drawer-width-left, 0px)), so the
        // gutter is never restated in the stylesheet — it is whatever width NsMainLayout
        // declared for its nav drawer, carried by the custom property MudDrawerContainer
        // writes on the layout root. If MudBlazor ever stopped writing it the calc would fall
        // back to 0px and an Xl aside would quietly go full-bleed again, with nothing else in
        // the app noticing: that is what this pins.
        var cut = Render<LayoutGutterHost>();

        var layout = cut.Find(".mud-layout");

        Assert.Contains("mud-drawer-open-responsive-md-left", layout.ClassName);
        Assert.Contains("--mud-drawer-width-left:280px", layout.GetAttribute("style"));

        // And it is written whether the nav is open or closed — measured, not assumed. So the
        // cap holds the gutter for a nav the user collapsed too, which is the honest cost
        // named in ns-mud.css beside the rule: a collapsed menu leaves its strip showing the
        // page rather than the aside, and the direction errs toward the menu staying
        // reachable.
        await cut.InvokeAsync(cut.Instance.CloseNav);
        cut.Render();

        layout = cut.Find(".mud-layout");

        Assert.Contains("mud-drawer-close-responsive-md-left", layout.ClassName);
        Assert.Contains("--mud-drawer-width-left:280px", layout.GetAttribute("style"));
    }

    [Theory]
    [InlineData(NsSize.Md)]
    [InlineData(NsSize.Xl)]
    public async Task CloseByX_removesTheAsideQueryParameter_regardlessOfSize(NsSize size)
    {
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("https://app.test/parties?aside=directory%2Fparties%2Fnew", false);

        var host = Render<NsDrawerHost>(p => p.Add(x => x.RouteTable, BuildRouteTable()));
        await host.InvokeAsync(() => host.Instance.Surface!.SetSize(size));
        host.Render();

        await host.InvokeAsync(() => host.Find(".drawer-close").Click());

        Assert.DoesNotContain("aside=", navigation.Uri);
    }

    [Theory]
    [InlineData(NsSize.Md)]
    [InlineData(NsSize.Xl)]
    public async Task UnsavedChangesGuard_tracksDirtyStateIdentically_regardlessOfSize(NsSize size)
    {
        var host = Render<NsDrawerHost>(p => p.Add(x => x.RouteTable, BuildRouteTable()));
        var surface = host.Instance.Surface!;
        await host.InvokeAsync(() => surface.SetSize(size));
        host.Render();

        Assert.False(surface.HasChanges);

        var dirtySource = new object();
        await host.InvokeAsync(() => surface.SetDirty(dirtySource));

        // NsSurfaceContext's NavigationLock (ConfirmExternalNavigation="_surfaceContext.
        // HasChanges") reads exactly this flag to gate navigation away from the surface —
        // it is entirely independent of NsDrawer.GetWidth()/Size, which this asserts by
        // construction: the flag flips the same way whatever size the drawer renders at.
        Assert.True(surface.HasChanges);

        await host.InvokeAsync(() => surface.SetUnchanged(dirtySource));
        Assert.False(surface.HasChanges);
    }

    [Fact]
    public async Task Xl_stillUsesTheTemporaryVariantWithABackdrop_soCloseByXAndEscapeStayReachable()
    {
        // Backdrop-inertness finding: IsDocked/GetVariant() never read Size, so Xl gets
        // exactly the same variant/backdrop decision as every other size — Overlay mode
        // (the default, and what today's Xl consumers actually render with, since none of
        // them call SetFloating) stays Temporary with a real .mud-overlay backdrop element.
        // What changes at Xl is geometry, not mechanism: the drawer paper now spans the
        // same box the backdrop covers (both keyed to the drawer's own width/height), so
        // there is no "outside the drawer" pixel left for a click to land on — the backdrop
        // is inert by being covered, not by any special-cased code path. Verified here by
        // confirming the backdrop still renders (so Escape/focus-trap semantics MudDrawer
        // gives it are untouched) and that NsClose's X — the actually reachable exit at any
        // size — still renders and still works (proven by CloseByX_... above, same host).
        var host = Render<NsDrawerHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Mode, NsDrawerMode.Overlay));

        await host.InvokeAsync(() => host.Instance.Surface!.SetSize(NsSize.Xl));
        host.Render();

        var drawer = host.Find(".mud-drawer");

        Assert.Contains("mud-drawer-temporary", drawer.ClassList);
        Assert.DoesNotContain("mud-drawer--persistent", drawer.ClassList);

        var backdrop = host.FindAll(".mud-drawer-overlay");
        Assert.Single(backdrop);

        Assert.Single(host.FindAll(".drawer-close"));
    }

    [Fact]
    public async Task Xl_inDockedMode_rendersNoBackdropAtAll_soThereIsNothingToBeInert()
    {
        // The other half of the backdrop question: MainLayout's aside slot renders NsDrawer
        // with Mode="Docked" (MainLayout.razor), and IsDocked (breakpoint Md..Xxl, not
        // Floating) sets MudDrawer's own Overlay="@(!IsDocked)" to false — Docked never
        // paints a backdrop at any size, Xl included, so "a backdrop that immediately closes"
        // cannot happen here structurally: there is no backdrop element to misfire. NsDrawer
        // assumes Breakpoint.Lg until the viewport service reports in (its own comment,
        // "Assume a wide viewport until measured"), which is within IsDocked's Md..Xxl band,
        // so this is the state a first render actually reaches without a real browser.
        var host = Render<NsDrawerHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Mode, NsDrawerMode.Docked));

        await host.InvokeAsync(() => host.Instance.Surface!.SetSize(NsSize.Xl));
        host.Render();

        var drawer = host.Find(".mud-drawer");

        Assert.DoesNotContain("mud-drawer-temporary", drawer.ClassList);
        Assert.Empty(host.FindAll(".mud-drawer-overlay"));

        // The X is still the reachable exit even when docked/full.
        Assert.Single(host.FindAll(".drawer-close"));
    }
}
