// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>The form grid lays fields out by their WIDTH NATURE — content, grow or full — never a
/// column fraction, and it wraps by the CONTAINER the form sits in, not the window: an aside on a
/// wide desktop is narrow. bUnit reaches exactly half of that — the mode class an item emits and
/// the ancestor that will do the measuring. The other half is the flex-wrap and the widths in
/// ns-mud.css, which only a browser at a real width can run: no assertion here proves that a 320px
/// panel folds the row, and the stylesheet's own rules are the Architect's pixel probe.</summary>
public sealed class NsFormGridTests : BunitContext, IAsyncLifetime
{
    public NsFormGridTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddComponentServices();
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();

        // The aside fixture renders a real NsDrawer, whose docked/overlay decision reads the
        // viewport service — JS-backed, and bUnit's loose interop answers it with nothing.
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

    static string ItemClasses(IRenderedComponent<NsFormGridItem> cut)
    {
        return cut.Find("div").GetAttribute("class") ?? string.Empty;
    }

    [Fact]
    public void AnItemThatSaysNothing_isContentWidth()
    {
        var cut = Render<NsFormGridItem>();

        // The default is the field's own width — never stretched to fill a lonely line. This is
        // the whole point of the retirement: a field that declares no intent is content-width, not
        // a full-row default that inflates a date to 600px.
        Assert.Equal("ns-form-field", ItemClasses(cut));
    }

    [Fact]
    public void AGrowItem_carriesTheGrowClass_andNotTheContentOne()
    {
        var cut = Render<NsFormGridItem>(p => p.Add(x => x.Grow, true));

        // Grow is the escape hatch the content-width default leaves open: a field forced to fill
        // when the layout calls for it. It replaces the content class, never rides beside it — two
        // width natures on one item would be the fraction sneaking back as a class pair.
        Assert.Equal("ns-form-field-grow", ItemClasses(cut));
    }

    [Fact]
    public void AFullItem_ownsItsRow()
    {
        var cut = Render<NsFormGridItem>(p => p.Add(x => x.Full, true));

        Assert.Equal("ns-form-field-full", ItemClasses(cut));
    }

    [Fact]
    public void FullWinsOverGrow_soABlockAskedForBothIsUnambiguous()
    {
        var cut = Render<NsFormGridItem>(p => p
            .Add(x => x.Grow, true)
            .Add(x => x.Full, true));

        // Precedence in the component, not a guard at the call site: a block asked to both grow and
        // own its row resolves to the row, deterministically, rather than emitting both classes.
        Assert.Equal("ns-form-field-full", ItemClasses(cut));
    }

    [Fact]
    public void TheCallersOwnClassRidesAlong()
    {
        var cut = Render<NsFormGridItem>(p => p
            .Add(x => x.Grow, true)
            .Add(x => x.Class, "probe"));

        Assert.Equal("ns-form-field-grow probe", ItemClasses(cut));
    }

    [Fact]
    public void ARecordStateItem_isADirectChildOfTheGrid_soItsCenteringSelectorMatches()
    {
        // nsail#213: the CSS centers a switch's cell with `.ns-grid > *:has(> .ns-record-state)`
        // -- a selector that only fires if ns-record-state is a direct child of the item the
        // grid itself parents. This pins that structural assumption; whether align-self actually
        // moves the switch to mid-height beside a taller neighbour is the Architect's pixel probe.
        var cut = Render<NsFormGridItem>(p => p.AddChildContent<NsRecordState>());

        var item = cut.Find("div");

        Assert.Contains("ns-form-field", item.ClassList);
        Assert.Contains(item.Children, child => child.ClassList.Contains("ns-record-state"));
    }

    [Fact]
    public void ACheckBoxItem_isADirectChildOfTheGrid_soItsCenteringSelectorMatches()
    {
        // nsail#2004: the same selector, the other boolean control --
        // `.ns-grid > *:has(> .ns-form-check)`. NsCheckBox renders a second child (NsFieldHelper,
        // which draws nothing while clean), so the mark has to ride the box itself and not the
        // item; this pins that it does.
        var cut = Render<NsFormGridItem>(p => p.AddChildContent<NsCheckBox<bool>>());

        var item = cut.Find("div");

        Assert.Contains("ns-form-field", item.ClassList);
        Assert.Contains(item.Children, child => child.ClassList.Contains("ns-form-check"));
    }

    [Fact]
    public void TheGridIsAFlexWrapBand_andItsDefaultGapIsTheHouseDefault()
    {
        var cut = Render<NsFormGrid>();

        // The class stays ns-grid — the flex-wrap band the stylesheet now defines — and gap-4 is
        // 16px, the same gutter the grid has always used, so migrating a form changes how it packs
        // and not how far apart it sits.
        Assert.Equal("ns-grid gap-4", cut.Find("div").GetAttribute("class"));
    }

