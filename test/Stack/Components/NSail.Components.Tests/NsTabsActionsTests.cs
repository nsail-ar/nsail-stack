// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#1267: what a strip's Actions slot holds is an act ON the tabs, not a third
/// tab. Handed to the vendor's header bare it became a flex item of the toolbar and was drawn
/// as tall as the strip (Leonardo: "queda feo asi ocupando toda la altura del tab"), so NsTabs
/// gives it a box of its own — the hook ns-mud.css insets it by. The height itself is a
/// geometry only a browser settles (NuevaVentaFourthPassTests); what is held here is that the
/// box exists, holds the act, and is not drawn for a strip that has no acts.</summary>
public sealed class NsTabsActionsTests : BunitContext
{
    public NsTabsActionsTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void AStripsActs_AreDrawnInsideTheirOwnBox()
    {
        var cut = Render<TabsActionsHost>();

        var box = cut.Find(".ns-tabs-actions");

        Assert.NotNull(box.QuerySelector(".strip-act"));
    }

    [Fact]
    public void AStripWithNoActs_DrawsNoBoxAtAll()
    {
        var cut = Render<TabsGrowthHost>(p => p.Add(x => x.CardGrows, false));

        Assert.Empty(cut.FindAll(".ns-tabs-actions"));
    }
}
