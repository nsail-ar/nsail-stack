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

[Route("/search/parties")]
public sealed class SearchPartiesProbePage : ComponentBase;

/// <summary>The third seam into the drawer's box (nsail#2590): the map is the list of where a
/// session may GO, and INavMenuSearch is what a module offers for what a session is looking
/// FOR. The results sit UNDER whatever the typing left of the map, marked as entries, so the
/// one Enter policy in ns.js keeps meaning the menu where the menu answered and means the first
/// result where it did not.
///
/// <para>What the drawer owes the seam is here: the rate (one ask per settled term, nothing
/// under two characters), the staleness (an answer belongs to the term that asked for it), and
/// the failure (a search is chrome and the map is not). How MANY results and who may see them
/// are the contributor's, and are not.</para></summary>
public sealed class NsNavMenuSearchTests : BunitContext, IAsyncLifetime
{
    readonly CountingNavSearch _search = new();

    public NsNavMenuSearchTests()
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
        Services.AddSingleton(new RouteTable(typeof(NsNavMenuSearchTests).Assembly, []));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<INavMenuContributor>(new FixedNavContributor(
            new NavMenuItem { Name = "Parties", PageType = typeof(SearchPartiesProbePage) }));
        Services.AddSingleton<INavMenuSearch>(_search);
        JSInterop.Mode = JSRuntimeMode.Loose;

        AddAuthorization().SetAuthorized("search-probe");
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

    static NavMenuResult Tenant(string label, string detail = "Lumina SRL")
    {
        return new NavMenuResult
        {
            Label = label,
            Detail = detail,
            Url = "/rings/7?aside=tenant"
        };
    }

    static async Task Type(IRenderedComponent<NsNavMenu> menu, string text)
    {
        await menu.InvokeAsync(() => menu.Find(".ns-nav-find .mud-input-slot").Input(text));
    }

    static void Waits(IRenderedComponent<NsNavMenu> menu, Action assertion)
    {
        menu.WaitForAssertion(assertion, TimeSpan.FromSeconds(10));
    }

    // The settle point for everything asserted as an ABSENCE: the box debounces, so a drawer
    // read straight after the keystroke is the drawer before the term was ever asked about —
    // which is the shape every one of these assertions would pass against. The ask having
    // answered is the only honest "and then nothing happened".
    async Task Settled(IRenderedComponent<NsNavMenu> menu)
    {
        await _search.Answered;

        await menu.InvokeAsync(() => { });
        await menu.InvokeAsync(() => { });
    }

    IRenderedComponent<NsNavMenu> RenderWithNoMatch()
    {
        return Render<NsNavMenu>(p => p
            .Add(x => x.NoMatch, typed => $"<button class=\"probe-ask\">{typed}</button>"));
    }

    /// <summary>What the seam is for: a term the map cannot answer is still answered, by
    /// whoever holds the records.</summary>
    [Fact]
    public async Task AModulesResultsAreDrawnForATermTheMapDoesNotMatch()
    {
        _search.Answers(Tenant("lumina@optical/canary"));

        var menu = Render<NsNavMenu>();

        await Type(menu, "lumina");

        Waits(
            menu,
            () =>
            {
                var row = menu.Find(".ns-nav-result a[href]");

                Assert.Equal("/rings/7?aside=tenant", row.GetAttribute("href"));
                Assert.Contains("lumina@optical/canary", row.TextContent);
            });
    }

    /// <summary>UNDER the map's matches, which is the whole of ruling 2: the first
    /// .ns-nav-entry the Enter policy finds is the menu's where the menu answered, and the
    /// result only where it did not.</summary>
    [Fact]
    public async Task ATermMatchingAnEntryDrawsBothWithTheEntryFirst()
    {
        _search.Answers(Tenant("parties@optical/canary"));

        var menu = Render<NsNavMenu>();

        await Type(menu, "part");

        Waits(
            menu,
            () =>
            {
                var rows = menu.FindAll(".ns-nav-entry a[href]");

                Assert.Equal(2, rows.Count);
                Assert.Equal("/search/parties", rows[0].GetAttribute("href"));
                Assert.Equal("/rings/7?aside=tenant", rows[1].GetAttribute("href"));
            });
    }

