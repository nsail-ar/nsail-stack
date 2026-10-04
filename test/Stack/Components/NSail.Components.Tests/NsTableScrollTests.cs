// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

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
