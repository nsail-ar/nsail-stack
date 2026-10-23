// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#605, the scroll branch that took the whole screen down on a phone. Two facts
/// are pinned: the branch renders every accumulated row (it carries no pager, so nothing may
/// slice the list it was handed), and neither of its two interop paths — the scroll listener
/// and the viewport subscription — may let an exception out of OnAfterRenderAsync, where
/// NsErrorBoundary would take @Body and the page header with it.</summary>
public sealed class NsTableScrollTests : BunitContext, IAsyncLifetime
{
    public NsTableScrollTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();
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
    public void TheScrollBranchCarriesNoPagerAndStandsOnItsOwn()
    {
        var cut = Render<ScrollTableHost>();

        // No PagerContent is what stops MudTable slicing Items by RowsPerPage at all
        // (CurrentPageItems returns FilteredItems whole when PagerContent is null), and it is
        // the DOM fact ScrollModeTests reads: no .mud-table-pagination at phone width.
        Assert.Empty(cut.FindAll(".ns-table .mud-table-pagination"));
        Assert.NotEmpty(cut.FindAll(".ns-table"));
    }

    // Which branch rendered is what ns-mud.css needs in order to release the container's own
    // scroll on a stacked PAGED grid and not on a scrolling one, where that same container is
    // what the next page is asked from (nsail#2595). The vendor's stacking class rides the
    // viewport and is no help off a browser; this half is the component's and is readable here.
    // One host per test, not two in one: the host mounts MudPopoverProvider, whose SectionOutlet
    // takes a registry subscription under one identifier — a second one in the same render tree
    // throws out of SetParametersAsync before any assertion is reached.
    [Fact]
    public void ThePagedBranchSaysSoOnItsRoot()
    {
        var cut = Render<ScrollTableHost>(parameters => parameters.Add(p => p.Mode, NsTableMode.Page));

        Assert.NotEmpty(cut.FindAll(".ns-table.ns-table-pages"));
        Assert.NotEmpty(cut.FindAll(".ns-table .mud-table-pagination"));
    }

    [Fact]
    public void TheScrollBranchDoesNot()
    {
        var cut = Render<ScrollTableHost>();

        Assert.Empty(cut.FindAll(".ns-table.ns-table-pages"));
        Assert.NotEmpty(cut.FindAll(".ns-table"));
    }