    /// <summary>A result is pressed by the key that presses an entry, so it wears the mark that
    /// key reads (ns.js). bUnit runs none of that script — what is held here is the hook, so the
    /// day the row stops carrying it the failure has a name instead of being an Enter that goes
    /// to the wrong place.</summary>
    [Fact]
    public async Task AResultCarriesTheHookEntersPolicyPressesItBy()
    {
        _search.Answers(Tenant("lumina@optical/canary"));

        var menu = Render<NsNavMenu>();

        await Type(menu, "lumina");

        // The entry the term left is the result and nothing else, so what Enter reaches is it.
        Waits(
            menu,
            () => Assert.Equal(
                "/rings/7?aside=tenant",
                Assert.Single(menu.FindAll(".ns-nav-entry a[href]")).GetAttribute("href")));
    }

    /// <summary>The second line is what tells two records wearing one label apart, and it is
    /// part of the row rather than a second thing to aim at.</summary>
    [Fact]
    public async Task AResultDrawsWhatItAnswersToUnderItsOwnLabel()
    {
        _search.Answers(Tenant("lumina@optical/canary", "Óptica Lumina"));

        var menu = Render<NsNavMenu>();

        await Type(menu, "lumina");

        Waits(menu, () => Assert.Equal("Óptica Lumina", menu.Find(".ns-nav-result .ns-nav-detail").TextContent));
    }

    /// <summary>A result that has nothing to add draws one line: an empty caption would be a
    /// row of blank space under every label.</summary>
    [Fact]
    public async Task AResultWithNothingToAddDrawsOneLine()
    {
        _search.Answers(new NavMenuResult { Label = "lumina@optical/canary", Url = "/rings/7" });

        var menu = Render<NsNavMenu>();

        await Type(menu, "lumina");

        Waits(
            menu,
            () =>
            {
                Assert.Single(menu.FindAll(".ns-nav-result"));
                Assert.Empty(menu.FindAll(".ns-nav-detail"));
            });
    }

    /// <summary>The results answer the term and nothing else: clearing the box is the map
    /// back, with no record of a search left standing in it.</summary>
    [Fact]
    public async Task ClearingTheBoxTakesTheResultsWithIt()
    {
        _search.Answers(Tenant("lumina@optical/canary"));

        var menu = Render<NsNavMenu>();

        await Type(menu, "lumina");

        Waits(menu, () => Assert.Single(menu.FindAll(".ns-nav-result")));

        await Type(menu, string.Empty);

        Waits(
            menu,
            () =>
            {
                Assert.Empty(menu.FindAll(".ns-nav-result"));
                Assert.Single(menu.FindAll("a[href='/search/parties']"));
            });
    }

    /// <summary>Ruling 3: a drawer showing the record it found is not a drawer that found
    /// nothing, so the app's way on with the question is not offered over it.</summary>
    [Fact]
    public async Task AResultStandsInFrontOfTheNoMatchRow()
    {
        _search.Answers(Tenant("lumina@optical/canary"));

        var menu = RenderWithNoMatch();

        await Type(menu, "lumina");

        Waits(
            menu,
            () =>
            {
                Assert.Single(menu.FindAll(".ns-nav-result"));
                Assert.Empty(menu.FindAll(".ns-nav-nomatch"));
            });
    }

    /// <summary>And the other side of the same condition: with neither an entry nor a result
    /// left, the row is what the drawer has to offer — unchanged for every app that registers
    /// no search at all.</summary>
    [Fact]
    public async Task WithNeitherAnEntryNorAResultTheNoMatchRowIsDrawn()
    {
        var menu = RenderWithNoMatch();

        await Type(menu, "cuanto sale un multifocal");

        Waits(
            menu,
            () =>
            {
                Assert.Single(menu.FindAll(".ns-nav-nomatch button"));
                Assert.Empty(menu.FindAll(".ns-nav-entry"));
            });
    }

