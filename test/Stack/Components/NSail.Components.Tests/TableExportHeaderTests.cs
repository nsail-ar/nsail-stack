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
/// embedded grid with no header on its surface keeps drawing its own, exactly as before.</summary>
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
        Assert.Contains(Export, cut.FindComponent<MudBlazor.MudTooltip>().Markup, StringComparison.Ordinal);
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
