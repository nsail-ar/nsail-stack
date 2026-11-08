// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A read that threw is not an answer: the grid keeps what it showed, appends nothing
/// twice, and offers the read again — it never says "no records".</summary>
public sealed class NsTableFailedReadTests : BunitContext, IAsyncLifetime
{
    public NsTableFailedReadTests()
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
    public void AFailedSecondPageAppendsNothingAndTheRetryAsksForIt()
    {
        var cut = Render<ScrollTableHost>(p => p.Add(x => x.Failing, new HashSet<int> { 2 }));

        cut.WaitForAssertion(() => Assert.Equal(25, cut.FindAll(".ns-table .mud-table-body tr.mud-table-row").Count));

        cut.Find(".ns-table-more button").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ns-table-retry")));
        Assert.Equal(25, cut.FindAll(".ns-table .mud-table-body tr.mud-table-row").Count);
        Assert.Empty(cut.FindAll(".ns-table-more"));

        cut.Find(".ns-table-retry").Click();

        cut.WaitForAssertion(() => Assert.Equal(50, cut.FindAll(".ns-table .mud-table-body tr.mud-table-row").Count));
        Assert.Contains("Row 025", cut.Markup);
        Assert.Equal(50, cut.FindAll(".ns-table .mud-table-body tr.mud-table-row td").Select(c => c.TextContent).Distinct().Count());
    }

    [Fact]
    public void AFailedFirstReadIsNotNoRecords()
    {
        var cut = Render<ScrollTableHost>(p => p.Add(x => x.Failing, new HashSet<int> { 1 }));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ns-table-retry")));
        Assert.DoesNotContain("Common.NoRecords", cut.Markup);
        Assert.Empty(cut.FindAll(".ns-table-loading-overlay"));
    }

    [Fact]
    public void AnEmptySuccessfulReadStillSaysNoRecords()
    {
        var cut = Render<ScrollTableHost>(p => p.Add(x => x.Total, 0));

        cut.WaitForAssertion(() => Assert.Contains("Common.NoRecords", cut.Markup));
        Assert.Empty(cut.FindAll(".ns-table-retry"));
    }

    [Fact]
    public void APagedFirstReadThatFailedIsNotNoRecords()
    {
        var cut = Render<ScrollTableHost>(p => p
            .Add(x => x.Mode, NsTableMode.Page)
            .Add(x => x.Yields, true)
            .Add(x => x.Failing, new HashSet<int> { 1 }));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ns-table-retry")));
        Assert.DoesNotContain("Common.NoRecords", cut.Markup);
        Assert.Equal(1, cut.Instance.Queries);
    }

    [Fact]
    public void AFailedPageKeepsThePagerOnTheShownPageWithoutAskingAgain()
    {
        var cut = Render<ScrollTableHost>(p => p
            .Add(x => x.Mode, NsTableMode.Page)
            .Add(x => x.Yields, true)
            .Add(x => x.Failing, new HashSet<int> { 2 }));

        cut.WaitForAssertion(() => Assert.Contains("Row 000", cut.Markup));

        cut.FindAll(".mud-table-pagination-actions button")[2].Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ns-table-retry")));
        Assert.Equal(2, cut.Instance.Queries);
        Assert.Contains("Row 000", cut.Markup);
    }
}