    /// <summary>A search is chrome and the map is not: what failed to arrive is drawn as
    /// nothing, never as an error over the entries the person can still use.</summary>
    [Fact]
    public async Task ASearchThatThrowsLeavesTheEntriesStanding()
    {
        _search.Answers(Tenant("lumina@optical/canary")).Fails();

        var menu = Render<NsNavMenu>();

        await Type(menu, "part");

        await Settled(menu);

        Assert.Single(menu.FindAll("a[href='/search/parties']"));
        Assert.Empty(menu.FindAll(".ns-nav-result"));
    }

    /// <summary>The unit is the ask and the number is one (nsail#795): the box hands over a
    /// term it already debounced, so one settled term is one round trip per module.</summary>
    [Fact]
    public async Task ASettledTermCostsOneAsk()
    {
        _search.Answers(Tenant("lumina@optical/canary"));

        var menu = Render<NsNavMenu>();

        await Type(menu, "lumina");

        Waits(menu, () => Assert.Single(menu.FindAll(".ns-nav-result")));

        Assert.Equal("lumina", Assert.Single(_search.Terms));
    }

    /// <summary>One letter asks nothing. It names nothing a person was looking for, and it
    /// would be a round trip per settled keystroke answering with the contributor's whole cap
    /// every time.</summary>
    [Fact]
    public async Task ASingleLetterAsksNobody()
    {
        var menu = Render<NsNavMenu>();

        await Type(menu, "l");

        // The term settling is visible without an ask: it is what takes the one entry out of the
        // drawer, since no label holds that letter.
        Waits(menu, () => Assert.Empty(menu.FindAll(".ns-nav-entry")));

        Assert.Empty(_search.Terms);
    }

    /// <summary>The term reaches the module trimmed: it is a filter sent to a server, where a
    /// leading space matches nothing. The box itself keeps what was typed — the no-match row is
    /// a question, and padding is part of one.</summary>
    [Fact]
    public async Task TheTermAModuleIsAskedAboutIsTrimmed()
    {
        _search.Answers(Tenant("lumina@optical/canary"));

        var menu = Render<NsNavMenu>();

        await Type(menu, "  lumina ");

        Waits(menu, () => Assert.Single(menu.FindAll(".ns-nav-result")));

        Assert.Equal("lumina", Assert.Single(_search.Terms));
    }

    /// <summary>An answer belongs to the term that asked for it. A slower module replying after
    /// the box was cleared would otherwise draw a result for a term nobody is holding any
    /// more — the shape of every stale answer, and the one the clear makes visible.</summary>
    [Fact]
    public async Task AnAnswerArrivingAfterTheBoxMovedOnIsNotDrawn()
    {
        _search.Answers(Tenant("lumina@optical/canary")).Holds();

        var menu = Render<NsNavMenu>();

        await Type(menu, "lumina");

        Waits(menu, () => Assert.Equal("lumina", Assert.Single(_search.Terms)));

        await Type(menu, string.Empty);

        // The clear reaches the drawer first — the map is back — and only then does the module
        // answer the term that is gone.
        Waits(menu, () => Assert.Single(menu.FindAll("a[href='/search/parties']")));

        _search.Release();

        await Settled(menu);

        Assert.Empty(menu.FindAll(".ns-nav-result"));
        Assert.Single(menu.FindAll("a[href='/search/parties']"));
    }

    /// <summary>The seam is opt-in in both directions: a module that found nothing for the term
    /// leaves the drawer exactly as the map's own filter left it.</summary>
    [Fact]
    public async Task AModuleThatFoundNothingChangesNothing()
    {
        var menu = Render<NsNavMenu>();

        await Type(menu, "part");

        await Settled(menu);

        Assert.Single(menu.FindAll("a[href='/search/parties']"));
        Assert.Empty(menu.FindAll(".ns-nav-result"));
    }
}
