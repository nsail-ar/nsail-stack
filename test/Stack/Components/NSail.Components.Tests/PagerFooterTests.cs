// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#225, the pager row at phone width. The correction is CSS (ns-mud.css, the
/// max-width: 599.98px block) and bUnit lays nothing out, so what is pinned here is the DOM
/// shape those selectors depend on: the page-size caption reachable inside the select's own
/// box, the row counter reachable outside it, and the toolbar the height rule names — the
/// vendor is free to restyle its pager, but if it moves these the rule stops matching and
/// these tests are what says so.</summary>
public sealed class PagerFooterTests : BunitContext, IAsyncLifetime
{
    public PagerFooterTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    [Fact]
    public void ThePagerRendersInsideTheHousesOwnTable()
    {
        var cut = Render<PagerFooterHost>();

        // Every rule in the block is scoped to .ns-table, so the pager has to be a descendant
        // of the element carrying it — the same containment the stacked-mode rules rely on.
        var table = cut.Find(".ns-table");

        Assert.Single(table.QuerySelectorAll(".mud-table-pagination"));
    }

    [Fact]
    public void ThePageSizeCaptionSitsInsideTheSelectsOwnBox()
    {
        var cut = Render<PagerFooterHost>();

        // .mud-table-pagination-display .mud-table-pagination-caption is the selector that
        // drops the label at phone width — and it is the label, not the counter.
        var caption = cut.Find(".mud-table-pagination-display .mud-table-pagination-caption");

        Assert.Contains("Rows per page", caption.TextContent);
    }

    [Fact]
    public void TheRowCounterIsOutsideThatBoxAndSurvives()
    {
        var cut = Render<PagerFooterHost>();

        var display = cut.Find(".mud-table-pagination-display");
        var counter = cut.Find(".mud-table-page-number-information");

        Assert.Contains("1-10 of 18", counter.TextContent);
        Assert.Null(counter.Closest(".mud-table-pagination-display"));
        Assert.DoesNotContain(counter, display.QuerySelectorAll("*"));
    }

    [Fact]
    public void TheSizeSelectStaysBesideTheCounter()
    {
        var cut = Render<PagerFooterHost>();

        // Dropping the caption must leave the control it named: the select is a sibling of the
        // caption inside the display box, not a child of it.
        var select = cut.Find(".mud-table-pagination-select");

        Assert.NotNull(select.Closest(".mud-table-pagination-display"));
        Assert.Null(select.Closest(".mud-table-pagination-caption"));
    }

    [Fact]
    public void TheToolbarIsTheRowTheHeightRuleNames()
    {
        var cut = Render<PagerFooterHost>();

        var toolbar = cut.Find(".mud-table-pagination-toolbar");

        // Both captions, the select and the actions strip are its flex items — the boxes whose
        // shrink-proof widths add up past a phone, and the reason the row is allowed to wrap.
        Assert.Equal(2, toolbar.QuerySelectorAll(".mud-table-pagination-caption").Length);
        Assert.Single(toolbar.QuerySelectorAll(".mud-table-pagination-actions"));
    }
}
