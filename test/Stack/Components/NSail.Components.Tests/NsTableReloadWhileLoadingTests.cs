// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>Two reads of one page-mode grid that overlap. The new query supersedes the one in
/// flight rather than being refused by a Runner already running it — the refusal the vendor
/// swallowed, which left the grid showing rows the search excludes (found on the price list
/// sheet, where adding a price reloads the grid and the search follows at once) — and the
/// superseded read changes nothing when it answers last, which is the window a watch's own push
/// opens with nobody's hand in it.</summary>
public sealed class NsTableReloadWhileLoadingTests : BunitContext, IAsyncLifetime
{
    public NsTableReloadWhileLoadingTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>())]));
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
    public async Task A_query_asked_while_a_page_is_loading_is_run_rather_than_refused()
    {
        var gate = new TaskCompletionSource();

        var cut = Render<ReloadWhileLoadingHost>(p => p.Add(x => x.Gate, gate));

        cut.WaitForAssertion(() => Assert.Equal(1, cut.Instance.Queries));

        await cut.InvokeAsync(() => cut.Instance.Table.Reload(first: true));

        Assert.Equal(2, cut.Instance.Queries);

        gate.SetResult();
    }

    [Fact]
    public async Task A_superseded_read_answering_last_changes_neither_the_rows_the_total_nor_the_page()
    {
        var gate = new TaskCompletionSource();

        // The gate holds the THIRD query: the first two put the pager on the second page, the
        // third is the read a reload supersedes, and it is released only after that reload has
        // already answered.
        var cut = Render<ReloadWhileLoadingHost>(p => p
            .Add(x => x.Gate, gate)
            .Add(x => x.Held, 3));

        cut.WaitForAssertion(() => Assert.Equal("1-10 of 30", Counter(cut)));

        cut.Find("[aria-label='Next page']").Click();

        cut.WaitForAssertion(() => Assert.Equal("11-20 of 30", Counter(cut)));

        var superseded = cut.InvokeAsync(() => cut.Instance.Table.Reload());

        await cut.InvokeAsync(() => cut.Instance.Table.Reload());

        gate.SetResult();

        // MudTable assigns whatever ServerData hands it before the reload that pulled it
        // completes, so the superseded answer has landed — or been dropped — by the next line.
        await superseded;

        // The counter is the rows, the total and the page in one read: the superseded answer
        // winning prints "1-1 of 1", its own single row on a pager the vendor sent back to the
        // first page because that answer's total could not hold the second.
        Assert.Equal("11-20 of 30", Counter(cut));
        Assert.DoesNotContain("Superseded 1", cut.Markup);

        // Still a grid, and still drawing: a superseded answer that reached NsErrorBoundary
        // would have taken the whole screen with it.
        Assert.Equal(10, cut.FindAll(".ns-table tbody tr.mud-table-row").Count);
    }

    static string Counter(IRenderedComponent<ReloadWhileLoadingHost> cut)
    {
        return cut.Find(".mud-table-page-number-information").TextContent.Trim();
    }
}
