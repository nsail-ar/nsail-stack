// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using NSail.Data;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>A ticket carries the memberships and roles of sign-in; a party whose set changed
/// since has it refused as stale, within the state window and at one read per party per window.</summary>
public sealed class MembershipStampTests : IAsyncLifetime
{
    static readonly Guid Party = Guid.Parse("11111111-1111-1111-1111-111111111111");

    readonly TestClock _clock = new();
    TenancyHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await TenancyHost.Start(TenancyMode.None, clock: _clock);
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task ARoleChangedBetweenTwoRequestsRefusesTheTicketOncePastTheWindow()
    {
        _host.Memberships.Set(Party, "admin");

        var ticket = await _host.SignIn(tenant: null, party: Party, memberships: "admin");

        Assert.Equal(HttpStatusCode.OK, await Status(ticket));

        _host.Memberships.Set(Party, "viewer");
        _clock.Advance(TimeSpan.FromSeconds(new TenancyOptions().StateCacheSeconds + 1));

        Assert.Equal(HttpStatusCode.Unauthorized, await Status(ticket));
    }

    [Fact]
    public async Task ASignInAfterTheChangeIsHonoured()
    {
        _host.Memberships.Set(Party, "viewer");

        var ticket = await _host.SignIn(tenant: null, party: Party, memberships: "viewer");

        Assert.Equal(HttpStatusCode.OK, await Status(ticket));
    }

    [Fact]
    public async Task ASignInAfterTheChangeIsHonouredEvenWhenTheMemoHoldsTheOldSet()
    {
        _host.Memberships.Set(Party, "admin");

        var before = await _host.SignIn(tenant: null, party: Party, memberships: "admin");

        Assert.Equal(HttpStatusCode.OK, await Status(before));

        _host.Memberships.Set(Party, "viewer");

        var after = await _host.SignIn(tenant: null, party: Party, memberships: "viewer");

        Assert.Equal(HttpStatusCode.OK, await Status(after));
        Assert.Equal(HttpStatusCode.Unauthorized, await Status(before));
    }

    [Fact]
    public async Task AnUnchangedPartyIsReadOncePerWindowNotOncePerRequest()
    {
        _host.Memberships.Set(Party, "admin");

        var ticket = await _host.SignIn(tenant: null, party: Party, memberships: "admin");

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.OK, await Status(ticket));
        }

        Assert.Equal(1, _host.Memberships.Reads);

        _clock.Advance(TimeSpan.FromSeconds(new TenancyOptions().StateCacheSeconds + 1));

        Assert.Equal(HttpStatusCode.OK, await Status(ticket));
        Assert.Equal(2, _host.Memberships.Reads);
    }

    [Fact]
    public async Task AChangeInsideTheWindowIsNotYetSeen()
    {
        _host.Memberships.Set(Party, "admin");

        var ticket = await _host.SignIn(tenant: null, party: Party, memberships: "admin");

        Assert.Equal(HttpStatusCode.OK, await Status(ticket));

        _host.Memberships.Set(Party, "viewer");

        Assert.Equal(HttpStatusCode.OK, await Status(ticket));
    }

    [Fact]
    public async Task AMismatchWithinTheWindowCostsOneConfirmingRead()
    {
        _host.Memberships.Set(Party, "admin");

        var ticket = await _host.SignIn(tenant: null, party: Party, memberships: "admin");

        Assert.Equal(HttpStatusCode.OK, await Status(ticket));

        _host.Memberships.Set(Party, "viewer");
        _clock.Advance(TimeSpan.FromSeconds(new TenancyOptions().StateCacheSeconds + 1));

        var before = _host.Memberships.Reads;

        Assert.Equal(HttpStatusCode.Unauthorized, await Status(ticket));
        Assert.Equal(before + 1, _host.Memberships.Reads);
    }

    async Task<HttpStatusCode> Status(string ticket)
    {
        using var response = await _host.Client.SendAsync(_host.Get("/guarded", tenant: null, ticket: ticket));

        return response.StatusCode;
    }
}
