// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#1816 — Leonardo, on Optical: "no sé si me gusta el odd/even en la tabla" and
/// "hay un doble borde feo en la tabla". A grid is told apart by the line between its rows and
/// by the hover tone, never by an alternating tint, and a table placed inside a sheet of its own
/// draws one edge — the sheet's. Both halves are stylesheet and markup facts with no geometry in
/// them, so they are pinned the NsTablePagerShrinkTests way: the class the vendor's zebra needs
/// is absent from real DOM in all three of NsTable's modes, the selectors the flattening rule
/// depends on reach real DOM under both sheets, and the rules themselves are read verbatim out
/// of the shipped ns-mud.css so markup and stylesheet cannot drift apart in silence. What the
/// eye is actually given — a computed border, radius and fill, in both schemes — is the browser's
/// to answer (NSail.Optical.E2E, TableOneFrameTests).</summary>
public sealed class NsTableChromeTests : BunitContext, IAsyncLifetime
{
    public NsTableChromeTests()
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

    // mud-table-striped is the only thing the vendor's zebra rules select on, so its absence IS
    // the banding's absence — nothing else in the vendor or in ns-mud.css tints a row by parity.
    [Theory]
    [InlineData(NsTableMode.Scroll)]
    [InlineData(NsTableMode.Page)]
    public void AQueriedTableDoesNotBandItsRows(NsTableMode mode)
    {
        var cut = Render<ScrollTableHost>(parameters => parameters.Add(p => p.Mode, mode));

        Assert.NotEmpty(cut.FindAll(".ns-table .mud-table-body tr"));
        Assert.Empty(cut.FindAll(".mud-table-striped"));
    }

    // The third mode: rows in hand, no OnQuery, so neither branch above renders.
    [Fact]
    public void AnInMemoryTableDoesNotBandItsRows()
    {
        var cut = Render<SheetedTableHost>();

        Assert.NotEmpty(cut.FindAll(".ns-table .mud-table-body tr"));
        Assert.Empty(cut.FindAll(".mud-table-striped"));
    }

    // What divides two rows once the tint is gone: the vendor's own per-row hairline, which this
    // stylesheet used to suppress BECAUSE the zebra already drew an edge. With that rule gone the
    // line is back, and the vendor drops it under the last row itself.
    [Fact]
    public void TheStylesheetNoLongerSuppressesTheLineBetweenTwoRows()
    {
        Assert.DoesNotContain(".ns-table .mud-table-body .mud-table-cell", ReadStylesheet(), StringComparison.Ordinal);
    }

    [Fact]
    public void ATableInsideASheetIsFlatOnIt()
    {
        var css = ReadStylesheet();

        // The rule names both sheets in one selector list, so the second line is what the block
        // hangs off — matched without the newline between them, which a checkout may rewrite.
        Assert.Contains(".ns-paper .ns-table,", css, StringComparison.Ordinal);

        var block = Rule(css, ".ns-card .ns-table {");

        Assert.Contains("background-color: transparent;", block, StringComparison.Ordinal);
        Assert.Contains("border: none;", block, StringComparison.Ordinal);
        Assert.Contains("border-radius: 0;", block, StringComparison.Ordinal);

        // The edge actually drawn on 9.10.0: the table's root renders mud-elevation-1, whose ring
        // BuildElevations casts. Outlined is false there and no border is rendered at all.
        Assert.Contains("box-shadow: none;", block, StringComparison.Ordinal);
    }

    [Fact]
    public void BothSheetsPutTheirHookAboveTheTableTheRuleFlattens()
    {
        var cut = Render<SheetedTableHost>();

        Assert.Single(cut.FindAll(".ns-paper .ns-table"));
        Assert.Single(cut.FindAll(".ns-card .ns-table"));
    }

    // A grid standing alone on a page is under neither hook, so the rule above cannot reach it
    // and it keeps the fill and border that make it read as a card against the canvas — NsTable
    // has 85 call sites and only nine of them sit inside a sheet.
    [Fact]
    public void AStandaloneTableIsUnderNeitherHook()
    {
        var cut = Render<ScrollTableHost>(parameters => parameters.Add(p => p.Mode, NsTableMode.Page));

        Assert.Empty(cut.FindAll(".ns-paper .ns-table, .ns-card .ns-table"));
        Assert.Single(cut.FindAll(".ns-table.mud-elevation-1"));
    }

    static string Rule(string css, string selector)
    {
        var start = css.IndexOf(selector, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{selector}' was not found in the stylesheet.");

        var end = css.IndexOf('}', start);
        Assert.True(end >= 0, $"'{selector}' has no closing brace.");

        return css[start..end];
    }

    static string ReadStylesheet()
    {
        return File.ReadAllText(Path.Combine(
            RepoRoot(),
            "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css"));
    }

    static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"NSail.sln was not found above {AppContext.BaseDirectory}.");
    }
}
