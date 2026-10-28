// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;
using NSail.Testing.Models;

namespace NSail.Components.Tests;

/// <summary>nsail#2875, the compact phone form: an opted-in table draws each row as one line
/// under the tablet's width. bUnit lays nothing out and runs no container query, so what is
/// pinned is the DOM the compact block of ns-mud.css selects on — the table's own marker, the
/// lead, a column left off the line and a cell drawn only on it — and that a table which does
/// not opt in carries none of it.</summary>
public sealed class NsTableCompactTests : BunitContext, IAsyncLifetime
{
    public NsTableCompactTests()
    {
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
        AddAuthorization().SetAuthorized("probe");
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
    public void An_opted_in_table_carries_its_compact_marker_beside_the_stacked_one()
    {
        var cut = Render<CompactTableHost>(p => p.Add(x => x.Compact, true));

        var table = cut.Find(".ns-table");

        Assert.Contains("ns-table-compact", table.ClassList);
        Assert.Contains("mud-sm-table", table.ClassList);
    }

    [Fact]
    public void The_first_drawn_column_leads_the_compact_row()
    {
        var cut = Render<CompactTableHost>(p => p.Add(x => x.Compact, true));

        var cells = cut.FindAll("tbody td");

        Assert.Contains("ns-compact-lead", cells[0].ClassList);
        Assert.Single(cut.FindAll("tbody td.ns-compact-lead"));
    }

    [Fact]
    public void A_hidden_column_is_left_off_the_compact_row_header_and_cell()
    {
        var cut = Render<CompactTableHost>(p => p.Add(x => x.Compact, true));

        Assert.Contains("ns-compact-hidden", cut.FindAll("thead th")[1].ClassList);
        Assert.Contains("ns-compact-hidden", cut.FindAll("tbody td")[1].ClassList);
    }

    [Fact]
    public void A_cell_drawn_only_on_the_compact_row_is_marked_and_keeps_its_content()
    {
        var cut = Render<CompactTableHost>(p => p.Add(x => x.Compact, true));

        var cell = cut.Find("a.call").Closest("td")!;

        Assert.Contains("ns-compact-only", cell.ClassList);
        Assert.Equal("tel:3", cut.Find("a.call").GetAttribute("href"));
    }

    /// <summary>A row's own act that leaves the app — Call's tel: — is the tap's whole answer:
    /// the row under it opening its record as well would put the reader somewhere else when the
    /// call ends.</summary>
    [Fact]
    public async Task A_link_that_leaves_the_app_does_not_also_open_the_row()
    {
        var opened = false;

        var cut = Render<CompactTableHost>(p => p
            .Add(x => x.Compact, true)
            .Add(x => x.RowClicked, EventCallback.Factory.Create<TestModel>(this, _ => opened = true)));

        Assert.True(cut.Find("span.ns-link-leaves").HasAttribute("blazor:onclick:stopPropagation"));

        // bUnit bubbles a click the way Blazor does and stops where the flag stands: what the tap
        // reaches is no handler at all, where the row's own would otherwise answer it.
        await Assert.ThrowsAsync<MissingEventHandlerException>(() => cut.InvokeAsync(() => cut.Find("a.call").Click()));

        Assert.False(opened);

        await cut.InvokeAsync(() => cut.Find("tbody tr").Click());

        Assert.True(opened);
    }

    [Fact]
    public void A_hidden_column_stays_a_column_of_the_file()
    {
        var cut = Render<CompactTableHost>(p => p.Add(x => x.Compact, true));

        var columns = cut.FindComponent<TableColumnsProbe>().Instance.Columns!;

        Assert.Equal(2, columns.Declared.Count);
    }

    [Fact]
    public void A_table_that_does_not_opt_in_carries_no_compact_marker_but_keeps_its_only_cell_off()
    {
        var cut = Render<CompactTableHost>(p => p.Add(x => x.Compact, false));

        Assert.DoesNotContain("ns-table-compact", cut.Find(".ns-table").ClassList);
        Assert.Empty(cut.FindAll(".ns-compact-lead, .ns-compact-hidden"));

        // Drawn nowhere but on the compact row, so a table that never draws one keeps it off
        // through the same marker (ns-mud.css: .ns-table .ns-compact-only).
        Assert.Contains("ns-compact-only", cut.Find("a.call").Closest("td")!.ClassList);
    }

    [Fact]
    public void A_table_that_does_not_opt_in_renders_as_one_with_no_compact_declaration()
    {
        var plain = Render<StackedBreakpointHost>();

        Assert.DoesNotContain("compact", plain.Markup);
    }

    [Fact]
    public void The_lead_skips_a_column_left_off_the_row()
    {
        var columns = new TableColumns();

        columns.BeginPass();
        columns.Declare("Phone", NsSize.None, NsCompact.Hidden);
        columns.Declare("Name", NsSize.None, NsCompact.Shown);
        columns.Declare("Next", NsSize.None, NsCompact.Shown);

        Assert.Equal("Name", columns.Lead);
        Assert.Equal(NsCompact.Hidden, columns.CompactFor("Phone"));
        Assert.Equal(NsCompact.Shown, columns.CompactFor("Unknown"));
    }
}