    // The other half of the release, and the half the C# class cannot carry: the WIDTH. The
    // vendor's mud-sm-table is on the root at every width (Breakpoint="Sm" — StackedColumnBreakpointTests
    // reads it off bUnit's own markup, where there is no viewport at all), so what the rule means
    // by "stacked" is the query it sits in and nothing else; outside it a paged DESKTOP grid
    // wears both hooks and loses the container scroll its pinned pager stands on. The release is
    // that ONE declaration: the container's own overflow stays, because a box whose height is its
    // rows' has nothing to scroll and the two axes cannot be set apart (measured, nsail#2595).
    // Read off the file the app ships, like NsTablePagerShrinkTests, since bUnit paints nothing.
    [Fact]
    public void TheStylesheetReleasesTheStackedPagedGridOnlyUnderTheVendorsStackingQuery()
    {
        var rules = StackedPagedRules(HouseStylesheet.Read());

        var rule = Assert.Single(rules);

        Assert.Equal("@media (max-width: 960px)", rule.Query);
        Assert.Contains("min-height: auto;", rule.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("overflow", rule.Body, StringComparison.Ordinal);
    }

    /// <summary>Every rule whose selector names the stacked paged grid, each with the at-rule it
    /// is nested in — empty for one written at the top level.</summary>
    static List<(string Query, string Body)> StackedPagedRules(string css)
    {
        const string selector = ".ns-table.ns-table-pages.mud-sm-table";

        var stripped = Regex.Replace(css, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        var found = new List<(string Query, string Body)>();
        var open = new List<(string Prelude, int Body, bool Wanted)>();
        var start = 0;

        for (var i = 0; i < stripped.Length; i++)
        {
            if (stripped[i] == '{')
            {
                var prelude = stripped[start..i].Trim();

                open.Add((prelude, i + 1, prelude.Contains(selector, StringComparison.Ordinal)));
                start = i + 1;
            }
            else if (stripped[i] == '}')
            {
                Assert.NotEmpty(open);

                var block = open[^1];
                open.RemoveAt(open.Count - 1);

                if (block.Wanted)
                {
                    found.Add((open.Count == 0 ? string.Empty : open[^1].Prelude, stripped[block.Body..i]));
                }

                start = i + 1;
            }
            else if (stripped[i] == ';')
            {
                start = i + 1;
            }
        }

        Assert.Empty(open);

        return found;
    }

    [Fact]
    public void AScrollListenerThatThrowsLeavesTheTableStanding()
    {
        Services.AddScoped<IScrollListenerFactory, ThrowingScrollListenerFactory>();

        var cut = Render<ScrollTableHost>();

        // The table is still there. bUnit reaches StartScrollListener only when the branch has
        // accumulated a row, which it does not here (no browser, no query continuation), so
        // what this pins is the render surviving a hostile factory in the tree — the guard
        // itself is read in the file, and on a phone by ScrollModeTests.
        Assert.NotEmpty(cut.FindAll(".ns-table"));
    }

    [Fact]
    public void AViewportServiceThatThrowsLeavesTheTableStanding()
    {
        Services.AddScoped<IBrowserViewportService, ThrowingViewportService>();

        var cut = Render<ScrollTableHost>(parameters => parameters.Add(p => p.Mode, NsTableMode.Auto));

        // A viewport nobody can measure costs the measurement and nothing else: the branch was
        // decided by the device seam before any of this, so the pager is there and so is the
        // table, which is what "degrades" has to mean.
        Assert.NotEmpty(cut.FindAll(".ns-table .mud-table-pagination"));
        Assert.NotEmpty(cut.FindAll(".ns-table .mud-table-body tr"));
    }

    sealed class ThrowingScrollListenerFactory : IScrollListenerFactory
    {
        public IScrollListener Create(string? selector)
        {
            throw new InvalidOperationException("the selector resolved to nothing");
        }

        public IScrollListener Create(string? selector, int reportRateMs)
        {
            throw new InvalidOperationException("the selector resolved to nothing");
        }
    }

    sealed class ThrowingViewportService : IBrowserViewportService
    {
        public ResizeOptions ResizeOptions
        {
            get { return new ResizeOptions(); }
        }

        public Task SubscribeAsync(IBrowserViewportObserver observer, bool fireImmediately = true)
        {
            throw new InvalidOperationException("no browser to measure");
        }

        public Task SubscribeAsync(Guid observerId, Action<BrowserViewportEventArgs> lambda, ResizeOptions? options = null, bool fireImmediately = true)
        {
            throw new InvalidOperationException("no browser to measure");
        }

        public Task SubscribeAsync(Guid observerId, Func<BrowserViewportEventArgs, Task> lambda, ResizeOptions? options = null, bool fireImmediately = true)
        {
            throw new InvalidOperationException("no browser to measure");
        }

        public Task UnsubscribeAsync(IBrowserViewportObserver observer)
        {
            return Task.CompletedTask;
        }

        public Task UnsubscribeAsync(Guid observerId)
        {
            return Task.CompletedTask;
        }

        public Task<bool> IsMediaQueryMatchAsync(string mediaQuery)
        {
            return Task.FromResult(false);
        }

        public Task<bool> IsBreakpointWithinReferenceSizeAsync(Breakpoint breakpoint, Breakpoint reference)
        {
            return Task.FromResult(false);
        }

        public Task<bool> IsBreakpointWithinWindowSizeAsync(Breakpoint breakpoint)
        {
            return Task.FromResult(false);
        }

        public Task<Breakpoint> GetCurrentBreakpointAsync()
        {
            throw new InvalidOperationException("no browser to measure");
        }

        public Task<BrowserWindowSize> GetCurrentBrowserWindowSizeAsync()
        {
            throw new InvalidOperationException("no browser to measure");
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
