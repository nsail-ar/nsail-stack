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

/// <summary>A column heading a reader can press, and the list it orders. The offer is per column
/// and only where the grid holds every row of its list; the gesture is ascending, descending,
/// ascending, one column at a time; a blank cell is an unknown and sits last whichever way the
/// arrow points; and the file Exportar writes is the order on screen.
/// <para>What the heading LOOKS like once pressed is a stylesheet fact and the browser's to
/// answer; what it IS — a control with the column's own word as its name — is here.</para>
/// </summary>
public sealed class NsTableOrderTests : BunitContext, IAsyncLifetime
{
    const string Export = "Exportar";

    readonly RecordingExportWriter _csv = new(ExportKind.Csv);

    public NsTableOrderTests()
    {
        var metadata = new MetadataProvider();

        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Actions.Export"] = Export,
            ["Actions.ExportCsv"] = "CSV",
            [metadata.KeyFor(typeof(ExportRowModel), nameof(ExportRowModel.Name))] = "Nombre",
            [metadata.KeyFor(typeof(ExportRowModel), nameof(ExportRowModel.Count))] = "Cantidad",
            [metadata.KeyFor(typeof(ExportRowModel), nameof(ExportRowModel.Kind))] = "Tipo",
            [metadata.KeyFor(ExportRowKind.Person)] = "Persona",
            [metadata.KeyFor(ExportRowKind.Shop)] = "Comercio",
        })]));
        Services.AddSingleton(new LanguageProvider { Current = "es" });
        Services.AddSingleton(metadata);
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsTableOrderTests).Assembly, []));
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

    /// <summary>The offer is the column's own, never the grid's: a heading that asked for it is a
    /// control, the one beside it that did not is the text it always was, and the actions header
    /// — which declares no column and cannot — is silent however it is marked.</summary>
    [Fact]
    public void Only_the_columns_that_asked_for_it_are_pressable()
    {
        var cut = Render<OrderTableHost>();

        Assert.Equal(["Nombre", "Tipo"], cut.FindAll(".ns-th-order").Select(control => control.GetAttribute("aria-label")));
        Assert.Equal(4, cut.FindAll(".ns-table .mud-table-head th").Count);
    }

    /// <summary>The whole gesture: unpressed the list stands in the order it arrived in, the first
    /// press orders ascending, the second reverses it, and a third is back where the first was.
    /// Text folds case and accents the way the read that answered already collates (data.md), so
    /// Zorro follows beta rather than leading the column, and two names that differ by an accent
    /// keep the order they arrived in.</summary>
    [Fact]
    public async Task A_press_orders_ascending_and_a_second_press_reverses_it()
    {
        var cut = Render<OrderTableHost>();

        Assert.Equal(OrderTableHost.Arrived, Shown(cut));

        await Press(cut, "Nombre");

        Assert.Equal([1, 2, 3, 0, 4], Shown(cut));

        await Press(cut, "Nombre");

        Assert.Equal([0, 3, 1, 2, 4], Shown(cut));

        await Press(cut, "Nombre");

        Assert.Equal([1, 2, 3, 0, 4], Shown(cut));
    }

    /// <summary>One column orders at a time, and the one pressed takes the order over at
    /// ascending — never at the direction the column before it was pointing.</summary>
    [Fact]
    public async Task Another_column_takes_the_order_over_at_ascending()
    {
        var cut = Render<OrderTableHost>();

        await Press(cut, "Nombre");
        await Press(cut, "Nombre");

        Assert.Equal("descending", Marked(cut, "Nombre"));

        await Press(cut, "Tipo");

        Assert.Equal("ascending", Marked(cut, "Tipo"));
        Assert.Equal("none", Marked(cut, "Nombre"));
    }

    /// <summary>The mark is the pressed heading's alone: it carries the arrow it is pointing and
    /// no other heading carries a glyph at all.</summary>
    [Fact]
    public async Task The_ordered_heading_is_the_only_one_wearing_an_arrow()
    {
        var cut = Render<OrderTableHost>();

        Assert.Empty(Arrows(cut));

        await Press(cut, "Nombre");

        Assert.Equal(["Nombre"], Arrows(cut).Select(arrow => arrow.GetAttribute("aria-label")));
        Assert.Contains(Drawing(cut), NsIcons.MoveUp.Markup, StringComparison.Ordinal);

        await Press(cut, "Nombre");

        Assert.Contains(Drawing(cut), NsIcons.MoveDown.Markup, StringComparison.Ordinal);
    }

    /// <summary>An enum column orders by the word the cell shows and not by the value behind it —
    /// the promise the exported file already makes. Comercio before Persona is the Spanish
    /// alphabet's answer; the enum's own order is the other one.</summary>
    [Fact]
    public async Task An_enum_column_orders_by_the_word_the_cell_shows()
    {
        var cut = Render<OrderTableHost>();

        await Press(cut, "Tipo");

        Assert.Equal([1, 3, 4, 0, 2], Shown(cut));
    }

    /// <summary>A row with nothing in the ordered column sits last in both directions: "sin
    /// backup" is an unknown, not the oldest backup.</summary>
    [Fact]
    public async Task A_blank_cell_lands_last_in_both_directions()
    {
        var cut = Render<OrderTableHost>();

        await Press(cut, "Nombre");

        Assert.Equal(4, Shown(cut)[^1]);

        await Press(cut, "Nombre");

        Assert.Equal(4, Shown(cut)[^1]);
    }

    /// <summary>A grid the server pages holds a slice of its list, so ordering it would order the
    /// page and read as ordering the list: the heading stays the plain text it is on every other
    /// list, whatever the column declared. The server-side order is its own question.</summary>
    [Fact]
    public void A_server_paged_grid_draws_no_pressable_heading()
    {
        var cut = Render<OrderTableHost>(parameters => parameters.Add(host => host.Queried, true));

        Assert.Empty(cut.FindAll(".ns-th-order"));
        Assert.Contains("Nombre", cut.Find(".ns-table .mud-table-head").TextContent, StringComparison.Ordinal);
    }

    /// <summary>The file is the list the reader is looking at, in the order they are looking at
    /// it: a file in another order than its own list is the same defect as one three hours off it
    /// (exporting.md).</summary>
    [Fact]
    public async Task Exportar_writes_the_rows_in_the_order_on_screen()
    {
        Services.AddSingleton(new ExportWriters([_csv]));

        var cut = Render<OrderTableHost>();

        await Press(cut, "Nombre");
        await Press(cut, "Nombre");

        await Open(cut);

        await cut.InvokeAsync(() => cut
            .Find(".ns-menu-list")
            .QuerySelectorAll(".ns-menu-item")
            .First(item => item.TextContent.Contains("CSV", StringComparison.Ordinal))
            .ClickAsync(new MouseEventArgs()));

        Assert.Equal([0, 3, 1, 2, 4], _csv.Rows.Select(row => Convert.ToInt32(row.Values[0])));
    }

    // Which rows are on screen and in which order, read off the column no test presses.
    static int[] Shown(IRenderedComponent<OrderTableHost> cut)
    {
        return [.. cut.FindAll(".ns-table .mud-table-body tr td:first-child").Select(cell => int.Parse(cell.TextContent.Trim(), null))];
    }

    static AngleSharp.Dom.IElement[] Arrows(IRenderedComponent<OrderTableHost> cut)
    {
        return [.. cut.FindAll(".ns-th-order").Where(control => control.QuerySelector("svg path") is not null)];
    }

    // The one arrow on screen, read as the drawing it is: the catalog's own path, so no test
    // carries a copy of a glyph (NsFileUploadTests).
    static string Drawing(IRenderedComponent<OrderTableHost> cut)
    {
        return Arrows(cut).Single().QuerySelector("svg path")!.GetAttribute("d")!;
    }

    static string? Marked(IRenderedComponent<OrderTableHost> cut, string heading)
    {
        return Heading(cut, heading).Closest("th")!.GetAttribute("aria-sort");
    }

    static AngleSharp.Dom.IElement Heading(IRenderedComponent<OrderTableHost> cut, string heading)
    {
        return cut.FindAll(".ns-th-order").First(control => control.GetAttribute("aria-label") == heading);
    }

    // Found and pressed inside ONE dispatch: between a Find and a Click the tree can re-render
    // and the handler the element carried is gone by the time the click reaches it (bUnit,
    // UnknownEventHandlerIdException — TableExportTests' note).
    static Task Press(IRenderedComponent<OrderTableHost> cut, string heading)
    {
        return cut.InvokeAsync(() => Heading(cut, heading).ClickAsync(new MouseEventArgs()));
    }

    static Task Open(IRenderedComponent<OrderTableHost> cut)
    {
        return cut.InvokeAsync(() => cut.Find(".ns-table-export button").ClickAsync(new MouseEventArgs()));
    }
}
