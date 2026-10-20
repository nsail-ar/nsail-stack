// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Exporting;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>Exportar used to be bare text below Nuevo, on its own line in the collection bar
/// (nsail#2344). It is now an icon-only trigger, with icons on its options, handed up to a list
/// page's header — the other way across the boundary NsTable already reads Surface.Title
/// through — so the header draws it ahead of Nuevo and the grid's own bar stays silent; an
/// embedded grid with no header on its surface keeps drawing its own, exactly as before. The
/// slot changes hands with a word to both sides — taking it and handing it back are announced
/// the way a grid's own fragment is — so neither the header nor the grid waits for an unrelated
/// render to find out where Exportar belongs.</summary>
public sealed class TableExportHeaderTests : BunitContext, IAsyncLifetime
{
    const string Export = "Exportar";

    readonly RecordingExportWriter _excel = new(ExportKind.Excel);
    readonly RecordingExportWriter _csv = new(ExportKind.Csv);

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public new async Task DisposeAsync()
    {
        await ((IAsyncDisposable)this).DisposeAsync();
    }

    sealed class FakeJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return default;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return default;
        }
    }

    SurfaceContext Setup()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        var metadata = new MetadataProvider();

        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Actions.Export"] = Export,
            ["Actions.ExportExcel"] = "Excel",
            ["Actions.ExportCsv"] = "CSV",
            ["Actions.Add"] = "Nuevo",
            [metadata.KeyFor(typeof(ExportRowModel), nameof(ExportRowModel.Name))] = "Nombre",
        })]));
        Services.AddSingleton(new LanguageProvider { Current = "es" });
        Services.AddSingleton(metadata);
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(TableExportHeaderTests).Assembly, []));
        Services.AddScoped<NavMenu>();
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        Services.AddSingleton(new ExportWriters([_excel, _csv]));
        JSInterop.Mode = JSRuntimeMode.Loose;

        var routes = Services.GetRequiredService<RouteTable>();
        var navigation = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();

        return new SurfaceContext(null, navigation, routes, new FakeJs(), new SurfaceHistory());
    }

    [Fact]
    public void AListPagesHeaderDrawsExportarAheadOfNuevoAndTheGridStaysSilent()
    {
        var surface = Setup();

        var cut = Render<ExportHeaderHost>(p => p.Add(x => x.Surface, surface));

        var header = cut.FindComponent<NsPageHeader>();

        // Handed up, drawn once, and nowhere else.
        Assert.Single(cut.FindAll(".ns-table-export"));
        Assert.Single(header.FindAll(".ns-table-export"));
        Assert.Empty(cut.FindComponent<NsTable<ExportRowModel>>().FindAll(".ns-table-export"));

        // Before Nuevo, in document order — the same row the AC asks for.
        var exportIndex = header.Markup.IndexOf("ns-table-export", StringComparison.Ordinal);
        var nuevoIndex = header.Markup.IndexOf("Nuevo", StringComparison.Ordinal);

        Assert.True(exportIndex >= 0);
        Assert.True(exportIndex < nuevoIndex);
    }

    [Fact]
    public void AnEmbeddedGridWithNoHeaderKeepsItsOwnExportar()
    {
        var surface = Setup();

        var cut = Render<ExportHeaderHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.ShowHeader, false));

        Assert.Single(cut.FindAll(".ns-table-export"));
        Assert.Single(cut.FindComponent<NsTable<ExportRowModel>>().FindAll(".ns-table-export"));
    }

    [Fact]
    public void TheTriggerIsIconOnlyWithExportarAsItsTooltipAndAccessibleName()
    {
        var surface = Setup();

        var cut = Render<ExportHeaderHost>(p => p.Add(x => x.Surface, surface));

        var button = cut.Find(".ns-table-export button");

        Assert.Equal(Export, button.GetAttribute("aria-label"));
        Assert.DoesNotContain(Export, button.TextContent, StringComparison.Ordinal);

        // No second, vendor tooltip: NsMenu's own aria-label already whispers the word through
        // the house's ungated CSS bubble (CollapsedLabelWhisperTests), and a MudTooltip beside it
        // would double-bill the name and stay painted over the menu its own press just opened.
        Assert.Empty(cut.FindComponents<MudBlazor.MudTooltip>());
    }

    [Fact]
    public async Task TheTriggerAndEachOptionCarryTheirIcons()
    {
        var surface = Setup();

        var cut = Render<ExportHeaderHost>(p => p.Add(x => x.Surface, surface));

        Assert.Contains(PathDataOf(NSail.Components.NsIcons.Download), cut.Find(".ns-table-export button").InnerHtml, StringComparison.Ordinal);

        await cut.InvokeAsync(() => cut.Find(".ns-table-export button").ClickAsync(new MouseEventArgs()));

        var list = cut.Find(".ns-menu-list");

        Assert.Contains(_excel.Icon.Markup, list.InnerHtml, StringComparison.Ordinal);
        Assert.Contains(_csv.Icon.Markup, list.InnerHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void ADocumentCollectionOnTheSameSurfaceNeverEvictsThePrimaryListsExportar()
    {
        var surface = Setup();

        var cut = Render<ExportHeaderMultiGridHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.ShowPrimary, true)
            .Add(x => x.ShowDocument, true));

        var header = cut.FindComponent<NsPageHeader>();

        // The document's own collection renders null on every one of its renders (its rows are
        // not a list at all, Document cascaded), and that must not be read as "nothing exports
        // here anymore" over a different grid's announcement.
        Assert.Single(header.FindAll(".ns-table-export"));
    }

    [Fact]
    public void ASecondUnnamedListDoesNotStealTheFirstsClaim()
    {
        var surface = Setup();

        var cut = Render<ExportHeaderMultiGridHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.ShowPrimary, true)
            .Add(x => x.ShowSecondList, true));

        var header = cut.FindComponent<NsPageHeader>();
        var tables = cut.FindComponents<NsTable<ExportRowModel>>();

        // Exactly one trigger on the whole surface: the first unnamed, exportable grid keeps the
        // slot, and the second does not get a copy of its own either — a header stands and
        // neither grid carries a Title to fall back on.
        Assert.Single(cut.FindAll(".ns-table-export"));
        Assert.Single(header.FindAll(".ns-table-export"));

        foreach (var table in tables)
        {
            Assert.Empty(table.FindAll(".ns-table-export"));
        }
    }

    [Fact]
    public void ANamedRelatedGridKeepsItsOwnExportarEvenWithAHeaderPresent()
    {
        var surface = Setup();

        var cut = Render<ExportHeaderMultiGridHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.ShowPrimary, true)
            .Add(x => x.ShowRelated, true));

        var header = cut.FindComponent<NsPageHeader>();

        // Two triggers: the page's own, in the header, and the named panel's own, in its own
        // bar beside "Relacionados" — a Title names a grid as a panel of a bigger page, which
        // never hands its export to a header it does not own.
        Assert.Equal(2, cut.FindAll(".ns-table-export").Count);
        Assert.Single(header.FindAll(".ns-table-export"));

        var related = cut.FindComponents<NsTable<ExportRowModel>>()
            .Single(t => t.Instance.Title == "Relacionados");

        Assert.Single(related.FindAll(".ns-table-export"));
        Assert.Contains("Relacionados", related.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void DisposingTheExportingGridRetractsTheHeadersExportar()
    {
        var surface = Setup();

        var cut = Render<ExportHeaderMultiGridHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.ShowPrimary, true));

        Assert.Single(cut.FindComponent<NsPageHeader>().FindAll(".ns-table-export"));

        cut.Render(p => p.Add(x => x.ShowPrimary, false));

        // The grid that held the slot is gone, so the header draws nothing in its place — not
        // Exportar pointed at a disposed grid, and not a toolbar row kept open for an empty slot.
        Assert.Null(surface.Export);
        Assert.Empty(cut.FindComponent<NsPageHeader>().FindAll(".ns-table-export"));
    }

    [Fact]
    public async Task AGridDrawsItsOwnExportarTheMomentTheSlotIsHandedBack()
    {
        var surface = Setup();

        // A header standing on this surface outside the rendered tree. Releasing through the
        // fixture's own live header would measure nothing: the release wakes it and it claims
        // again from the render that release prompts, which is the exchange
        // WalkingToTheNextListPageLeavesExportarInTheArrivingHeader covers. What is asked here
        // is the grid's own half — a slot that goes free under a grid already on screen.
        var elsewhere = new object();
        surface.ClaimsExport(elsewhere);

        var cut = Render<ExportHeaderHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.ShowHeader, false));

        var table = cut.FindComponent<NsTable<ExportRowModel>>();

        Assert.Empty(cut.FindAll(".ns-table-export"));

        await cut.InvokeAsync(() => surface.ReleaseExport(elsewhere));

        // Back in the collection bar, once. Nothing above the grid was touched to get there —
        // the release is the whole gesture — which on a real page is the whole point: nothing
        // above it was going to render either.
        Assert.Single(table.FindAll(".ns-table-export"));

        await cut.InvokeAsync(() => surface.ClaimsExport(elsewhere));

        // And the other way: a header takes the slot and the grid drops its copy, so the trigger
        // is never on screen twice whichever way the slot moved.
        Assert.Empty(cut.FindAll(".ns-table-export"));
    }

    [Fact]
    public async Task AnAnnouncementThatLeavesTheSlotWhereItIsDoesNotRedrawTheGrid()
    {
        var surface = Setup();

        var cut = Render<ExportHeaderHost>(p => p.Add(x => x.Surface, surface));

        var table = cut.FindComponent<NsTable<ExportRowModel>>();
        var drawn = table.RenderCount;

        await cut.InvokeAsync(() => surface.AnnounceExport(table.Instance, null));

        // ExportChanged carries every grid's every announcing render, this grid's own included,
        // and the only thing this grid's render reads off the surface is who holds the slot
        // (ui/surfaces.md, the announce seam). Redrawing for the rest would rebuild the rows to
        // emit what is already on screen — and for its own announcement, made from its render
        // body, it would not stop.
        Assert.Equal(drawn, table.RenderCount);
    }

    [Fact]
    public void AHeaderSwapLeavesTheArrivingHeaderHoldingTheSlotAndTheGridSilent()
    {
        var surface = Setup();

        var cut = Render<ExportHeaderSwapHost>(p => p.Add(x => x.Surface, surface));

        Assert.Single(cut.FindComponent<NsPageHeader>().FindAll(".ns-table-export"));

        cut.Render(p => p.Add(x => x.Arrived, true));

        // The arriving header is initialized and rendered before the leaving one is disposed, so
        // it finds the slot still held and claims nothing on that first render: the leaving
        // header's release is what hands it over. Without that word the surface arrives at the
        // next page with no Exportar at all — the slot free, the header not asking again, and
        // the grid, which read the slot while it was still held, not asking either.
        var header = cut.FindComponent<NsPageHeader>();

        Assert.Contains("Artículos", header.Markup, StringComparison.Ordinal);
        Assert.Single(cut.FindAll(".ns-table-export"));
        Assert.Single(header.FindAll(".ns-table-export"));
        Assert.Empty(cut.FindComponent<NsTable<ExportRowModel>>().FindAll(".ns-table-export"));
    }

    // AngleSharp re-serializes the void <path/> this house's Glyphs carry as <path></path>: the
    // DRAWING survives the round trip, the self-closing slash does not, so the comparison is
    // the one substring that does.
    static string PathDataOf(NSail.Icons.Glyph glyph)
    {
        var start = glyph.Markup.IndexOf("d=\"", StringComparison.Ordinal);
        var end = glyph.Markup.IndexOf('"', start + 3);

        return glyph.Markup[start..(end + 1)];
    }
}
