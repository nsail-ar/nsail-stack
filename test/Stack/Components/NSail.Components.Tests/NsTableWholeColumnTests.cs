// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#2999 — a code such as ARM-TEST-01 broke at its hyphens once an aside narrowed
/// the list. A column says Whole on its header; the cells follow through the column's key, and the
/// stylesheet keeps the value on one line.</summary>
public sealed class NsTableWholeColumnTests : BunitContext, IAsyncLifetime
{
    public NsTableWholeColumnTests()
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

    [Fact]
    public void TheCellsCarryTheMarkAndTheHeadingStillWraps()
    {
        var cut = Render<WholeColumnTableHost>();

        Assert.DoesNotContain("ns-col-whole", cut.FindAll("thead tr th")[0].ClassList);
        Assert.Contains("ns-col-whole", cut.FindAll("tbody tr")[0].QuerySelectorAll("td")[0].ClassList);
    }

    [Fact]
    public void AComputedColumnSaysItOnItsOwnCell()
    {
        var cut = Render<WholeColumnTableHost>();

        Assert.Contains("ns-col-whole", cut.FindAll("tbody tr")[0].QuerySelectorAll("td")[2].ClassList);
    }

    [Fact]
    public void AnotherColumnIsLeftToWrap()
    {
        var cut = Render<WholeColumnTableHost>();

        Assert.DoesNotContain("ns-col-whole", cut.FindAll("tbody tr")[0].QuerySelectorAll("td")[1].ClassList);
    }

    [Fact]
    public void TheStylesheetKeepsTheValueOnOneLine()
    {
        var css = HouseStylesheet.Read();
        var start = css.IndexOf(".ns-table .ns-col-whole {", StringComparison.Ordinal);
        Assert.True(start >= 0, "the Whole rule was not found in the stylesheet.");

        var block = css[start..css.IndexOf('}', start)];

        Assert.Contains("white-space: nowrap;", block, StringComparison.Ordinal);
    }

    [Fact]
    public void TheStackedLabelStillWraps()
    {
        var block = Rule(HouseStylesheet.Read(), ".ns-table.mud-sm-table .ns-col-whole::before {");

        Assert.Contains("white-space: normal;", block, StringComparison.Ordinal);
    }

    [Fact]
    public void TheContainersStackedLabelStillWraps()
    {
        const string Query = "@media (min-width: 960px) {\n    @container (max-width: 739.98px) {\n";
        const string Selector = ".ns-table.mud-sm-table .ns-col-whole::before {";
        var css = HouseStylesheet.Read();

        var at = css.LastIndexOf(Selector, StringComparison.Ordinal);
        var query = css.IndexOf(Query, StringComparison.Ordinal);
        Assert.True(at > query && query >= 0, "the label rule is not inside the container's stacking query.");
        Assert.True(css.IndexOf("\n}\n", query, StringComparison.Ordinal) > at,
            "the label rule is not inside the container's stacking query.");
        Assert.Contains("white-space: normal;", Rule(css[at..], Selector), StringComparison.Ordinal);
    }

    [Fact]
    public void ATableWithNoWholeColumnHasNothingForTheGridToReserve()
    {
        var cut = Render<StackedBreakpointHost>();

        Assert.Empty(cut.FindAll(".ns-col-whole"));
    }

    [Fact]
    public void TheGridShedsTheWidestBreakpointFirstAndNeverTheOthers()
    {
        var script = ReadScript();
        var start = script.IndexOf("function fitColumns(root)", StringComparison.Ordinal);
        Assert.True(start >= 0, "fitColumns was not found in ns.js.");
        var body = script[start..script.IndexOf("function fitTables()", start, StringComparison.Ordinal)];

        Assert.Contains("td.ns-col-whole", body, StringComparison.Ordinal);
        Assert.Contains("b.rung - a.rung", body, StringComparison.Ordinal);
        Assert.Contains("rung >= 0", body, StringComparison.Ordinal);
        Assert.Contains("scroller.scrollWidth > scroller.clientWidth", body, StringComparison.Ordinal);
        Assert.Contains("[\"sm\", \"md\", \"lg\", \"xl\"]", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AShedColumnIsHiddenAboveTheBreakpointRungs()
    {
        var block = Rule(HouseStylesheet.Read(), ".ns-table .mud-table-root .mud-table-cell[data-ns-shed] {");

        Assert.Contains("display: none;", block, StringComparison.Ordinal);
    }

    static string ReadScript()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return File.ReadAllText(Path.Combine(directory.FullName, "src/Stack/BaseServices/NSail.BaseServices.WebApp/wwwroot/ns.js"))
            .ReplaceLineEndings("\n");
    }

    static string Rule(string css, string selector)
    {
        var start = css.IndexOf(selector, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{selector}' was not found in the stylesheet.");

        var end = css.IndexOf('}', start);
        Assert.True(end >= 0, $"'{selector}' has no closing brace.");

        return css[start..end];
    }
}
