// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;
using NSail.Components;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;
using System.Globalization;

namespace NSail.Components.Tests;

/// <summary>The guide screen's own contract: the chapter list arrives from DI and costs no
/// fetch, the open chapter's body is read from the file its module ships, the rail marks where
/// the reader is, and the sections under the open chapter are its own headings.</summary>
public sealed class NsGuideTests : BunitContext, IAsyncLifetime
{
    const string Assets = "_content/NSail.Components.Tests/guide/es";

    StaticAssets _assets = new(new Dictionary<string, string>());

    public NsGuideTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Guide.Accounting"] = "Contabilidad",
            ["Guide.Sales"] = "Ventas",
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(ProbeGuidePage).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        Services.AddScoped(provider => new GuideRoute(
            typeof(ProbeGuidePage),
            provider.GetRequiredService<RouteTable>()));
        Services.AddScoped<GuideReader>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider resolves a MudBlazor service that is IAsyncDisposable-only and
    // internal, so bUnit's synchronous teardown cannot dispose it.
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static GuideItem Chapter(string name, string file, int weight = 0)
    {
        return new GuideItem { Name = name, File = file, Weight = weight };
    }

    void Shelf(Dictionary<string, string> files, params GuideItem[] chapters)
    {
        _assets = new StaticAssets(files);

        Services.AddScoped(_ => new HttpClient(_assets));
        Services.AddScoped<IGuideContributor>(_ => new FixedGuideContributor(chapters));
    }

    IRenderedComponent<NsGuide> Open(string? chapter = null)
    {
        return Render<NsGuide>(parameters => parameters.Add(guide => guide.Chapter, chapter));
    }

    SurfaceContext Overlay()
    {
        return new SurfaceContext(
            Surfaces.Aside,
            Services.GetRequiredService<NavigationManager>(),
            Services.GetRequiredService<RouteTable>(),
            Services.GetRequiredService<IJSRuntime>(),
            new SurfaceHistory());
    }

    [Fact]
    public void TheRailListsEveryChapterInWeightOrder()
    {
        Shelf(
            new Dictionary<string, string> { [$"{Assets}/sales.md"] = "## Ventas" },
            Chapter("Accounting", "accounting", 30),
            Chapter("Sales", "sales", 10));

        var cut = Open();

        Assert.Equal(
            ["Ventas", "Contabilidad"],
            cut.FindAll(".ns-guide-rail .ns-guide-chapter").Select(entry => entry.TextContent));
    }

    [Fact]
    public void TheRailMarksTheOpenChapter()
    {
        Shelf(
            new Dictionary<string, string>(),
            Chapter("Sales", "sales", 10),
            Chapter("Accounting", "accounting", 30));

        var cut = Open("accounting");

        Assert.Equal(
            "Contabilidad",
            cut.Find(".ns-guide-rail .ns-guide-chapter.ns-guide-entry-current").TextContent);
    }

    [Fact]
    public void AChapterLinksToItsOwnAddress()
    {
        Shelf(new Dictionary<string, string>(), Chapter("Sales", "sales"));

        Assert.Equal(
            "/guide/sales",
            Open().Find(".ns-guide-rail .ns-guide-chapter").GetAttribute("href"));
    }

