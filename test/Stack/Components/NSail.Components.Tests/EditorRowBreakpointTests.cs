// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;
using NSail.Testing.Models;

namespace NSail.Components.Tests;

/// <summary>A read column may hide by breakpoint; an edit input never (nsail#480 — a Work Order
/// line opened on a phone showed Producto and a Total frozen at 0,00, because Cantidad and
/// Precio are Breakpoint="Sm" columns and the editor's cells inherited the read column's
/// declaration). Hiding an input does not shrink the screen, it removes the use case. bUnit lays
/// nothing out and runs no container query, so what is pinned here is the DOM the CSS keys on:
/// the hide hooks reaching a read cell and never reaching a cell of the row open for editing —
/// in column mode and, since the same class is the stacked-mode selector's (ns-mud.css), in the
/// card too.</summary>
public sealed class EditorRowBreakpointTests : BunitContext, IAsyncLifetime
{
    public EditorRowBreakpointTests()
    {
        var metadata = new MetadataProvider();

        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            [metadata.KeyFor(typeof(TestModel), nameof(TestModel.Name))] = "Nombre",
            [metadata.KeyFor(typeof(TestModel), nameof(TestModel.Age))] = "Edad",
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton(metadata);
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

    // Three cells per row — the two declared columns and the action cell the table adds
    // itself — so the second row's Edad is the fifth. Find and click run as one dispatched
    // step, awaited: the editor's cells do not exist until the click's re-render has landed.
    [Fact]
    public async Task ACellOfTheRowBeingEditedCarriesNoHideHookWhereTheReadCellDoes()
    {
        var cut = Render<EditorRowBreakpointHost>();

        await cut.InvokeAsync(() => cut.FindAll("tbody button")[0].Click());

        var cells = cut.FindAll("tbody td");

        Assert.NotNull(cells[1].QuerySelector("input"));
        Assert.DoesNotContain("d-c-none", cells[1].ClassList);
        Assert.DoesNotContain("d-c-lg-table-cell", cells[1].ClassList);

        Assert.Contains("d-c-none", cells[4].ClassList);
        Assert.Contains("d-c-lg-table-cell", cells[4].ClassList);
    }

    /// <summary>The reading list does not get longer: freeing the editor's cells must not free
    /// the column, so an undeclared cell stays hookless and a declared read cell keeps both
    /// hooks whether or not some other row happens to be open.</summary>
    [Fact]
    public async Task TheColumnItselfStillHidesForEveryRowBeingRead()
    {
        var cut = Render<EditorRowBreakpointHost>();

        var before = cut.FindAll("tbody td");

        Assert.Contains("d-c-none", before[1].ClassList);
        Assert.DoesNotContain("d-c-none", before[0].ClassList);

        await cut.InvokeAsync(() => cut.FindAll("tbody button")[0].Click());

        var after = cut.FindAll("tbody td");

        Assert.DoesNotContain("d-c-none", after[0].ClassList);
        Assert.Contains("d-c-none", after[4].ClassList);
    }

    /// <summary>Stacked, a cell is a card line named by its data-label — the editor's input has
    /// to keep that name, or a freed cell would arrive on the phone anonymous.</summary>
    [Fact]
    public async Task TheFreedCellStillNamesItself()
    {
        var cut = Render<EditorRowBreakpointHost>();

        await cut.InvokeAsync(() => cut.FindAll("tbody button")[0].Click());

        Assert.Equal("Edad", cut.FindAll("tbody td")[1].GetAttribute("data-label"));
    }

    /// <summary>The column-mode half. A display:none cell generates no box, so a freed editor row
    /// laid out as a real table would own more columns than its header and drop every input one
    /// column off — the band between md and a column's own threshold, since an open row stacks
    /// below that (nsail#1614) and only an Lg or Xl column is still hidden above it
    /// (EditorRowColumnModeTests). The table itself is marked for as long as a row is open, which is
    /// what lets ns-mud.css stop the whole table from hiding; the cells keep their hooks, so the
    /// stacked rules that key on them are untouched.</summary>
    [Fact]
    public async Task TheTableIsMarkedForAsLongAsARowIsOpen()
    {
        var cut = Render<EditorRowBreakpointHost>();

        Assert.DoesNotContain("ns-table-editing", cut.Find(".ns-table").ClassList);

        await cut.InvokeAsync(() => cut.FindAll("tbody button")[0].Click());

        Assert.Contains("ns-table-editing", cut.Find(".ns-table").ClassList);
        Assert.Contains("d-c-none", cut.FindAll("thead th")[1].ClassList);

        // Reached through the action cell rather than by counting buttons: an editor holds
        // fields of its own and a numeric one brings two spin buttons with it.
        var actions = cut.FindAll("tbody td")[2].QuerySelectorAll("button");

        await cut.InvokeAsync(() => actions[1].Click());

        Assert.DoesNotContain("ns-table-editing", cut.Find(".ns-table").ClassList);
    }

    /// <summary>The reported path is Add, not Edit: a row that never existed opens on the same
    /// editor, so it has to be freed by the same flag.</summary>
    [Fact]
    public async Task AFreshRowsEditorIsFreedTheSameWay()
    {
        var cut = Render<EditorRowBreakpointHost>();

        // The added row lands at the end of the collection, and the add control is the
        // collection bar's, above the table.
        await cut.InvokeAsync(() => cut.Find(".ns-collection-bar button").Click());

        var cells = cut.FindAll("tbody td");

        Assert.NotNull(cells[7].QuerySelector("input"));
        Assert.DoesNotContain("d-c-none", cells[7].ClassList);
    }
}
