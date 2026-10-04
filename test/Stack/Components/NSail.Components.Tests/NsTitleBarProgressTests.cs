// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Icons;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>Leonardo, 2026-08-10: "¿podemos mostrar el ícono de carga EN LUGAR DEL ícono de la
/// página en vez de meter otro ícono al final?". The title bar used to append NsIcons.Progress
/// after the title while the surface had work, which is a third icon appearing and disappearing
/// in a row that is supposed to be still — geometry is reserved, never moved. The loading state
/// takes over the page icon's own slot: one icon either way, in one place, and the title never
/// shifts under the eye.
///
/// On the main surface the slot is the page's own bar again (nsail#517: there is no app bar from
/// the drawer's breakpoint up), so these read it out of NsTitleBar — the minimal bar below that
/// breakpoint holds the same one slot from the same announcement, and the two never show at
/// once.</summary>
public sealed class NsTitleBarProgressTests : BunitContext
{
    static readonly Glyph PageGlyph = NsIcons.Badge;

    sealed class FakeJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return default;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return default;
        }
    }

    SurfaceContext Setup()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.Loading"] = "Cargando"
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton(new RouteTable(typeof(NsTitleBarProgressTests).Assembly, []));
        JSInterop.Mode = JSRuntimeMode.Loose;

        var routes = Services.GetRequiredService<RouteTable>();
        var navigation = Services.GetRequiredService<NavigationManager>();

        // The main surface: the page's own bar draws the announcement it makes, so the icon
        // slot and the title are read out of it.
        return new SurfaceContext(null, navigation, routes, new FakeJs(), new SurfaceHistory());
    }

    // The page's own bar, not the whole host: the minimal top bar holds the same slot from the
    // same announcement, and this pins the one that draws where the drawer is docked.
    static IRenderedComponent<NsTitleBar> Bar(IRenderedComponent<MainSurfaceChromeHost> cut)
    {
        return cut.FindComponent<NsTitleBar>();
    }

    IRenderedComponent<MainSurfaceChromeHost> RenderBar(SurfaceContext surface, Glyph? icon)
    {
        return Render<MainSurfaceChromeHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.Title, "Personas")
            .Add(x => x.Icon, icon));
    }

    [Fact]
    public void AtRestTheSlotHoldsThePagesOwnGlyph()
    {
        var surface = Setup();

        var cut = RenderBar(surface, PageGlyph);

        var icons = Bar(cut).FindComponents<NsIcon>();

        Assert.Single(icons);
        Assert.Equal(PageGlyph, icons[0].Instance.Icon);
    }

    [Fact]
    public async Task WhileWorkingTheSameSlotHoldsProgressAndNothingElse()
    {
        var surface = Setup();
        var cut = RenderBar(surface, PageGlyph);

        await cut.InvokeAsync(surface.Enter);

        var icons = Bar(cut).FindComponents<NsIcon>();

        // One icon, not two: the third glyph after the title is what this cure removed.
        Assert.Single(icons);
        Assert.Equal(NsIcons.Progress, icons[0].Instance.Icon);

        // The accessible name the appended indicator used to carry travels with it into the
        // slot — the swap is a change of glyph, never a loss of what it says.
        Assert.Equal("Cargando", icons[0].Instance.Title);
    }

    [Fact]
    public async Task WhenTheWorkEndsThePagesGlyphComesBack()
    {
        var surface = Setup();
        var cut = RenderBar(surface, PageGlyph);

        await cut.InvokeAsync(surface.Enter);
        await cut.InvokeAsync(surface.Exit);

        var icons = Bar(cut).FindComponents<NsIcon>();

        Assert.Single(icons);
        Assert.Equal(PageGlyph, icons[0].Instance.Icon);
    }

    [Fact]
    public async Task APageWithNoGlyphLendsTheEmptySlotToProgress()
    {
        var surface = Setup();

        // Somewhere the route table knows nothing about, which is what a page carrying no menu
        // entry of its own looks like to the bar's derivation: it resolves no glyph and the
        // slot starts empty. Not the app root, which routes like anywhere else and would
        // derive the home page's own glyph (NsTitleBarRootRouteTests).
        Services.GetRequiredService<NavigationManager>().NavigateTo("/nowhere");

        var cut = RenderBar(surface, null);

        Assert.Empty(Bar(cut).FindComponents<NsIcon>());

        await cut.InvokeAsync(surface.Enter);

        var icons = Bar(cut).FindComponents<NsIcon>();

        Assert.Single(icons);
        Assert.Equal(NsIcons.Progress, icons[0].Instance.Icon);
    }

    /// <summary>The emptiest case the slot has to hold: a page with no glyph of its own and a
    /// surface at rest puts nothing inside ns-title-icon-slot, and the slot still stands. That is
    /// what reserves the same box from the first paint, so the spinner arriving later fills a
    /// space that was already there instead of opening one.</summary>
    [Fact]
    public void ASlotStandsEvenWithNoGlyphAndNoWork()
    {
        var surface = Setup();

        Services.GetRequiredService<NavigationManager>().NavigateTo("/nowhere");

        var cut = RenderBar(surface, null);

        Assert.Empty(Bar(cut).FindComponents<NsIcon>());
        Assert.Single(Bar(cut).FindAll(".ns-title-icon-slot"));
    }

    /// <summary>The property the swap exists to buy: the title occupies the same position in the
    /// same row of nodes whether or not the surface is working, so nothing under the reader's eye
    /// moves when a load starts or ends.</summary>
    [Fact]
    public async Task TheTitleKeepsItsPlaceWhileTheIconSwaps()
    {
        var surface = Setup();
        var cut = RenderBar(surface, PageGlyph);

        Assert.Equal(1, TitleIndex(cut));
        Assert.Equal(2, Siblings(cut));

        await cut.InvokeAsync(surface.Enter);

        Assert.Equal(1, TitleIndex(cut));
        Assert.Equal(2, Siblings(cut));
    }

    // The row the title lives in, read from the document rather than from the handle Find
    // returns: bUnit hands back a wrapper that survives re-renders, and a wrapper is not the
    // node its own parent lists among its children.
    static AngleSharp.Dom.IElement Row(IRenderedComponent<MainSurfaceChromeHost> cut)
    {
        return Bar(cut).Find(".mud-typography").ParentElement!;
    }

    static int TitleIndex(IRenderedComponent<MainSurfaceChromeHost> cut)
    {
        var children = Row(cut).Children;

        for (var index = 0; index < children.Length; index++)
        {
            if (children[index].ClassList.Contains("mud-typography"))
            {
                return index;
            }
        }

        return -1;
    }

    static int Siblings(IRenderedComponent<MainSurfaceChromeHost> cut)
    {
        return Row(cut).Children.Length;
    }
}