    [Theory]
    [InlineData(NsSize.None, "gap-0")]
    [InlineData(NsSize.Xs, "gap-1")]
    [InlineData(NsSize.Sm, "gap-2")]
    [InlineData(NsSize.Md, "gap-4")]
    [InlineData(NsSize.Lg, "gap-6")]
    [InlineData(NsSize.Xl, "gap-8")]
    [InlineData(NsSize.Xxl, "gap-10")]
    public void TheGapIsTheHouseScale(NsSize gap, string expected)
    {
        var cut = Render<NsFormGrid>(p => p.Add(x => x.Gap, gap));

        Assert.Equal($"ns-grid {expected}", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void InsideAnAside_theElementThatMeasures_isThePanelAndNotTheWindow()
    {
        var cut = Render<FormGridAsideHost>(p => p.Add(x => x.RouteTable, BuildRouteTable()));

        var grid = cut.Find(".ns-grid");

        var measuring = grid.ParentElement;

        while (measuring is not null && !measuring.ClassList.Contains("ns-container"))
        {
            measuring = measuring.ParentElement;
        }

        // The whole point of the story in one assertion: the nearest container-type ancestor of a
        // form's grid is the panel the page rendered, and that panel lives INSIDE the drawer's
        // paper — so the width the spans resolve against is the aside's, which on a 1920px
        // desktop is a narrow box. A grid whose nearest such ancestor were outside the drawer
        // (or absent, leaving the root) would answer the desktop's width instead, silently.
        Assert.NotNull(measuring);

        var drawer = cut.Find(".mud-drawer");

        Assert.NotSame(drawer, measuring);
        Assert.Contains(measuring, drawer.QuerySelectorAll(".ns-container"));

        // And the drawer does not declare itself a container: if it did, the panel's padding
        // would be measured in and the two would disagree about what "the aside's width" is.
        Assert.DoesNotContain("ns-container", drawer.ClassList);

        // The items are DIRECT children of the grid: the width rules are written `.ns-grid > .ns-*`,
        // so an item one level deeper is silently just a block at no width the stylesheet answers.
        Assert.Equal(["ns-form-field", "ns-form-field-grow"],
            grid.Children.Select(child => child.GetAttribute("class")));
    }

    [Fact]
    public void TheAsidesXlWidth_stillReachesTheDrawer_evenBeforeTheBreakpointIsMeasured()
    {
        // Both products' MainLayout.razor hardcode Mode="Docked" for every aside, and NsDrawer
        // assumes Breakpoint.Lg (inside IsDocked's Md..Xxl band) until the JS-backed viewport
        // service reports the real one -- so on first paint, a phone's aside opens believing
        // itself docked and renders MudBlazor's Persistent variant, not Temporary. MudDrawer's
        // own inline "--mud-drawer-width" override is conditional on (!IsFixed || Temporary):
        // a docked, IsFixed drawer (real MudLayout ancestor, which FormGridAsideHost's story
        // is precisely about) fails that condition and omits the override entirely -- so
        // "wrongly docked" is not just a variant/class difference, it is a WIDTH story too.
        // What saves the Xl ask is a second path: MudLayout publishes every registered
        // drawer's OWN Width as --mud-drawer-width-{side} on itself (MudDrawerContainer
        // registers a drawer whenever its Variant isn't Temporary, which the wrongly-docked
        // state is), and CSS custom properties inherit -- so the drawer's own missing
        // override still resolves through its ancestor's fallback. This pins both halves of
        // that rescue from the C# side; whether the inherited value actually PAINTS at 100%
        // is CSS inheritance, which only a browser runs.
        Services.AddScoped<IBrowserViewportService>(_ => new NeverRespondingViewportService());

        var cut = Render<FormGridAsideHost>(p => p.Add(x => x.RouteTable, BuildRouteTable()));

        var drawer = cut.Find(".mud-drawer");

        Assert.Contains("mud-drawer-persistent", drawer.ClassList);
        Assert.Contains("mud-drawer-fixed", drawer.ClassList);
        Assert.DoesNotContain("--mud-drawer-width:", drawer.GetAttribute("style"));

        var layout = cut.Find(".mud-layout");

        Assert.Contains("--mud-drawer-width-right:100%", layout.GetAttribute("style"));
    }

    static RouteTable BuildRouteTable()
    {
        return new(typeof(NsFormGridTests).Assembly, Array.Empty<System.Reflection.Assembly>());
    }
}
