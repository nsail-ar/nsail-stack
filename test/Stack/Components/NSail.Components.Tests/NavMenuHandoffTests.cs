// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using NSail.Components.Tests.Fixtures;
using NSail.Settings;

namespace NSail.Components.Tests;

/// <summary>What the prerender spent on the drawer's wire — the install's arrangement and the
/// entries' counts — crosses to the client as persisted state. Not an optimization with a
/// margin: until it crossed, a full load drew the drawer empty, asked the two of them, and the
/// splash came off over the gap.</summary>
public sealed class NavMenuHandoffTests : BunitContext
{
    const string HandoffKey = "NSail.NavMenu";

    readonly CountingNavContributor _contributor = new(
        new NavMenuItem { Name = "Parties", Weight = 10 },
        new NavMenuItem { Name = "Tickets", Weight = 20 });

    [Fact]
    public async Task TheClientSpendsNoAskOnTheArrangementOrTheCounts()
    {
        var handoff = await Handoff.Carrying(HandoffKey, new NavMenuHandoff
        {
            Arrangements = [Placed("Parties", 90)],
            Counts = new Dictionary<string, int> { ["Tickets"] = 4 }
        });

        var arrangements = new CountingArrangement(Placed("Parties", 5));
        var counts = new CountingCount("Tickets", 9);

        var menu = new NavMenu([_contributor], [counts], [arrangements], handoff: handoff.State);

        var items = menu.GetItems(null);

        // Completed before anything awaited it, which is the whole point: an adoption with a
        // round trip behind it costs exactly the render the handoff exists to save.
        Assert.True(items.IsCompletedSuccessfully);

        var counted = menu.GetCounts();

        Assert.True(counted.IsCompletedSuccessfully);

        Assert.Equal(0, arrangements.Calls);
        Assert.Equal(0, counts.Calls);
        Assert.Equal(90, (await items).Single(item => item.Name == "Parties").Weight);
        Assert.Equal(4, (await counted)["Tickets"]);
    }

    // The number is the point of being a number: the handoff answers the ARRIVAL and nothing
    // after it, so the drawer's next recount — a navigation, a NavMenuCountsChanged — is a real
    // ask again.
    [Fact]
    public async Task TheHandedCountIsTakenOnceAndTheNextRecountAsks()
    {
        var handoff = await Handoff.Carrying(HandoffKey, new NavMenuHandoff
        {
            Counts = new Dictionary<string, int> { ["Tickets"] = 4 }
        });

        var counts = new CountingCount("Tickets", 9);

        var menu = new NavMenu([_contributor], [counts], [], handoff: handoff.State);

        _ = menu.GetItems(null);

        Assert.Equal(4, (await menu.GetCounts())["Tickets"]);
        Assert.Equal(9, (await menu.GetCounts())["Tickets"]);
        Assert.Equal(1, counts.Calls);
    }

    // Nothing handed over: this side is the one that asks, and therefore the one with an answer
    // worth persisting. What it writes has to be what the client above restores, or the two ends
    // drift apart silently and the empty drawer comes back.
    [Fact]
    public async Task ThePrerenderPersistsTheArrangementItBuiltFromAndTheCountsAtPersistTime()
    {
        var handoff = await Handoff.Empty();

        var arrangements = new CountingArrangement(Placed("Parties", 90));
        var counts = new CountingCount("Tickets", 4);

        var menu = new NavMenu([_contributor], [counts], [arrangements], handoff: handoff.State);

        await menu.GetItems(null);
        await handoff.Manager.PersistStateAsync(handoff, Renderer);

        var persisted = handoff.Read<NavMenuHandoff>(HandoffKey);

        Assert.Equal(90, persisted?.Arrangements.Single().Entries.Single().Order);
        Assert.Equal(4, persisted?.Counts["Tickets"]);
    }

    // An install's rows are an authorized read, so the session that was denied them and the
    // session that was granted them do not get one answer between them.
    [Fact]
    public async Task ARebuiltTreeReadsTheArrangementAgain()
    {
        var handoff = await Handoff.Empty();

        var arrangements = new CountingArrangement(Placed("Parties", 90));

        var menu = new NavMenu([_contributor], [], [arrangements], handoff: handoff.State);

        await menu.GetItems(null);
        await menu.GetItems(Task.FromResult(new Microsoft.AspNetCore.Components.Authorization.AuthenticationState(new System.Security.Claims.ClaimsPrincipal())));

        Assert.Equal(2, arrangements.Calls);
    }

    static NavMenuArrangement Placed(string name, int order)
    {
        return new NavMenuArrangement
        {
            Entries = [new NavMenuArrangementEntry { Name = name, Order = order }]
        };
    }

    sealed class CountingArrangement(NavMenuArrangement arrangement) : INavMenuArrangement
    {
        public int Calls { get; private set; }

        public Task<NavMenuArrangement> GetArrangement(CancellationToken cancellationToken = default)
        {
            Calls++;

            return Task.FromResult(arrangement);
        }
    }

    sealed class CountingCount(string name, int count) : INavMenuCount
    {
        public int Calls { get; private set; }

        public string Name
        {
            get { return name; }
        }

        public Task<int> GetCount()
        {
            Calls++;

            return Task.FromResult(count);
        }
    }
}
