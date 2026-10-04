// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>Doctrine (ui/surfaces.md, The stacking seam): surfaces rank on MudBlazor's own tiers
/// and no docked surface outranks a header the layout mounts — the aside occupies the region
/// UNDER it; and the overlay under a modal covers the VIEWPORT, so header, nav gutter, an open
/// aside and the page all dim together. The shipped shells mount no header; SurfaceStackHost
/// mounts one (an app bar in Header, nav in Drawer, the modal surface inside Content, the aside
/// in Aside) because the clipped geometry only exists beside one, and every layering fact here
/// is decided by the four together.
///
/// What a bUnit harness reaches is the tree and the tiers written into it. MudOverlay — the
/// modal's backdrop — portals through MudPopoverProvider and renders no markup here at all, so
/// its own scope is asserted through the class NsDialog no longer puts on either box; that it
/// paints dark across the whole viewport is a rendered-viewport fact.</summary>
public sealed class SurfaceStackLayeringTests : BunitContext, IAsyncLifetime
{
    // MudBlazor's own tiers, the ones our surfaces are ranked against. Written here because a
    // test asserting "under the app bar" has to name what the app bar is: MudThemeProvider
    // emits --mud-zindex-appbar: 1300 and --mud-zindex-drawer: 1100 from LayoutProperties
    // defaults, which BrandMudTheme leaves alone (it overrides only the dialog/popover/
    // snackbar/tooltip tiers, with SurfaceContext.VendorZIndex).
    const int AppBarZIndex = 1300;
    const int DrawerZIndex = 1100;

    Breakpoint _breakpoint = Breakpoint.Lg;

    public SurfaceStackLayeringTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddComponentServices();
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();

        // MudBlazor's real viewport service is JS-backed; the fake reports a breakpoint
        // synchronously so NsDrawer's IsDocked resolves deterministically (same idiom as
        // NsDrawerXlTests).
        Services.AddScoped<IBrowserViewportService>(_ => new FixedBreakpointViewportService(_breakpoint));
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    IRenderedComponent<SurfaceStackHost> RenderStack(bool aside = true, bool modal = true, bool header = true)
    {
        return Render<SurfaceStackHost>(p => p
            .Add(x => x.RouteTable, new RouteTable(typeof(SurfaceStackLayeringTests).Assembly, []))
            .Add(x => x.Aside, aside)
            .Add(x => x.Modal, modal)
            .Add(x => x.Header, header));
    }

    [Fact]
    public void TheDockedAside_takesTheVendorsDrawerTier_notASurfaceTierAboveTheAppBar()
    {
        var host = RenderStack(modal: false);

        var style = host.Find(".ns-drawer").GetAttribute("style") ?? string.Empty;

        // The whole of defect 1: the aside used to be pinned at SurfaceContext.ZIndex (1400)
        // while MudBlazor ranks the app bar at 1300, so the only thing keeping a drawer off the
        // bar was a vendor geometry rule. A docked drawer is chrome beside the page and belongs
        // at the vendor's own drawer tier, which IS below the bar.
        Assert.Contains("z-index: var(--mud-zindex-drawer)", style);
        Assert.DoesNotContain("1400", style);

        Assert.True(DrawerZIndex < AppBarZIndex);
    }

    [Fact]
    public void TheDockedAside_declaresTheClippedGeometry_soItOccupiesTheRegionUnderTheBar()
    {
        var host = RenderStack(modal: false);

        // ClipMode.Always is MudBlazor's own way of saying "under the app bar"; ns-mud.css
        // restates the same geometry on .ns-drawer.mud-drawer.mud-drawer-clipped-always so it
        // survives a variant MudBlazor's clipped-* selectors do not list. The class is what
        // both hang off, which is why it is the thing pinned here.
        Assert.Contains("mud-drawer-clipped-always", host.Find(".ns-drawer").ClassList);
    }

