// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;
using NSail.Testing.Models;

namespace NSail.Components.Tests;

/// <summary>nsail#1492: stacked, the row open for editing is a FORM, so each of its cells draws
/// its label above its field and the whole row shares one left edge — where a row being read
/// keeps the vendor's label-then-value card line. The two are told apart by the cell, not by the
/// table: ns-table-editing says some row is open, while ns-cell-editing rides the cells of that
/// row alone. bUnit lays nothing out and runs no media query, so what is pinned here is the DOM
/// the stacked rules in ns-mud.css key on — the edge itself is measured in a browser
/// (StackedEditorRowTests, Optical's mobile lane).</summary>
public sealed class EditorRowStackedFormTests : BunitContext, IAsyncLifetime
{
    public EditorRowStackedFormTests()
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

    /// <summary>Two rows, three cells each (the two declared columns and the action cell the
    /// table adds itself), so opening the first row leaves the second one being read: the marker
    /// has to reach the open row's cells and none of the other's.</summary>
    [Fact]
    public async Task OnlyTheCellsOfTheRowOpenForEditingAreMarkedAsFormLines()
    {
        var cut = Render<EditorRowBreakpointHost>();

        foreach (var cell in cut.FindAll("tbody td"))
        {
            Assert.DoesNotContain("ns-cell-editing", cell.ClassList);
        }

        await cut.InvokeAsync(() => cut.FindAll("tbody button")[0].Click());

        var cells = cut.FindAll("tbody td");

        Assert.Contains("ns-cell-editing", cells[0].ClassList);
        Assert.Contains("ns-cell-editing", cells[1].ClassList);

        // The second row is still being read, and its cells are the card lines Leonardo's own
        // ruling on the stacked label applies to.
        Assert.DoesNotContain("ns-cell-editing", cells[3].ClassList);
        Assert.DoesNotContain("ns-cell-editing", cells[4].ClassList);
    }

    /// <summary>A field cell's label is the vendor's own ::before on data-label, so the name has
    /// to survive the move above it — a stacked form line whose label went missing is the ruling
    /// this layout is bound by ("NO quites la etiqueta"), broken.</summary>
    [Fact]
    public async Task AMarkedCellStillCarriesTheLabelTheLineIsNamedBy()
    {
        var cut = Render<EditorRowBreakpointHost>();

        await cut.InvokeAsync(() => cut.FindAll("tbody button")[0].Click());

        var cells = cut.FindAll("tbody td");

        Assert.Equal("Nombre", cells[0].GetAttribute("data-label"));
        Assert.Equal("Edad", cells[1].GetAttribute("data-label"));
        Assert.NotNull(cells[1].QuerySelector("input"));
    }

    /// <summary>A row that never existed opens on the same editor, so it stacks as the same
    /// form: Add is the path the report was walked on.</summary>
    [Fact]
    public async Task AFreshRowsEditorStacksAsTheSameForm()
    {
        var cut = Render<EditorRowBreakpointHost>();

        await cut.InvokeAsync(() => cut.Find(".ns-collection-bar button").Click());

        var cells = cut.FindAll("tbody td");

        Assert.Contains("ns-cell-editing", cells[6].ClassList);
        Assert.Contains("ns-cell-editing", cells[7].ClassList);
    }
}
