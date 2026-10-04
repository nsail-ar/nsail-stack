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

/// <summary>nsail#1027: a lookup's open list is sized by its own results, floored at the field it
/// drops from and bounded by a measure — never by the box the field stands in. MudBlazor's default
/// (DropdownWidth.Relative) writes max-width = the anchor's width inline on every open, and in a
/// line editor the anchor is a table cell, so an option read as three lines of a narrow column.
/// <para>What a render can prove here is the word the field hands the vendor; the geometry is an
/// inline style a browser's JS writes and is measured in NSail.Optical.E2E
/// (LookupListWidthTests). The rules that bound it are read off the shipped stylesheet, the
/// CardIconRankTests idiom.</para></summary>
public sealed class NsLookupListWidthTests : BunitContext, IAsyncLifetime
{
    public NsLookupListWidthTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsLookupListWidthTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider resolves a MudBlazor service that is IAsyncDisposable-only, so bUnit's
    // synchronous teardown cannot dispose it (NsAutocompleteSearchTests' own note).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    [Fact]
    public void TheFieldAsksTheVendorToFloorTheListAtItInsteadOfCappingItThere()
    {
        var host = Render<SearchLookupHost>(p => p
            .Add(x => x.Rows, [new SelectRef(Guid.NewGuid(), "Caja en pesos")])
            .Add(x => x.Lazy, false));

        var field = host.FindComponent<MudAutocomplete<SelectRef>>();

        // Adaptive is min-width = the anchor; Relative, the vendor's default, is max-width = the
        // anchor and is the defect. The enum stays inside this project — no Ns* API names it.
        Assert.Equal(DropdownWidth.Adaptive, field.Instance.RelativeWidth);
    }

    [Fact]
    public void TheOpenListsRowStopsAtAMeasureRatherThanAtTheFieldsBox()
    {
        Assert.Contains(
            """
            .ns-lookup-list .mud-list-item-text {
                max-width: 28rem;
            }
            """,
            ReadStylesheet());
    }

    [Fact]
    public void AGrowingLookupsCellDemandsTheGrowBasisFromMdUpAndAsksForNothingBelowIt()
    {
        var css = ReadStylesheet();

        // From md up the basis is a DEMAND, which the widest line editor in the tree still clears
        // at the smallest container past the gate, and which keeps the column growing past the
        // basis with the surplus.
        Assert.Contains(
            """
            @container (min-width: 960px) {
                .ns-table .mud-table-cell:has(.mud-autocomplete .mud-input-control.flex-1) {
                    min-width: var(--ns-field-grow-basis, 12rem);
                }
            }
            """,
            css);

        // One demanded selector under the grid and no other: a floor under every field in the
        // row spends width on columns that never needed it, and a minimum the row cannot pay for
        // is added to the table rather than redistributed inside it — which is what demanding the
        // basis in the comprobante's eight-column row costs at a 712px container, 92px more of
        // the overflow that already cuts its confirm.
        Assert.Equal(
            [".ns-table .mud-table-cell:has(.mud-autocomplete .mud-input-control.flex-1)"],
            Sized(css, ".ns-table", "min-width"));

        // And BELOW md no cell in a grid asks for a width at all. nsail#1242 handed a narrow row
        // the basis as a preference there — the most a column could take out of a row's own
        // surplus — and nsail#1614 ended the arithmetic instead: under md the open row stops
        // being a row of columns and every field takes its own card line, 654px of the same
        // 688px aside the preference could only find 192 in. A rule left behind to decide
        // nothing is the drift this list exists to catch.
        Assert.Empty(Sized(css, ".ns-table", "width"));
    }

    [Fact]
    public void TheFloorKeysOnTheClassAGrowingFieldRenders()
    {
        // One host, told twice: MudPopoverProvider subscribes a section by a fixed id, so a
        // second render of the fixture in one test throws before any assertion runs.
        var host = Render<SearchLookupHost>(p => p
            .Add(x => x.Rows, [new SelectRef(Guid.NewGuid(), "Caja en pesos")])
            .Add(x => x.Lazy, false)
            .Add(x => x.Grow, true));

        Assert.NotEmpty(host.FindAll(".mud-autocomplete .mud-input-control.flex-1"));

        host.Render(p => p.Add(x => x.Grow, false));

        // And on nothing else: the declaration is the whole difference the selector above reads.
        // Undeclared is not the same as needing no floor — a Cuenta's reading is composed at
        // every AccountSelect, yet only the two line-editor callers that ask for it declare
        // Grow (nsail#1171); the rest default it false the same as SearchLookupHost above.
        Assert.Empty(host.FindAll(".mud-autocomplete .mud-input-control.flex-1"));
    }

    /// <summary>Every rule under <paramref name="scope"/> that sizes a cell through
    /// <paramref name="property"/>, by selector. Read as selector-block pairs rather than by
    /// splitting on the braces: a rule nested in an at-rule carries its own, and the innermost
    /// block is the one with a declaration in it. The property is matched at its own start, so
    /// "width" does not answer for "min-width" — the two words are the whole subject here.</summary>
    static string[] Sized(string css, string scope, string property)
    {
        var rules = System.Text.RegularExpressions.Regex.Replace(css, @"/\*.*?\*/", "", System.Text.RegularExpressions.RegexOptions.Singleline);

        return [.. System.Text.RegularExpressions.Regex.Matches(rules, @"([^{}]+)\{([^{}]*)\}")
            .Where(rule => rule.Groups[1].Value.Trim().StartsWith(scope, StringComparison.Ordinal)
                && rule.Groups[1].Value.Contains("table-cell", StringComparison.Ordinal)
                && System.Text.RegularExpressions.Regex.IsMatch(rule.Groups[2].Value, $@"(^|[;\s]){property}\s*:"))
            .Select(rule => rule.Groups[1].Value.Trim())];
    }

    static string ReadStylesheet()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
            {
                return File.ReadAllText(Path.Combine(
                    directory.FullName,
                    "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css"));
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"NSail.sln was not found above {AppContext.BaseDirectory}.");
    }
}
