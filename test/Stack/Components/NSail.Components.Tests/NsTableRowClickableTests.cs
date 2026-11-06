// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;
using NSail.Testing.Models;

namespace NSail.Components.Tests;

/// <summary>nsail#1904 — a row looks clickable only where clicking it does something. MudTable
/// marks a row <c>mud-table-row-clickable</c> — the vendor's own <c>cursor:pointer</c> — from the
/// mere presence of an <c>OnRowClick</c> delegate, so handing it a method of NsTable's own put the
/// pointer on every grid in the house while only a handful answer a row click. What the
/// absence costs is nothing a row needs: the line between two rows and the hover tone are the
/// table's and stay (hosts.md), which is why these pin the clickable class rather than the hover
/// one. Both sides are real DOM in all three of NsTable's modes.</summary>
public sealed class NsTableRowClickableTests : BunitContext, IAsyncLifetime
{
    public NsTableRowClickableTests()
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

    [Theory]
    [InlineData(NsTableMode.Scroll)]
    [InlineData(NsTableMode.Page)]
    public void AQueriedTableWithNoRowClickDrawsNoClickableRow(NsTableMode mode)
    {
        var cut = Render<RowClickTableHost>(parameters => parameters.Add(p => p.Mode, mode));

        Assert.Equal(2, Rows(cut).Count);
        Assert.Empty(cut.FindAll(".mud-table-row-clickable"));
        Assert.Empty(cut.FindAll(".ns-table .mud-table-body tr.cursor-pointer"));
    }

    // The third mode: rows in hand, no OnQuery, so neither queried branch renders.
    [Fact]
    public void AnInMemoryTableWithNoRowClickDrawsNoClickableRow()
    {
        var cut = Render<RowClickTableHost>(parameters => parameters.Add(p => p.Queried, false));

        Assert.Equal(2, Rows(cut).Count);
        Assert.Empty(cut.FindAll(".mud-table-row-clickable"));
        Assert.Empty(cut.FindAll(".ns-table .mud-table-body tr.cursor-pointer"));
    }

    [Theory]
    [InlineData(NsTableMode.Scroll)]
    [InlineData(NsTableMode.Page)]
    public async Task AQueriedTableWithARowClickMarksEveryRowAndOpensTheOneClicked(NsTableMode mode)
    {
        TestModel? clicked = null;

        var cut = Render<RowClickTableHost>(parameters => parameters
            .Add(p => p.Mode, mode)
            .Add(p => p.RowClicked, EventCallback.Factory.Create<TestModel>(this, row => clicked = row)));

        var rows = Rows(cut);

        Assert.Equal(2, rows.Count);
        Assert.Equal(2, cut.FindAll(".mud-table-row-clickable").Count);
        Assert.Equal(2, cut.FindAll(".ns-table .mud-table-body tr.cursor-pointer").Count);

        await cut.InvokeAsync(() => rows[0].Click());

        Assert.NotNull(clicked);
        Assert.Equal("Ana", clicked!.Name);
    }

    [Fact]
    public async Task AnInMemoryTableWithARowClickMarksEveryRowAndOpensTheOneClicked()
    {
        TestModel? clicked = null;

        var cut = Render<RowClickTableHost>(parameters => parameters
            .Add(p => p.Queried, false)
            .Add(p => p.RowClicked, EventCallback.Factory.Create<TestModel>(this, row => clicked = row)));

        var rows = Rows(cut);

        Assert.Equal(2, rows.Count);
        Assert.Equal(2, cut.FindAll(".mud-table-row-clickable").Count);
        Assert.Equal(2, cut.FindAll(".ns-table .mud-table-body tr.cursor-pointer").Count);

        await cut.InvokeAsync(() => rows[1].Click());

        Assert.NotNull(clicked);
        Assert.Equal("Beto", clicked!.Name);
    }

    // Hover is the table's, not the row's, and it is one of the two things that tell two rows
    // apart (hosts.md) — so it rides every grid whether or not the rows answer a click.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheHoverToneStaysOnAGridThatAnswersNoClick(bool queried)
    {
        var cut = Render<RowClickTableHost>(parameters => parameters.Add(p => p.Queried, queried));

        Assert.Single(cut.FindAll(".ns-table.mud-table-hover"));
    }

    // What makes the absent class equal a plain arrow: in the shipped vendor stylesheet the
    // clickable class is the ONLY rule that hands a body row cursor:pointer — the sort label's
    // and the pager's are chrome, not rows. Read verbatim, so a vendor upgrade that grows a
    // second source cannot pass the markup pins above in silence.
    [Fact]
    public void TheClickableClassIsTheOnlyThingThatPointsAtARow()
    {
        var rules = Regex
            .Matches(VendorStylesheet.Read(), @"([^{}]*)\{[^{}]*cursor:\s*pointer[^{}]*\}")
            .Select(match => match.Groups[1].Value.Trim())
            .Where(selector => selector.Contains("mud-table-row", StringComparison.Ordinal))
            .ToList();

        Assert.Equal([".mud-table-row-clickable"], rules);
    }

    /// <summary>Known red (keyboard.md, Known reds): a clickable row is a bare tr with a click
    /// handler, so Tab does not reach it and Enter does not open it. This pins what is drawn
    /// today: the day a row gains a tab stop, this fails and the row goes.</summary>
    [Fact]
    public void AClickableRowIsNotATabStopYet()
    {
        var cut = Render<RowClickTableHost>(parameters => parameters
            .Add(p => p.RowClicked, EventCallback.Factory.Create<TestModel>(this, _ => { })));

        Assert.All(Rows(cut), row => Assert.Null(row.GetAttribute("tabindex")));
    }

    static IReadOnlyList<IElement> Rows(IRenderedComponent<RowClickTableHost> cut)
    {
        return cut.FindAll(".ns-table .mud-table-body tr");
    }
}
