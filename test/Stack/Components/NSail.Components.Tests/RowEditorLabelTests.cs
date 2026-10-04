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

/// <summary>An inline editor's cells ask for no visible label — the column header above them
/// already says the word — and that used to leave their inputs anonymous: nothing a screen
/// reader could announce and nothing a semantic selector could reach, which is what blocked the
/// browser golden paths. A blank Label now hides the label without removing it: the field keeps
/// the text it would have shown as its accessible name, and that text is the column header's,
/// because NsTh's For expression and the field's own binding resolve to the same member and
/// therefore to the same localization key. No screen writes a string for this.</summary>
public sealed class RowEditorLabelTests : BunitContext, IAsyncLifetime
{
    public RowEditorLabelTests()
    {
        var metadata = new MetadataProvider();

        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            [metadata.KeyFor(typeof(TestModel), nameof(TestModel.Name))] = "Nombre",
            [metadata.KeyFor(typeof(TestModel), nameof(TestModel.Age))] = "Edad",
            ["Actions.Edit"] = "Editar",
            ["Actions.Confirm"] = "Confirmar",
            ["Actions.Cancel"] = "Cancelar",
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton(metadata);
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider resolves a MudBlazor service that is internal and IAsyncDisposable
    // only, so bUnit's synchronous teardown cannot dispose it (NsSelectTests' own note).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    // The row's own controls are the only buttons the body renders (the cells hold plain
    // text), and Edit is the first of them.
    async Task<IRenderedComponent<RowEditorHost>> Editing(string kind = "text")
    {
        var cut = Render<RowEditorHost>(ps => ps.Add(p => p.Kind, kind));

        await cut.InvokeAsync(() => cut.FindAll("tbody button")[0].Click());

        return cut;
    }

    [Fact]
    public async Task AnInlineEditorsInputIsNamedByItsColumnHeader()
    {
        var cut = await Editing();

        var headers = cut.FindAll("thead th").Select(cell => cell.TextContent.Trim()).ToList();

        Assert.Equal("Nombre", headers[0]);
        Assert.Equal("Edad", headers[1]);

        var cells = cut.FindAll("tbody td");

        Assert.Equal(headers[0], cells[0].QuerySelector("input")!.GetAttribute("aria-label"));
        Assert.Equal(headers[1], cells[1].QuerySelector("input")!.GetAttribute("aria-label"));
    }

    /// <summary>The three vendor inputs a real inline editor reaches for — a text box, a
    /// select and an autocomplete — each carry the name through a different internal path, and
    /// the select is the one the browser suite could never reach: its accessible name defaults
    /// to whatever VALUE it currently shows (AsideSmokeTests' note), never to its column.</summary>
    [Theory]
    [InlineData("text")]
    [InlineData("select")]
    [InlineData("autocomplete")]
    [InlineData("unbound")]
    public async Task EveryControlAnInlineEditorUsesCarriesTheName(string kind)
    {
        var cut = await Editing(kind);

        var input = cut.FindAll("tbody td")[0].QuerySelector("input");

        Assert.Equal("Nombre", input!.GetAttribute("aria-label"));
    }

    /// <summary>A control whose value is a whole row rather than the member (a lookup crossing
    /// product and store) is bound by hand, so the field has no expression to read its member
    /// off — and a cell that lost its name that way is a column a screen reader goes silent on
    /// and a golden path stops at. The name still comes from the column, never from a literal
    /// the screen writes beside the header it would duplicate.</summary>
    [Fact]
    public async Task AHandBoundControlIsNamedByItsColumnToo()
    {
        var cut = await Editing("unbound");

        var cell = cut.FindAll("tbody td")[0];
        var header = cut.FindAll("thead th")[0].TextContent.Trim();

        Assert.Equal(header, cell.QuerySelector("input")!.GetAttribute("aria-label"));
        Assert.Empty(cell.QuerySelectorAll("label"));
    }

    /// <summary>The name is the invisible one: asking for no label must still leave nothing
    /// drawn in the cell, or every inline editor would grow a second copy of its header.</summary>
    [Fact]
    public async Task TheNameIsCarriedWithoutDrawingALabel()
    {
        var cut = await Editing();

        var cells = cut.FindAll("tbody td");

        Assert.Empty(cells[0].QuerySelectorAll("label"));
        Assert.Empty(cells[1].QuerySelectorAll("label"));
    }

    /// <summary>The mechanism is opt-in and costs an ordinary form nothing: a field that never
    /// asked for a hidden label keeps its visible one and gains no attribute — the vendor's own
    /// label/for association already names that input.</summary>
    [Fact]
    public void AFieldWithAVisibleLabelGainsNoAttribute()
    {
        var cut = Render<RowEditorHost>();

        var loose = cut.Find("#loose input");

        Assert.Null(loose.GetAttribute("aria-label"));
        Assert.NotEmpty(cut.Find("#loose").QuerySelectorAll("label"));
    }

    /// <summary>The row's own controls are icon buttons, and a tooltip paints a hover popover
    /// rather than naming what it hovers — so the Label an icon button is always given (its own
    /// summary calls it the accessible label) has to be written as one. Without it the confirm
    /// that closes an inline row is as anonymous as the cells beside it were.</summary>
    [Fact]
    public async Task TheRowsOwnControlsAreNamedByTheirLabel()
    {
        // One render for both halves: a second MudPopoverProvider in the same context claims
        // a section id the first already holds.
        var cut = Render<RowEditorHost>();

        Assert.Equal("Editar", cut.FindAll("tbody button")[0].GetAttribute("aria-label"));

        await cut.InvokeAsync(() => cut.FindAll("tbody button")[0].Click());

        var names = cut.FindAll("tbody button").Select(button => button.GetAttribute("aria-label")).ToList();

        Assert.Contains("Confirmar", names);
        Assert.Contains("Cancelar", names);
    }

    /// <summary>A row added rather than edited opens on the same editor, so the name has to
    /// come from the column and not from anything the row already held.</summary>
    [Fact]
    public async Task AFreshRowsEditorIsNamedTheSameWay()
    {
        var cut = Render<RowEditorHost>();

        // The added row lands at the end of the collection, so the editor is the only thing
        // in the body carrying inputs at all. The add control is the collection bar's, above
        // the table and outside tbody. Find and click run as one dispatched step, awaited: the
        // editor's inputs do not exist until the click's re-render has landed.
        await cut.InvokeAsync(() => cut.Find(".ns-collection-bar button").Click());

        var inputs = cut.FindAll("tbody input");

        Assert.Equal("Nombre", inputs[0].GetAttribute("aria-label"));
        Assert.Equal("Edad", inputs[1].GetAttribute("aria-label"));
    }
}
