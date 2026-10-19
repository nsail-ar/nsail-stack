// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Exporting;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>Every list offers Exportar, and what it exports is what the current filter answers —
/// all of it, not the page on screen. The grid's half of that: which grids offer it, what the
/// book's columns are, and that the rows come from reading every page.
/// <para>The writer here records the book instead of writing a file (RecordingExportWriter): what
/// a real one does with it is NSail.Exporting.Tests'.</para></summary>
public sealed class TableExportTests : BunitContext, IAsyncLifetime
{
    const string Export = "Exportar";

    readonly RecordingExportWriter _excel = new(ExportKind.Excel);
    readonly RecordingExportWriter _csv = new(ExportKind.Csv);

    public TableExportTests()
    {
        var metadata = new MetadataProvider();

        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Actions.Export"] = Export,
            ["Actions.ExportExcel"] = "Excel",
            ["Actions.ExportCsv"] = "CSV",
            [metadata.KeyFor(typeof(ExportRowModel), nameof(ExportRowModel.Name))] = "Nombre",
            [metadata.KeyFor(typeof(ExportRowModel), nameof(ExportRowModel.Count))] = "Cantidad",
            [metadata.KeyFor(typeof(ExportRowModel), nameof(ExportRowModel.Total))] = "Total",
            [metadata.KeyFor(typeof(ExportRowModel), nameof(ExportRowModel.Share))] = "Parte",
            [metadata.KeyFor(typeof(ExportRowModel), nameof(ExportRowModel.On))] = "Fecha",
            [metadata.KeyFor(typeof(ExportRowModel), nameof(ExportRowModel.Kind))] = "Tipo",
            [metadata.KeyFor(typeof(ExportRowModel), nameof(ExportRowModel.IsActive))] = "Activo",
            [metadata.KeyFor(ExportRowKind.Person)] = "Persona",
            [metadata.KeyFor(ExportRowKind.Shop)] = "Comercio",
            ["Common.Yes"] = "Sí",
            ["Common.No"] = "No",
        })]));
        Services.AddSingleton(new LanguageProvider { Current = "es" });
        Services.AddSingleton(metadata);
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(TableExportTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
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

    void Writers(params RecordingExportWriter[] writers)
    {
        Services.AddSingleton(new ExportWriters(writers));
    }

    [Fact]
    public void A_server_paged_list_offers_Exportar()
    {
        Writers(_excel, _csv);

        var cut = Render<ExportTableHost>();

        Assert.Equal(Export, cut.Find(".ns-table-export button").GetAttribute("aria-label"));
    }

    /// <summary>A host that registered no writer gets a grid with no Exportar on it, rather than a
    /// menu that opens onto nothing.</summary>
    [Fact]
    public void A_host_with_no_writer_offers_nothing()
    {
        var cut = Render<ExportTableHost>();

        Assert.Empty(cut.FindAll(".ns-table-export"));
    }

    [Fact]
    public void A_registered_writer_with_no_kinds_offers_nothing()
    {
        Services.AddSingleton(new ExportWriters([]));

        var cut = Render<ExportTableHost>();

        Assert.Empty(cut.FindAll(".ns-table-export"));
    }

    /// <summary>A grid typed into is not a list: its rows are a field of the document being
    /// edited, and the document is saved rather than exported.</summary>
    [Fact]
    public void A_grid_typed_into_offers_nothing()
    {
        Writers(_excel, _csv);

        var cut = Render<ExportTableHost>(parameters => parameters
            .Add(host => host.Editable, true)
            .Add(host => host.Title, "Líneas"));

        Assert.Empty(cut.FindAll(".ns-table-export"));
        Assert.Single(cut.FindAll(".ns-cells-add"));
    }

    /// <summary>The business/machinery opt-out (exporting.md): a list that IS a list by every
    /// other measure still drops the menu when the screen says its rows are not worth it, and
    /// takes the bar's whole seat with it when nothing else needs one.</summary>
    [Fact]
    public void An_opted_out_list_offers_nothing_and_draws_no_bar()
    {
        Writers(_excel, _csv);

        var cut = Render<ExportTableHost>(parameters => parameters.Add(host => host.Exportable, false));

        Assert.Empty(cut.FindAll(".ns-table-export"));
        Assert.Empty(cut.FindAll(".ns-collection-bar"));
    }

    /// <summary>The opt-out takes only the menu: a named list keeps its heading and the bar that
    /// carries it.</summary>
    [Fact]
    public void An_opted_out_named_list_keeps_its_heading_and_loses_only_Exportar()
    {
        Writers(_excel, _csv);

        var cut = Render<ExportTableHost>(parameters => parameters
            .Add(host => host.Exportable, false)
            .Add(host => host.Title, "Pedidos"));

        Assert.Empty(cut.FindAll(".ns-table-export"));
        Assert.Contains("Pedidos", cut.Find(".ns-collection-bar").TextContent, StringComparison.Ordinal);
    }

    /// <summary>A register the client HOLDS is a list too: its rows went past no page, so "all
    /// pages" is every row in hand, and a reader asking for a spreadsheet does not know which kind
    /// of list they are looking at.</summary>
    [Fact]
    public async Task A_register_the_client_holds_offers_Exportar_and_writes_every_row()
    {
        Writers(_excel);

        await Held("Excel");

        Assert.Equal(ExportHeldTableHost.Held, _excel.Rows.Count);
        Assert.Equal("Row 0", _excel.Rows[0].Values[0]);
        Assert.Equal($"Row {ExportHeldTableHost.Held - 1}", _excel.Rows[^1].Values[0]);
    }

    /// <summary>And the one thing a held list is not: a collection a DOCUMENT holds. Its rows are
    /// a field of what the submit saves, so the document is what gets saved and there is nothing
    /// to read out on its own.</summary>
    [Fact]
    public void A_collection_a_document_holds_offers_nothing()
    {
        Writers(_excel, _csv);

        var cut = Render<ExportHeldTableHost>(parameters => parameters.Add(host => host.InDocument, true));

        Assert.Empty(cut.FindAll(".ns-table-export"));
    }

    /// <summary>Nor is a collection with a way IN, document or not: a grid that can be added to or
    /// taken from is being edited wherever it stands.</summary>
    [Fact]
    public void A_collection_with_a_way_in_offers_nothing()
    {
        Writers(_excel, _csv);

        var cut = Render<ExportHeldTableHost>(parameters => parameters.Add(host => host.Editable, true));

        Assert.Empty(cut.FindAll(".ns-table-export"));
        Assert.Empty(cut.FindAll(".ns-collection-bar"));
    }

    /// <summary>A boolean reads as the word the cell prints, for the same reason an enum does:
    /// True under a heading that says Activo is the CLR's word for it and nobody else's.</summary>
    [Fact]
    public async Task A_boolean_column_carries_the_word_the_cell_shows()
    {
        Writers(_csv);

        await Held("CSV");

        Assert.Equal("Sí", _csv.Rows[0].Values[2]);
        Assert.Equal("No", _csv.Rows[1].Values[2]);
    }

    /// <summary>The bar draws for a grid that has something to put in it, named or not: there is
    /// no other seat for a control over the whole collection, and the pager row — the alternative
    /// — exists in only one of the three paging modes.</summary>
    [Fact]
    public void The_bar_draws_for_a_list_that_does_not_name_itself()
    {
        Writers(_excel);

        var cut = Render<ExportTableHost>();

        Assert.Single(cut.FindAll(".ns-collection-bar .ns-table-export"));
        Assert.Empty(cut.FindAll(".ns-collection-bar .mud-typography-h6"));
    }

    [Fact]
    public void A_named_list_keeps_its_heading_beside_Exportar()
    {
        Writers(_excel);

        var cut = Render<ExportTableHost>(parameters => parameters.Add(host => host.Title, "Pedidos"));

        Assert.Single(cut.FindAll(".ns-collection-bar .ns-table-export"));
        Assert.Contains("Pedidos", cut.Find(".ns-collection-bar").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_menu_offers_a_row_per_registered_writer_in_one_order()
    {
        Writers(_csv, _excel);

        var cut = Render<ExportTableHost>();

        await Open(cut);

        Assert.Equal(["Excel", "CSV"], cut.Find(".ns-menu-list").QuerySelectorAll(".ns-menu-item-label").Select(row => row.TextContent.Trim()));
    }

    [Fact]
    public async Task The_columns_are_the_headers_own_titles_and_formats()
    {
        Writers(_excel);

        await Choose("Excel");

        var table = _excel.Book!.Pages[0].Tables[0];

        Assert.Equal(["Nombre", "Cantidad", "Total", "Parte", "Fecha", "Tipo"], table.Columns.Select(column => column.Title));

        Assert.Equal(
            [ExportFormat.Text, ExportFormat.Number, ExportFormat.Money, ExportFormat.Percent, ExportFormat.Date, ExportFormat.Text],
            table.Columns.Select(column => column.Format));
    }

    /// <summary>The action column is not a column: it has no field behind it and nothing to key
    /// on, which is already why it never hides (ui/hosts.md).</summary>
    [Fact]
    public async Task The_action_column_is_not_in_the_file()
    {
        Writers(_excel);

        var cut = await Choose("Excel");

        Assert.Equal(cut.FindAll("thead th").Count - 1, _excel.Book!.Pages[0].Tables[0].Columns.Count);
    }

    /// <summary>A column the list's own filter drops is gone from the file too — the column set is
    /// rebuilt on every header render rather than accumulated (TableColumns).</summary>
    [Fact]
    public async Task A_dropped_column_leaves_the_file()
    {
        Writers(_excel);

        await Choose("Excel", parameters => parameters.Add(host => host.DropKind, true));

        Assert.DoesNotContain("Tipo", _excel.Book!.Pages[0].Tables[0].Columns.Select(column => column.Title));
        Assert.Equal(5, _excel.Book.Pages[0].Tables[0].Columns.Count);
    }

    /// <summary>The whole claim, end to end through the grid: Exportar reaches every page the
    /// filter answers, in pages of its own rather than the pager's, and the last one is short.
    /// The arithmetic on its own is TableExportPagesTests'.</summary>
    [Fact]
    public async Task Every_page_the_filter_answers_is_read_and_written()
    {
        Writers(_excel);

        var cut = await Choose("Excel");

        Assert.Equal(ExportTableHost.Register, _excel.Rows.Count);
        Assert.Equal("Row 0", _excel.Rows[0].Values[0]);
        Assert.Equal($"Row {ExportTableHost.Register - 1}", _excel.Rows[^1].Values[0]);

        // Three pages of a thousand for 2 350 rows, after the one the grid asked for to draw
        // itself — which was the PAGER's size, not this one.
        Assert.Equal(
            [(0, TableExport.PageSize), (1, TableExport.PageSize), (2, TableExport.PageSize)],
            cut.Instance.Asked.Where(asked => asked.Size == TableExport.PageSize));

        Assert.Contains(cut.Instance.Asked, asked => asked.Size != TableExport.PageSize);
    }

    /// <summary>An enum reads as the word the cell shows, not as its CLR name: a file in another
    /// language than the person who asked for it is the list mistranslated.</summary>
    [Fact]
    public async Task An_enum_column_carries_its_own_word()
    {
        Writers(_csv);

        await Choose("CSV");

        Assert.Equal("Persona", _csv.Rows[0].Values[5]);
        Assert.Equal("Comercio", _csv.Rows[1].Values[5]);
    }

    /// <summary>A refusal part way through leaves no file at all: the pages already read are not
    /// offered as the list.</summary>
    [Fact]
    public async Task A_refusal_part_way_through_leaves_no_file()
    {
        Writers(_excel);

        await Choose("Excel", parameters => parameters.Add(host => host.RefuseFrom, 2));

        Assert.Equal(2 * TableExport.PageSize, _excel.Rows.Count);
        Assert.NotEqual(ExportTableHost.Register, _excel.Rows.Count);
    }

    /// <summary>A read that does not land leaves no file: half a list downloaded as a whole one is
    /// worse than the refusal the reader already has on screen.</summary>
    [Fact]
    public async Task A_read_that_does_not_land_leaves_no_file()
    {
        Writers(_excel);

        await Choose("Excel", parameters => parameters.Add(host => host.RefuseFrom, 0));

        Assert.Empty(_excel.Rows);
    }

    /// <summary>The file is named after what the screen calls the list, and the culture the writer
    /// is handed is the reader's region — not the thread's.</summary>
    [Fact]
    public async Task The_book_is_named_after_the_list_and_written_in_the_readers_region()
    {
        Writers(_excel);

        await Choose("Excel", parameters => parameters.Add(host => host.Title, "Pedidos"));

        Assert.Equal("Pedidos", _excel.Book!.Name);
        Assert.Equal("Pedidos", _excel.Book.Pages[0].Name);
        Assert.Equal("es-AR", _excel.Culture!.Name);
    }

    async Task<IRenderedComponent<ExportTableHost>> Choose(
        string row,
        Action<ComponentParameterCollectionBuilder<ExportTableHost>>? parameters = null)
    {
        var cut = parameters is null ? Render<ExportTableHost>() : Render<ExportTableHost>(parameters);

        await Pick(cut, row);

        return cut;
    }

    async Task<IRenderedComponent<ExportHeldTableHost>> Held(string row)
    {
        var cut = Render<ExportHeldTableHost>();

        await Pick(cut, row);

        return cut;
    }

    // Every press in this file is found and pressed inside ONE dispatch: between a Find and a
    // Click the tree can re-render — the first read's own page lands on a continuation, and so
    // does the open menu's popover — and the handler the element carried is gone by the time the
    // click reaches it (bUnit, UnknownEventHandlerIdException, which a loaded machine is what
    // makes likely).
    static Task Open<THost>(IRenderedComponent<THost> cut)
        where THost : IComponent
    {
        return cut.InvokeAsync(() => cut.Find(".ns-table-export button").ClickAsync(new MouseEventArgs()));
    }

    static async Task Pick<THost>(IRenderedComponent<THost> cut, string row)
        where THost : IComponent
    {
        await Open(cut);

        await cut.InvokeAsync(() => cut
            .Find(".ns-menu-list")
            .QuerySelectorAll(".ns-menu-item")
            .First(item => item.TextContent.Contains(row, StringComparison.Ordinal))
            .ClickAsync(new MouseEventArgs()));
    }

}