    [Fact]
    public void TheBodyIsReadFromTheFileTheModuleShipsAndDrawnAsHtml()
    {
        Shelf(
            new Dictionary<string, string>
            {
                [$"{Assets}/sales.md"] = "## Registrar una Venta\n\n- **Diario** muestra los asientos.",
            },
            Chapter("Sales", "sales"));

        var cut = Open();

        cut.WaitForAssertion(() =>
            Assert.Equal("Registrar una Venta", cut.Find(".ns-guide-body h2").TextContent));

        Assert.Equal("registrar-una-venta", cut.Find(".ns-guide-body h2").GetAttribute("id"));
        Assert.Equal("Diario", cut.Find(".ns-guide-body ul li strong").TextContent);
        Assert.DoesNotContain("##", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TheOpenChapterSectionsAreItsOwnHeadings()
    {
        Shelf(
            new Dictionary<string, string>
            {
                [$"{Assets}/accounting.md"] = "## Cobrar\n\nTexto.\n\n### El dólar\n\n## Libros",
            },
            Chapter("Sales", "sales", 10),
            Chapter("Accounting", "accounting", 30));

        var cut = Open("accounting");

        cut.WaitForAssertion(() => Assert.Equal(
            ["Cobrar", "Libros"],
            cut.FindAll(".ns-guide-rail .ns-guide-section").Select(entry => entry.TextContent)));

        Assert.Equal(
            "/guide/accounting#cobrar",
            cut.FindAll(".ns-guide-rail .ns-guide-section")[0].GetAttribute("href"));
    }

    // Only what the reader opened travels: the chapter list is the contributors' answer and
    // costs nothing, and the chapter beside the open one is never asked for.
    [Fact]
    public void OnlyTheOpenChapterIsFetched()
    {
        Shelf(
            new Dictionary<string, string>
            {
                [$"{Assets}/sales.md"] = "## Ventas",
                [$"{Assets}/accounting.md"] = "## Contabilidad",
            },
            Chapter("Sales", "sales", 10),
            Chapter("Accounting", "accounting", 30));

        var cut = Open("accounting");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ns-guide-body h2")));

        Assert.All(_assets.Requested, path => Assert.EndsWith("/accounting.md", path, StringComparison.Ordinal));
    }

    // A module writes its guide in one language and the reader asks for theirs first: what it
    // never translated is read in the language its owner wrote it in, at the cost of the miss.
    [Fact]
    public void AChapterFallsBackToTheLanguageItsOwnerWroteItIn()
    {
        Shelf(
            new Dictionary<string, string> { [$"{Assets}/sales.md"] = "## Ventas" },
            Chapter("Sales", "sales"));

        var culture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en");

            var cut = Open();

            cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ns-guide-body h2")));

            Assert.Equal(
                ["_content/NSail.Components.Tests/guide/en/sales.md", $"{Assets}/sales.md"],
                _assets.Requested);
        }
        finally
        {
            CultureInfo.CurrentUICulture = culture;
        }
    }

    // The switch, not the first paint: the rail marks the new chapter the moment the address
    // changes, and the body under it must not be the chapter the reader just left for as long as
    // the fetch takes.
    [Fact]
    public void SwitchingChapterNeverLeavesThePreviousChaptersBodyUnderTheNewOne()
    {
        Shelf(
            new Dictionary<string, string>
            {
                [$"{Assets}/sales.md"] = "## Registrar una Venta",
                [$"{Assets}/accounting.md"] = "## Cobrar",
            },
            Chapter("Sales", "sales", 10),
            Chapter("Accounting", "accounting", 30));

        var cut = Open("sales");

        cut.WaitForAssertion(() =>
            Assert.Equal("Registrar una Venta", cut.Find(".ns-guide-body h2").TextContent));

        _assets.Hold();

        cut.Render(parameters => parameters.Add(guide => guide.Chapter, "accounting"));

        Assert.Equal(
            "Contabilidad",
            cut.Find(".ns-guide-rail .ns-guide-chapter.ns-guide-entry-current").TextContent);

        Assert.Empty(cut.FindAll(".ns-guide-body h2"));

        _assets.Release();

        cut.WaitForAssertion(() =>
            Assert.Equal("Cobrar", cut.Find(".ns-guide-body h2").TextContent));
    }

    [Fact]
    public void AChapterNoModuleShippedSaysSoInsteadOfShowingNothing()
    {
        Shelf(new Dictionary<string, string>(), Chapter("Sales", "sales"));

        var cut = Open();

        cut.WaitForAssertion(() =>
            Assert.Contains("Common.GuideMissing", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void ASectionTitleReadsFromTheStringsUnderItsOwnKey()
    {
        Shelf(new Dictionary<string, string>(), Chapter("Accounting", "accounting"));

        Assert.Contains("Contabilidad", Open().Markup, StringComparison.Ordinal);
    }

    // The aside a guide button opens has the frame's drawer behind it and out of reach, and no
    // width for a second column: the switcher is its whole index, and nothing is handed to a
    // chrome that would draw it somewhere the reader cannot see.
    [Fact]
    public void AnOverlayGetsTheSwitcherAndHandsTheFrameNothing()
    {
        Shelf(
            new Dictionary<string, string> { [$"{Assets}/accounting.md"] = "## Cobrar" },
            Chapter("Sales", "sales", 10),
            Chapter("Accounting", "accounting", 30));

        var surface = Overlay();

        var cut = Render<NsGuide>(parameters => parameters
            .AddCascadingValue(surface)
            .Add(guide => guide.Chapter, "accounting"));

        Assert.NotEmpty(cut.FindAll(".ns-guide-index"));
        Assert.Empty(cut.FindAll(".ns-guide-rail"));
        Assert.Null(surface.Index);
    }

    // An app that composed no module carrying a guide draws no chapter at all — the screen
    // is the composition's answer, never a catalog it filters.
    [Fact]
    public void NothingRendersWhenNobodyContributed()
    {
        Shelf(new Dictionary<string, string>());

        Assert.Empty(Open().FindAll(".ns-guide"));
    }
}
