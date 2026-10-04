// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

[Route("/navmenu/asked")]
public sealed class AskedProbePage : ComponentBase;

/// <summary>The seam nsail#795 put between the contributors and everyone who wants the tree.
/// A contributor is a module's chance to ask its own server, and the drawer is not the only
/// thing that wants a menu — every title bar derives its page's glyph from the same tree, and
/// a screen draws several. What this pins is the ask, not the items: how many times a kit is
/// made to answer, and what still earns a fresh answer.</summary>
public sealed class NavMenuTests
{
    static readonly NavMenuItem Entry = new() { Name = "Parties", PageType = typeof(AskedProbePage) };

    // An install that arranged nothing, which is the shape of the ask this file is about: what
    // the contributors say, asked once.
    static NavMenu Menu(INavMenuContributor contributor)
    {
        return new NavMenu([contributor], []);
    }

    static Task<AuthenticationState> Session()
    {
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Fact]
    public async Task EveryCallerAfterTheFirstSharesTheOneAsk()
    {
        var contributor = new CountingNavContributor(Entry);
        var menu = Menu(contributor);
        var session = Session();

        for (var call = 0; call < 20; call++)
        {
            Assert.Single(await menu.GetItems(session));
        }

        Assert.Equal(1, contributor.Calls);
    }

    /// <summary>The session is the one thing a contributor may answer differently for — a
    /// sign-in, an organization switch — so it is the one thing that earns a fresh ask.</summary>
    [Fact]
    public async Task ADifferentSessionEarnsAFreshAsk()
    {
        var contributor = new CountingNavContributor(Entry);
        var menu = Menu(contributor);

        await menu.GetItems(Session());
        await menu.GetItems(Session());

        Assert.Equal(2, contributor.Calls);
    }

    /// <summary>A screen renders its drawer and its title bars in one pass, so the overlapping
    /// ask is the ordinary case rather than the exotic one: they share the ask in flight instead
    /// of each starting their own.</summary>
    [Fact]
    public async Task CallersOverlappingTheFirstAskJoinItRatherThanStartingTheirOwn()
    {
        var contributor = new HeldNavContributor(Entry);
        var menu = Menu(contributor);
        var session = Session();

        var callers = Enumerable.Range(0, 10).Select(_ => menu.GetItems(session)).ToList();

        Assert.Equal(1, contributor.Calls);

        contributor.Answer();

        foreach (var caller in callers)
        {
            Assert.Single(await caller);
        }

        Assert.Equal(1, contributor.Calls);
    }

    /// <summary>A failure is not an answer to keep. Contributors guard their own asks precisely
    /// because what escapes one costs the whole menu, so an ask that faulted anyway is the one
    /// case where remembering it would make a blip permanent.</summary>
    [Fact]
    public async Task AFaultedAskIsNotTheAnswer()
    {
        var contributor = new FailingNavContributor(Entry);
        var menu = Menu(contributor);
        var session = Session();

        await Assert.ThrowsAsync<InvalidOperationException>(() => menu.GetItems(session));

        contributor.Recover();

        Assert.Single(await menu.GetItems(session));
        Assert.Equal(2, contributor.Calls);
    }
}
