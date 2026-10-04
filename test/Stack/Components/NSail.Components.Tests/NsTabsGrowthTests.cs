// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>Proves NsTabs' panel wrapper inherits the honest scroll from the surface hosting
/// it. The wrapper used to declare "flex-fill min-h-0 overflow-y-auto" unconditionally: inside
/// a host that hands down no definite height (NsCard Grow=false — the page's own stack owns
/// the one scrollbar) a flex-basis-0 overflow box has nothing to resolve against, so the tab's
/// content was compressed and painted outside the card instead of taking its natural
/// height (Mi Perfil's Accounts tab, Emmanuel 2026-08-11).</summary>
public sealed class NsTabsGrowthTests : BunitContext
{
    public NsTabsGrowthTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>The defect, at the seam: hosted by a card that takes its natural height, the
    /// panels container claims neither the growth nor the scroll, and the tabs root does not
    /// ask its parent to stretch it.</summary>
    [Fact]
    public void TabsInAHostThatDoesNotGrow_ClaimNoScrollOfTheirOwn()
    {
        var cut = Render<TabsGrowthHost>(p => p.Add(x => x.CardGrows, false));

        var panels = cut.Find(".mud-tabs-panels").GetAttribute("class")!;

        Assert.DoesNotContain("overflow-y-auto", panels);
        Assert.DoesNotContain("flex-fill", panels);

        var tabs = cut.Find(".ns-tabs").GetAttribute("class")!;

        Assert.DoesNotContain("flex-1", tabs);

        // The four stacked blocks are all still rendered — the content is there to be shown
        // at its natural height, which is exactly what the compressed box was hiding.
        Assert.Equal(4, cut.FindAll(".account-block").Count);
    }

    /// <summary>The card that reported it (Mi Perfil, Identidad tab). A non-growing card takes
    /// its natural height and its page's own stack owns the scroll — so the card must carry no
    /// min-height:0: that class removes the content floor a flex item defaults to
    /// (min-height:auto), which is the only thing stopping a scrolling parent from
    /// flex-shrinking the card below its own content. Shrunk, the card's overflow-visible tab
    /// fields (DNI/Sexo/CUIT/Fecha) paint outside its border, over the page background, with no
    /// scrollbar anywhere to say so. min-h-0 rides with overflow-y-auto and never alone.</summary>
    [Fact]
    public void ANonGrowingCard_CarriesNoMinHeightZero_SoAScrollingParentCannotShrinkItBelowItsContent()
    {
        var cut = Render<TabsGrowthHost>(p => p.Add(x => x.CardGrows, false));

        Assert.DoesNotContain("min-h-0", cut.Find(".mud-card").GetAttribute("class")!);
        Assert.DoesNotContain("min-h-0", cut.Find(".ns-tabs").GetAttribute("class")!);

        // The tabs live INSIDE the card's content — a descendant of it, never a sibling
        // floating on the page. This is the structural half of "the card contains its tabs".
        var content = cut.Find(".mud-card-content");
        Assert.NotNull(content.QuerySelector(".mud-tabs-panels"));
        Assert.NotNull(content.QuerySelector(".account-block"));
    }

    /// <summary>The other half of the same inheritance: a growing card still hands the panel
    /// the definite height its scroll needs, so nothing regresses for the pages that pair
    /// tabs with a surface-filling host.</summary>
    [Fact]
    public void TabsInAGrowingCard_KeepTheirOwnScroll()
    {
        var cut = Render<TabsGrowthHost>(p => p.Add(x => x.CardGrows, true));

        var panels = cut.Find(".mud-tabs-panels").GetAttribute("class")!;

        Assert.Contains("overflow-y-auto", panels);
        Assert.Contains("flex-fill", panels);
        Assert.Contains("min-h-0", panels);

        // The growing branch keeps its min-height:0: the card is flex:1 1 0% and must shrink to
        // its bounded parent so its own content scrolls internally — min-h-0 is the pair of that
        // overflow, and dropping it in the non-growing branch never touches this one.
        Assert.Contains("min-h-0", cut.Find(".mud-card").GetAttribute("class")!);

        var tabs = cut.Find(".ns-tabs").GetAttribute("class")!;
        Assert.Contains("flex-1", tabs);
        Assert.Contains("min-h-0", tabs);
    }

    /// <summary>NsPanel's content always grows and always owns a scroll, so every tabbed page
    /// that sits in one (Parties, Products, Register Client) reads the same as before.</summary>
    [Fact]
    public void TabsInAPanel_KeepTheirOwnScroll()
    {
        var cut = Render<PanelTabsHost>();

        var panels = cut.Find(".mud-tabs-panels").GetAttribute("class")!;

        Assert.Contains("overflow-y-auto", panels);
        Assert.Contains("flex-1", cut.Find(".ns-tabs").GetAttribute("class")!);
    }

    /// <summary>The call site still wins: a tabs element that says it does not grow keeps the
    /// documented escape hatch even where the host would hand it a height (EyesEditor sits
    /// among other sections inside a growing panel).</summary>
    [Fact]
    public void TabsDeclaringNoGrowth_OverrideAGrowingHost()
    {
        var cut = Render<ExplicitTabsGrowthHost>(p => p.Add(x => x.Grow, false));

        var panels = cut.Find(".mud-tabs-panels").GetAttribute("class")!;

        Assert.DoesNotContain("overflow-y-auto", panels);
        Assert.DoesNotContain("flex-1", cut.Find(".ns-tabs").GetAttribute("class")!);
    }
}
