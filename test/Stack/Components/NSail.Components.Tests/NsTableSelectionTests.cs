// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;
using NSail.Paging;
using static NSail.Components.Tests.Fixtures.SelectionTableHost;

namespace NSail.Components.Tests;

/// <summary>A grid's ticks (the price list's Eliminar and Actualizar act on them): kept by key so
/// a reloaded page keeps them, a header tick for the rows in hand, and — once those are all
/// ticked and the list holds more — the one offer to reach every row the query answers, said
/// out loud so an act over "all" never quietly means "the ones on screen".</summary>
public sealed class NsTableSelectionTests : BunitContext, IAsyncLifetime
{
    public NsTableSelectionTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.Selected"] = "{0} selected",
            ["Common.Selected.Plural"] = "{0} selected",
            ["Common.SelectAll.Plural"] = "Select all {0}",
            ["Common.ClearSelection"] = "Clear selection",
        })]));
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

    static List<SelectableRow> Three()
    {
        return [new(Guid.NewGuid(), "uno"), new(Guid.NewGuid(), "dos"), new(Guid.NewGuid(), "tres")];
    }

    [Fact]
    public void Nothing_ticked_draws_no_bar()
    {
        var cut = Render<SelectionTableHost>(p => p
            .Add(x => x.Rows, Three())
            .Add(x => x.Selection, new Selection()));

        Assert.Empty(cut.FindAll(".ns-selection-bar"));
    }

    [Fact]
    public async Task A_row_s_tick_selects_it_by_key_and_says_how_many()
    {
        var rows = Three();
        var selection = new Selection();

        var cut = Render<SelectionTableHost>(p => p
            .Add(x => x.Rows, rows)
            .Add(x => x.Selection, selection));

        await cut.InvokeAsync(() => cut.FindAll("tbody input[type=checkbox]")[1].Change(true));

        Assert.Equal([rows[1].Id], selection.Ids);
        Assert.False(selection.All);
        Assert.Contains("1 selected", cut.Find(".ns-selection-bar").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_header_tick_selects_every_row_in_hand_and_a_second_one_clears_them()
    {
        var rows = Three();
        var selection = new Selection();

        var cut = Render<SelectionTableHost>(p => p
            .Add(x => x.Rows, rows)
            .Add(x => x.Selection, selection));

        await cut.InvokeAsync(() => cut.Find("thead input[type=checkbox]").Change(true));

        Assert.Equal(rows.Select(r => r.Id), selection.Ids);

        await cut.InvokeAsync(() => cut.Find("thead input[type=checkbox]").Change(false));

        Assert.True(selection.IsEmpty);
    }

    // Every row in hand ticked, and the query answers more than are in hand: the offer to reach
    // them all, and taking it is what the act will carry — All, not a longer list of ids.
    [Fact]
    public async Task A_full_page_of_ticks_offers_every_row_the_query_answers()
    {
        var selection = new Selection();

        var cut = Render<SelectionTableHost>(p => p
            .Add(x => x.Rows, Three())
            .Add(x => x.Total, 312)
            .Add(x => x.Selection, selection));

        await cut.InvokeAsync(() => cut.Find("thead input[type=checkbox]").Change(true));

        var offer = cut.FindAll(".ns-selection-bar button").Single(b => b.TextContent.Contains("Select all 312", StringComparison.Ordinal));

        await cut.InvokeAsync(() => offer.Click());

        Assert.True(selection.All);
        Assert.Empty(selection.Ids);
        Assert.Contains("312 selected", cut.Find(".ns-selection-bar").TextContent, StringComparison.Ordinal);
    }

    // The list holds nothing past the rows in hand, so there is nothing further to offer.
    [Fact]
    public async Task A_full_page_that_is_the_whole_list_offers_nothing_more()
    {
        var cut = Render<SelectionTableHost>(p => p
            .Add(x => x.Rows, Three())
            .Add(x => x.Selection, new Selection()));

        await cut.InvokeAsync(() => cut.Find("thead input[type=checkbox]").Change(true));

        Assert.DoesNotContain(cut.FindAll(".ns-selection-bar button"), b => b.TextContent.Contains("Select all", StringComparison.Ordinal));
    }

    // One row out of "all" is no longer all: what stays ticked is what the user can see ticked.
    [Fact]
    public async Task Unticking_one_row_out_of_all_keeps_the_rest_in_hand()
    {
        var rows = Three();
        var selection = new Selection { All = true };

        var cut = Render<SelectionTableHost>(p => p
            .Add(x => x.Rows, rows)
            .Add(x => x.Total, 312)
            .Add(x => x.Selection, selection));

        await cut.InvokeAsync(() => cut.FindAll("tbody input[type=checkbox]")[0].Change(false));

        Assert.False(selection.All);
        Assert.Equal([rows[1].Id, rows[2].Id], selection.Ids);
    }

    [Fact]
    public async Task Clearing_empties_the_selection_and_the_bar_goes()
    {
        var selection = new Selection();

        var cut = Render<SelectionTableHost>(p => p
            .Add(x => x.Rows, Three())
            .Add(x => x.Selection, selection));

        await cut.InvokeAsync(() => cut.FindAll("tbody input[type=checkbox]")[0].Change(true));
        await cut.InvokeAsync(() => cut.FindAll(".ns-selection-bar button").Single(b => b.TextContent.Contains("Clear selection", StringComparison.Ordinal)).Click());

        Assert.True(selection.IsEmpty);
        Assert.Empty(cut.FindAll(".ns-selection-bar"));
    }
}
