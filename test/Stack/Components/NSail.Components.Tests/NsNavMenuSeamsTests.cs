// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

[Route("/seams/parties")]
public sealed class SeamsPartiesProbePage : ComponentBase;

/// <summary>The two seams an app reaches the drawer's own header and its empty result through
/// (nsail#2420): Tools, where a door of the app's stands beside the search box and the frame's
/// "?", and NoMatch, the one entry drawn where typing filtered the map to nothing — given the
/// text, so the app can carry the question on somewhere else.
///
/// <para>That entry is pressed by the same Enter the first entry is, and the keystroke itself is
/// ns.js's (NsEnterPolicyMarkTests' own split: bUnit renders through AngleSharp and runs none of
/// it). What is held here is the MARK that policy reads — the day the row stops carrying it, the
/// failure has a name instead of being an Enter that does nothing.</para></summary>
public sealed class NsNavMenuSeamsTests : BunitContext, IAsyncLifetime
{
    public NsNavMenuSeamsTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        // The drawer opens the "?" sheet through the manager (nsail#2207), so a harness that
        // renders it composes one.
        Services.AddSingleton<DialogManager>(new CountingDialogManager());
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsNavMenuSeamsTests).Assembly, []));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<INavMenuContributor>(new FixedNavContributor(
            new NavMenuItem { Name = "Parties", PageType = typeof(SeamsPartiesProbePage) }));
        JSInterop.Mode = JSRuntimeMode.Loose;

        AddAuthorization().SetAuthorized("seams-probe");
    }

    // The drawer draws a tooltip (the "?" door), and the vendor's popover service behind one is
    // IAsyncDisposable only — a synchronous dispose of the container throws on it.
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static async Task Type(IRenderedComponent<NsNavMenu> menu, string text)
    {
        await menu.InvokeAsync(() => menu.Find(".ns-nav-find .mud-input-slot").Input(text));
    }

    [Fact]
    public void ADoorInToolsStandsOnTheHeaderRowBesideTheSearchBox()
    {
        var menu = Render<NsNavMenu>(p => p
            .Add(x => x.Tools, "<button class=\"probe-tool\">Asistente</button>"));

        Assert.Single(menu.FindAll(".ns-nav-find .probe-tool"));
    }

    /// <summary>An app that places no door leaves the row as it was — the box and what the frame
    /// itself owns, with nothing drawn for an empty slot.</summary>
    [Fact]
    public void AnEmptyToolsSlotDrawsNothing()
    {
        var menu = Render<NsNavMenu>();

        Assert.Empty(menu.FindAll(".probe-tool"));
        Assert.Single(menu.FindAll(".ns-nav-find input"));
    }

    /// <summary>The map while it still matches: the entry is what is drawn, and the app's way on
    /// is not offered for a search that found the door it was looking for. NsSearchField
    /// debounces, so the assertion waits for the filter rather than reading the DOM straight
    /// after the keystroke.</summary>
    [Fact]
    public async Task WithSomethingStillMatchingTheEntriesAreWhatIsDrawn()
    {
        var menu = RenderWithNoMatch();

        await Type(menu, "part");

        menu.WaitForAssertion(
            () =>
            {
                Assert.Single(menu.FindAll("a[href='/seams/parties']"));
                Assert.Empty(menu.FindAll(".ns-nav-nomatch"));
            },
            TimeSpan.FromSeconds(10));
    }

    /// <summary>Filtered to nothing, the app's entry is the ONE thing in the list — nothing above
    /// it to press first — and it is given the text that found nothing.</summary>
    [Fact]
    public async Task FilteredToNothingTheAppsEntryIsTheOneEntryAndCarriesTheTypedText()
    {
        var menu = RenderWithNoMatch();

        await Type(menu, "cuanto sale un multifocal");

        menu.WaitForAssertion(
            () =>
            {
                var row = menu.Find(".ns-nav-nomatch");

                Assert.Equal("cuanto sale un multifocal", row.QuerySelector("button")!.TextContent);
                Assert.Empty(menu.FindAll(".ns-nav-entry"));
            },
            TimeSpan.FromSeconds(10));
    }

    /// <summary>The mark the Enter policy reads: the row's own control is what the keystroke
    /// presses where the filter left no entry, exactly as it presses the first entry's link
    /// otherwise (ns.js). A button as much as a link — taking a question somewhere is not
    /// navigating — so what is pinned is the hook around it.</summary>
    [Fact]
    public async Task TheRowCarriesTheHookEntersPolicyPressesItBy()
    {
        var menu = RenderWithNoMatch();

        await Type(menu, "sin coincidencias");

        menu.WaitForAssertion(
            () => Assert.Single(menu.FindAll(".ns-nav-nomatch button")),
            TimeSpan.FromSeconds(10));
    }

    /// <summary>An app that offers nothing for a question keeps today's empty list: the seam is
    /// opt-in, and a drawer that answers nothing says nothing.</summary>
    [Fact]
    public async Task AnAppThatOffersNoWayOnKeepsTheEmptyList()
    {
        var menu = Render<NsNavMenu>();

        await Type(menu, "sin coincidencias");

        menu.WaitForAssertion(
            () =>
            {
                Assert.Empty(menu.FindAll(".ns-nav-entry"));
                Assert.Empty(menu.FindAll(".ns-nav-nomatch"));
            },
            TimeSpan.FromSeconds(10));
    }

    IRenderedComponent<NsNavMenu> RenderWithNoMatch()
    {
        return Render<NsNavMenu>(p => p
            .Add(x => x.NoMatch, typed => $"<button class=\"probe-ask\">{typed}</button>"));
    }
}
