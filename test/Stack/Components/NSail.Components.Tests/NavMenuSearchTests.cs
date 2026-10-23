// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>What the ASK owes a module that offers results for the drawer's box (nsail#2590):
/// which terms are worth a round trip, what the term looks like when it arrives, and what one
/// module failing costs the others. The drawing is NsNavMenuSearchTests'; this is the policy
/// NavMenu holds so no contributor has to repeat it and no two of them can disagree.</summary>
public sealed class NavMenuSearchTests
{
    static NavMenu Menu(params INavMenuSearch[] searches)
    {
        return new NavMenu([], [], searches);
    }

    static NavMenuResult Found(string label)
    {
        return new NavMenuResult { Label = label, Url = "/rings/7" };
    }

    [Fact]
    public async Task EveryRegisteredModuleIsAskedInRegistrationOrder()
    {
        var first = new CountingNavSearch().Answers(Found("lumina@optical/canary"));
        var second = new CountingNavSearch().Answers(Found("lumina@therapy/prod1"));

        var results = await Menu(first, second).Find("lumina");

        Assert.Equal(
            ["lumina@optical/canary", "lumina@therapy/prod1"],
            results.Select(result => result.Label));
    }

    /// <summary>An app that registers none has the drawer it already had, which is the whole
    /// of "the seam is opt-in": there is nothing to ask and nothing to draw.</summary>
    [Fact]
    public async Task AnAppThatRegistersNoneAnswersNothing()
    {
        Assert.Empty(await Menu().Find("lumina"));
    }

    /// <summary>Two characters is the floor, and it lives here because the ask does. One letter
    /// names nothing a person is looking for, and it would be a round trip per settled keystroke
    /// coming back with whatever cap each module answers with.</summary>
    [Theory]
    [InlineData("l")]
    [InlineData(" l ")]
    [InlineData(" ")]
    [InlineData("")]
    [InlineData(null)]
    public async Task ATermUnderTwoCharactersAsksNobody(string? term)
    {
        var search = new CountingNavSearch().Answers(Found("lumina@optical/canary"));

        Assert.Empty(await Menu(search).Find(term));
        Assert.Empty(search.Terms);
    }

    /// <summary>The term arrives trimmed: it is a filter sent to somebody's server, where a
    /// leading space matches nothing. The BOX keeps what was typed — the no-match row is a
    /// question and padding is part of one — so the two part ways at the ask.</summary>
    [Fact]
    public async Task TheTermArrivesTrimmed()
    {
        var search = new CountingNavSearch().Answers(Found("lumina@optical/canary"));

        await Menu(search).Find("  lumina ");

        Assert.Equal("lumina", Assert.Single(search.Terms));
    }

    /// <summary>A module that cannot answer is left out rather than allowed to take the box
    /// down with it — the same bargain a count makes, and for the same reason: a drawer is
    /// chrome, and the map under it is not.</summary>
    [Fact]
    public async Task AModuleThatThrowsIsLeftOutAndTheOthersStillAnswer()
    {
        var failing = new CountingNavSearch().Fails();
        var answering = new CountingNavSearch().Answers(Found("lumina@optical/canary"));

        var results = await Menu(failing, answering).Find("lumina");

        Assert.Equal("lumina@optical/canary", Assert.Single(results).Label);
    }

    /// <summary>Nothing is remembered between terms: the term is the whole input, so the next
    /// one is a fresh ask of every module and never last answer's list filtered again.</summary>
    [Fact]
    public async Task EveryTermIsItsOwnAsk()
    {
        var search = new CountingNavSearch().Answers(Found("lumina@optical/canary"));
        var menu = Menu(search);

        await menu.Find("lu");
        await menu.Find("lumina");
        await menu.Find("lumina");

        Assert.Equal(["lu", "lumina", "lumina"], search.Terms);
    }
}