    [Fact]
    public void TheDockedAside_withNoHeaderSlot_doesNotReserveABarHeightGap()
    {
        // nsail#666: Optical and Therapy stopped passing a Header (nsail#609), but the aside
        // kept ClipMode.Always, so ns-mud.css's own rule (top: var(--mud-appbar-height)) went
        // on reserving a bar-height gap above a drawer with no bar to sit under — and shorting
        // the same height off its bottom. NsMainLayout.OnParametersSet reads Header is null
        // into NsLayoutState.HasHeader; GetClipMode reads it back. Asserting the class flips
        // to clipped-never (rather than just "not clipped-always") is what proves the geometry
        // actually reverts to MudDrawer's plain top:0/height:100% instead of landing on some
        // other clip variant that would reserve a different, still-wrong gap.
        var host = RenderStack(modal: false, header: false);

        var classList = host.Find(".ns-drawer").ClassList;

        Assert.Contains("mud-drawer-clipped-never", classList);
        Assert.DoesNotContain("mud-drawer-clipped-always", classList);
    }

    [Fact]
    public void TheAside_isASiblingOfTheAppBarUnderTheLayout_andRendersAfterIt()
    {
        var host = RenderStack(modal: false);

        var layout = host.Find(".mud-layout");
        var children = layout.Children.ToList();

        var bar = children.FindIndex(x => x.ClassList.Contains("mud-appbar"));
        var aside = children.FindIndex(x => x.ClassList.Contains("ns-drawer"));

        // Both are direct children of MudLayout — the drawer is deliberately NOT inside
        // MudMainContent, which is what lets a docked drawer push the content aside. The bar
        // comes first, so with equal tiers the drawer could never win on document order either.
        Assert.True(bar >= 0);
        Assert.True(aside > bar);

        Assert.Contains("mud-main-content", children[aside - 1].ClassList);
    }

    [Fact]
    public void TheModalCoversTheViewport_carryingNoScopeThatWouldLeaveTheNavGutterLit()
    {
        var host = RenderStack();

        var wrapper = host.Find(".fixed.inset-0");

        // Defect 2: the wrapper and the backdrop were both inset from the left by the docked
        // nav's own width on desktop, which left that strip undimmed — and with an aside open at
        // Xl the strip shows the ASIDE, not the nav. Nothing scopes either box any more.
        Assert.DoesNotContain("ns-modal-scope", host.Markup);
        Assert.Contains("inset-0", wrapper.ClassList);
    }

    [Fact]
    public void TheModalOutranksTheAppBarAndTheAside_soEverythingUnderItDims()
    {
        var host = RenderStack();

        var modal = host.Instance.ModalSurface!;

        // The modal's paper and its backdrop (ZIndex - 1) both clear the app bar, which is what
        // makes the bar dim rather than stand lit over a modal; the aside is two tiers down.
        Assert.True(modal.ZIndex - 1 > AppBarZIndex);
        Assert.True(AppBarZIndex > DrawerZIndex);

        Assert.Equal($"z-index: {modal.ZIndex}", host.Find(".fixed.inset-0").GetAttribute("style"));
    }

    [Fact]
    public void BelowTheDockingBreakpoint_theAsideIsASheetOverTheBar_whichIsTheMobileDegradation()
    {
        _breakpoint = Breakpoint.Sm;

        var host = RenderStack(modal: false);

        var drawer = host.Find(".ns-drawer");

        // BY DESIGN, and pinned because the obvious "fix" for defect 1 is to push every drawer
        // under the bar at every width: below Md the aside is a full-screen sheet, and MudDrawer
        // paints its own backdrop for it at --mud-zindex-appbar + 1. A sheet ranked under the
        // bar would therefore sit under its own backdrop. The vendor's two tiers are kept as the
        // pair they are.
        Assert.Contains("mud-drawer-temporary", drawer.ClassList);
        Assert.Contains("z-index: calc(var(--mud-zindex-appbar) + 2)", drawer.GetAttribute("style") ?? string.Empty);
    }
}
