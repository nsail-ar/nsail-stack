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

/// <summary>nsail#2696 — every money/quantity cell and header rendered text-align: start, so a
/// column of figures reads as a column of words: no digit lines up with the one above it. NsTh
/// and NsTd derive the alignment themselves, from the same Format a column already resolves for
/// export (TableColumn.FormatOf) — a screen declares neither, and a foot cell inherits it the way
/// it already inherits a breakpoint: by sharing its column's key (TableColumns).</summary>
public sealed class NsTableNumericColumnTests : BunitContext, IAsyncLifetime
{
    public NsTableNumericColumnTests()
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

    // Name is text, Count a plain Number, Total and Share declared Money/Percent, On a date: the
    // one CLR-derived kind and the two a screen states both read as figures, and a date's own
    // digits do not.
    [Fact]
    public void TextAndDateHeadersAreNotNumeric()
    {
        var cut = Render<ExportTableHost>();

        var headers = cut.FindAll("thead tr th");

        Assert.DoesNotContain("ns-col-numeric", headers[0].ClassList); // Name
        Assert.DoesNotContain("ns-col-numeric", headers[4].ClassList); // On
    }

    [Theory]
    [InlineData(1)] // Count: Number
    [InlineData(2)] // Total: Money
    [InlineData(3)] // Share: Percent
    public void NumberMoneyAndPercentHeadersAreNumeric(int column)
    {
        var cut = Render<ExportTableHost>();

        var header = cut.FindAll("thead tr th")[column];

        Assert.Contains("ns-col-numeric", header.ClassList);
    }

    [Fact]
    public void ACellCarriesTheSameAlignmentAsItsHeader()
    {
        var cut = Render<FooterRowTableHost>();

        var row = cut.FindAll("tbody tr")[0].QuerySelectorAll("td");

        Assert.DoesNotContain("ns-col-numeric", row[0].ClassList); // Name: text
        Assert.Contains("ns-col-numeric", row[1].ClassList); // Age: int
        Assert.DoesNotContain("ns-col-numeric", row[2].ClassList); // Doble: DataLabel only, no member
    }

    // The action cell has no For and no DataLabel: nothing to key on, so it never qualifies.
    [Fact]
    public void TheActionCellIsNeverNumeric()
    {
        var cut = Render<FooterRowTableHost>(parameters => parameters.Add(p => p.Editable, true));

        var action = cut.FindAll("tbody tr")[0].QuerySelectorAll("td")[^1];

        Assert.Contains("ns-actions", action.ClassList);
        Assert.DoesNotContain("ns-col-numeric", action.ClassList);
    }

    // A foot cell carries the SAME class its column's header does, off nothing but the key the
    // two already share — the mechanism FooterRow's own doctrine (NsTableFootTests) measures for
    // the breakpoint, here measured for alignment instead.
    [Fact]
    public void AFootFigureCarriesItsColumnsNumericAlignment()
    {
        var cut = Render<FooterRowTableHost>();

        var name = cut.FindAll("tfoot td")[0]; // Totales, no For
        var age = cut.FindAll("tfoot td")[1]; // For="() => total.Age", an int
        var doble = cut.FindAll("tfoot td")[2]; // DataLabel only, no member

        Assert.DoesNotContain("ns-col-numeric", name.ClassList);
        Assert.Contains("ns-col-numeric", age.ClassList);
        Assert.DoesNotContain("ns-col-numeric", doble.ClassList);
    }

    [Fact]
    public void TheStylesheetRightAlignsAndKeepsFiguresTabular()
    {
        var block = Rule(ReadStylesheet(), ".ns-table .ns-col-numeric {");

        Assert.Contains("text-align: end;", block, StringComparison.Ordinal);
        Assert.Contains("font-variant-numeric: tabular-nums;", block, StringComparison.Ordinal);
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
