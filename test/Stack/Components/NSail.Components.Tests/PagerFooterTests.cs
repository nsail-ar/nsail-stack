// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The pager row compacts by its own width — on a phone and in an aside on a wide
/// screen alike. The correction is CSS (ns-mud.css: the toolbar is the container, and its
/// max-width: 599.98px query drops the page-size caption and the first/last arrows) and bUnit
/// lays nothing out, so what is pinned here is the DOM shape those selectors depend on: the
/// page-size caption reachable inside the select's own box, the row counter reachable outside
/// it, the arrows inside the toolbar that measures them, and the rule itself read off the file
/// — the vendor is free to restyle its pager, but if it moves these the rule stops matching and
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

    /// <summary>Four arrows, and the first and last are what the compacted row drops: previous and
    /// next stay, which is what a page is walked with. Both sit inside the toolbar, the box the
    /// query measures.</summary>
    [Fact]
    public void TheArrowsTheCompactedRowDropsAreTheStripsFirstAndLast()
    {
        var cut = Render<PagerFooterHost>();

        var toolbar = cut.Find(".mud-table-pagination-toolbar");
        var arrows = toolbar.QuerySelectorAll(".mud-table-pagination-actions > .mud-button-root");

        Assert.Equal(4, arrows.Length);
        Assert.NotNull(toolbar.QuerySelector(".mud-table-pagination-display .mud-table-pagination-caption"));
    }

    /// <summary>The arrows are icon-only vendor buttons, so the whisper reaches them by a selector
    /// of their shape (ns.js's WHISPER_SELECTOR) and says their accessible name: pinned here so a
    /// vendor upgrade that moves them out of that shape reddens CI rather than taking the bubble
    /// away in silence. On page 1 of two, first and previous are disabled — the vendor drops their
    /// hit testing and Tab skips them, so they open nothing — and next and last are live.</summary>
    [Fact]
    public void TheArrowsAreTheNamedFaceTheWhisperIsKeyedOn()
    {
        var cut = Render<PagerFooterHost>();

        var arrows = cut.FindAll(PagerArrow);

        Assert.Equal(4, arrows.Count);
        Assert.All(arrows, arrow => Assert.Equal("BUTTON", arrow.NodeName));
        Assert.All(arrows, arrow => Assert.False(string.IsNullOrWhiteSpace(arrow.GetAttribute("aria-label"))));
        Assert.Equal([true, true, false, false], arrows.Select(arrow => arrow.HasAttribute("disabled")));

        var script = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "src/Stack/BaseServices/NSail.BaseServices.WebApp/wwwroot/ns.js"));

        var selector = Regex.Match(script, @"const WHISPER_SELECTOR = '(?<list>[^']*)';").Groups["list"].Value;
        Assert.Contains(PagerArrow, selector.Split(", "));
    }

    const string PagerArrow = ".ns-table .mud-table-pagination-actions .mud-icon-button[aria-label]:not([aria-label=\"\"])";

    static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"NSail.sln was not found above {AppContext.BaseDirectory}.");
    }

    /// <summary>A window query would compact the phone's pager and leave an aside's wrapping on a
    /// wide screen: the toolbar is the container and the compaction asks it, never the
    /// viewport.</summary>
    [Fact]
    public void TheStylesheetCompactsThePagerByTheToolbarsOwnWidth()
    {
        var css = HouseStylesheet.Read();

        var container = Rule(css, ".ns-table .mud-table-pagination .mud-table-pagination-toolbar {");
        Assert.Contains("container-type: inline-size;", container, StringComparison.Ordinal);

        var query = css.IndexOf("@container (max-width: 599.98px) {\n    .ns-table .mud-table-pagination .mud-table-pagination-display .mud-table-pagination-caption,", StringComparison.Ordinal);
        Assert.True(query >= 0, "the pager's compaction was not found under a container query.");

        var block = css[query..css.IndexOf('}', query)];
        Assert.Contains(".mud-table-pagination-actions .mud-button-root:first-child,", block, StringComparison.Ordinal);
        Assert.Contains(".mud-table-pagination-actions .mud-button-root:last-child {", block, StringComparison.Ordinal);
        Assert.Contains("display: none;", block, StringComparison.Ordinal);

        Assert.DoesNotContain("@media (max-width: 599.98px) {\n    .ns-table .mud-table-pagination", css, StringComparison.Ordinal);
    }

    static string Rule(string css, string selector)
    {
        var start = css.IndexOf(selector, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{selector}' was not found in the stylesheet.");

        return css[start..css.IndexOf('}', start)];
    }
}
