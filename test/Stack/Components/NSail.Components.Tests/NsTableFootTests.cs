// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#2054 — a totals row drawn outside the grid lines up under its columns only
/// where the strip and the columns happen to share an alignment, and stacked it has nothing left
/// to line up with at all. FooterRow puts the figures in the table's own tfoot, so they are
/// placed and named by the same mechanism every other cell already uses.
///
/// Markup here, geometry in the browser: bUnit lays out no box model, so the halves this suite
/// can prove are the element, the cell counts against the header, the per-cell column names and
/// the classes the stylesheet keys on. The rule that takes the vendor's hidden foot back is read
/// verbatim out of the shipped ns-mud.css, the NsTableChromeTests way, and what the eye is given
/// at phone width belongs to NSail.Optical.E2E (VatBookTotalsTests).</summary>
public sealed class NsTableFootTests : BunitContext, IAsyncLifetime
{
    public NsTableFootTests()
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

    // All three branches: the two that wire OnQuery (scroll appends, page replaces) and the one
    // that renders the rows in hand. A book reads through two of them at once — the same screen
    // resolves Auto to Scroll on a phone and to Page everywhere else.
    [Theory]
    [InlineData(NsTableMode.Page, true)]
    [InlineData(NsTableMode.Scroll, true)]
    [InlineData(NsTableMode.Page, false)]
    public void TheFootIsOneCellPerColumn(NsTableMode mode, bool queried)
    {
        var cut = Render<FooterRowTableHost>(parameters => parameters
            .Add(p => p.Mode, mode)
            .Add(p => p.Queried, queried));

        Assert.Single(cut.FindAll("tfoot"));
        Assert.Equal(cut.FindAll("thead tr th").Count, cut.FindAll("tfoot td").Count);
    }

    [Fact]
    public void ATableThatDeclaresNoFooterRowRendersNoFoot()
    {
        var cut = Render<FooterRowTableHost>(parameters => parameters.Add(p => p.WithFooter, false));

        Assert.Empty(cut.FindAll("tfoot"));
    }

    // The two columns the TABLE adds to the header and to every row itself. The vendor's foot row
    // adds no cell of its own, so a table drawing either of them and leaving the foot alone would
    // shift every figure one column off its header.
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void TheTicksAndTheEditColumnKeepTheFootInStepWithTheHeader(bool selectable, bool editable)
    {
        var cut = Render<FooterRowTableHost>(parameters => parameters
            .Add(p => p.Selectable, selectable)
            .Add(p => p.Editable, editable));

        var header = cut.FindAll("thead tr th");
        var foot = cut.FindAll("tfoot td");

        Assert.Equal(header.Count, foot.Count);
        Assert.Equal(selectable, foot[0].ClassList.Contains("ns-select"));
        Assert.Equal(editable, foot[^1].ClassList.Contains("ns-actions"));
    }

    // Named and hidden by the column's own declaration, which is the whole point of the slot: the
    // figure's data-label is the word the header shows, and the breakpoint classes are the ones
    // NsTh put on that column — so the figure appears and disappears with the column it totals.
    [Fact]
    public void AFigureCarriesItsColumnsNameAndItsColumnsBreakpoint()
    {
        var cut = Render<FooterRowTableHost>();

        var header = cut.FindAll("thead tr th")[1];
        var figure = cut.FindAll("tfoot td")[1];

        Assert.Equal(header.TextContent.Trim(), figure.GetAttribute("data-label"));
        Assert.Contains("d-c-none", figure.ClassList);
        Assert.Contains("d-c-md-table-cell", figure.ClassList);
        Assert.Equal(Hides(header), Hides(figure));
    }

    // The cell with no field behind it is the row's own name, not a botonera: ns-actions carries
    // a shrink-to-fit width and a strip packed to the trailing edge, and "Totales" wearing them
    // would read as an action cell at every width.
    [Fact]
    public void TheCellThatNamesTheRowIsNotAnActionCell()
    {
        var cut = Render<FooterRowTableHost>();

        var name = cut.FindAll("tfoot td")[0];

        Assert.Equal("Totales", name.TextContent.Trim());
        Assert.Contains("ns-foot-label", name.ClassList);
        Assert.DoesNotContain("ns-actions", name.ClassList);
    }

    // A cell outside the foot keeps reading as the action cell from the same silence — the
    // cascade is what tells the two apart, so neither inference leaks into the other.
    [Fact]
    public void ARowsOwnUnnamedCellIsStillTheActionCell()
    {
        var cut = Render<FooterRowTableHost>(parameters => parameters.Add(p => p.Editable, true));

        var actions = cut.FindAll("tbody tr td")[^1];

        Assert.Contains("ns-actions", actions.ClassList);
        Assert.DoesNotContain("ns-foot-label", actions.ClassList);
    }

    // A column with nothing to total holds its place and draws nothing — empty is the condition
    // the stylesheet keys on to leave it out of the stacked card.
    [Fact]
    public void AColumnWithNothingToTotalHoldsItsPlaceAndIsEmpty()
    {
        var cut = Render<FooterRowTableHost>();

        var blank = cut.FindAll("tfoot td")[2];

        Assert.Equal(string.Empty, blank.TextContent.Trim());
        Assert.Equal("Doble", blank.GetAttribute("data-label"));
        Assert.Empty(blank.Children);
    }

    // The vendor hides its own tfoot at phone width together with the head row it stacks the
    // table by replacing (.mud-sm-table .mud-table-root .mud-table-foot { display: none }), so a
    // tfoot added and left alone would vanish at exactly the width the strip was filed against.
    [Fact]
    public void TheStylesheetTakesTheVendorsHiddenFootBack()
    {
        var block = Rule(ReadStylesheet(), ".ns-table.mud-sm-table .mud-table-root .mud-table-foot {");

        Assert.Contains("display: table-footer-group;", block, StringComparison.Ordinal);
    }

    [Fact]
    public void TheStylesheetLeavesAnEmptyFootCellOutOfTheStackedCard()
    {
        var block = Rule(ReadStylesheet(), ".ns-table.mud-sm-table .mud-table-foot .mud-table-cell:empty {");

        Assert.Contains("display: none;", block, StringComparison.Ordinal);
    }

    [Fact]
    public void TheStylesheetDropsTheEmptyLeadingPseudoOfTheRowsName()
    {
        var block = Rule(ReadStylesheet(), ".ns-table.mud-sm-table .ns-foot-label::before {");

        Assert.Contains("display: none;", block, StringComparison.Ordinal);
    }

    static string Hides(IElement cell)
    {
        return string.Join(' ', cell.ClassList.Where(name => name.StartsWith("d-c-", StringComparison.Ordinal)).Order());
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
