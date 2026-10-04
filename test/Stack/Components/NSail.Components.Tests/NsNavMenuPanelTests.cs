// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

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

[Route("/navmenu/probe")]
public sealed class NavMenuProbePage : ComponentBase;

[Route("/navmenu/other")]
public sealed class NavMenuOtherProbePage : ComponentBase;

/// <summary>Leonardo, 2026-08-10 with a screenshot of the drawer — "Escribí para buscar" scrolled
/// half out of sight above the menu — and his own diagnosis, "¿falta un NsPanel?". The search box
/// and the menu were flat siblings, so the drawer's own content region scrolled the pair as one
/// thing and the field a user is typing into left the screen. The drawer now holds the house's
/// panel anatomy: a header that stays, a content region that is the only marked scroller, and a
/// footer slot pinned at the foot. The footer is deliberately EMPTY today (his ruling: the slot
/// exists, nothing goes in it yet), which is why it is asserted empty rather than left unasserted.</summary>
public sealed class NsNavMenuPanelTests : BunitContext
{
    void Setup()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsNavMenuPanelTests).Assembly, []));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<INavMenuContributor>(new FixedNavContributor(
            new NavMenuItem { Name = "Parties", PageType = typeof(NavMenuProbePage) },
            new NavMenuItem { Name = "Products", PageType = typeof(NavMenuOtherProbePage) }));
        JSInterop.Mode = JSRuntimeMode.Loose;

        this.AddAuthorization();
    }

    IRenderedComponent<NsNavMenu> RenderMenu()
    {
        Setup();

        return Render<NsNavMenu>();
    }

    IRenderedComponent<NsNavMenu> RenderMenuWithBrand()
    {
        Setup();

        return Render<NsNavMenu>(p => p.Add(x => x.Brand, BrandProbe()));
    }

    static RenderFragment BrandProbe()
    {
        return builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "probe-brand");
            builder.AddContent(2, "Óptica Demo");
            builder.CloseElement();
        };
    }

    /// <summary>The whole defect in one assertion: the field is outside the box that scrolls.</summary>
    [Fact]
    public void TheSearchFieldSitsOutsideTheScrollingRegion()
    {
        var cut = RenderMenu();

        Assert.NotEmpty(cut.FindAll("input"));
        Assert.Empty(Scroller(cut).QuerySelectorAll("input"));
    }

    [Fact]
    public void TheMenuItselfSitsInsideTheScrollingRegion()
    {
        var cut = RenderMenu();

        Assert.NotEmpty(Scroller(cut).QuerySelectorAll(".mud-navmenu"));
        Assert.NotEmpty(Scroller(cut).QuerySelectorAll("a"));
    }

    /// <summary>Only what is marked scrolls: the panel's content region is the one owner, and it
    /// carries the min-h-0 link of the chain the shell hands down (a5a7997c's 100dvh precedent
    /// reaches the drawer the same as everything else).</summary>
    [Fact]
    public void TheScrollingRegionIsTheOneMarkedOwner()
    {
        var cut = RenderMenu();

        var scroller = Scroller(cut);

        Assert.Contains("overflow-y-auto", scroller.ClassList);
        Assert.Contains("min-h-0", scroller.ClassList);
    }

    /// <summary>Leonardo's ruling, 2026-08-10: the footer slot exists so the thing that lands
    /// there later has a fixed place — "por ahora no pongamos nada".</summary>
    [Fact]
    public void TheFooterSlotIsRenderedAndEmpty()
    {
        var cut = RenderMenu();

        var footer = cut.Find(".ns-panel-footer");

        Assert.Empty(footer.Children);
        Assert.Equal(string.Empty, footer.TextContent.Trim());
    }

    /// <summary>nsail#199: the drawer leg of "the brand keeps the splash and the drawer" — no
    /// Brand wired, nothing renders where it would go.</summary>
    [Fact]
    public void WithNoBrandWiredNothingRendersInItsPlace()
    {
        var cut = RenderMenu();

        Assert.Empty(cut.FindAll(".probe-brand"));
    }

    /// <summary>The drawer owns the brand, always (Leonardo, nsail#378) — it takes it in the
    /// header rather than the scroller, with no dependency on what any page announced.</summary>
    [Fact]
    public void TheBrandAlwaysRendersInTheHeader()
    {
        var cut = RenderMenuWithBrand();

        var brand = cut.Find(".probe-brand");

        Assert.Equal("Óptica Demo", brand.TextContent);
        Assert.Empty(Scroller(cut).QuerySelectorAll(".probe-brand"));
    }

    static AngleSharp.Dom.IElement Scroller(IRenderedComponent<NsNavMenu> cut)
    {
        return cut.Find(".ns-scroll-shadow");
    }
}
