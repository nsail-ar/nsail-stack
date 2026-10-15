// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using NSail.Data;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>The install's cut across its own sessions, through the real pipeline: what it
/// refuses, what it leaves alone, and that the holder can act on the refusal. A ticket is the
/// whole subject, so none of it can be proven by a send.
/// <para>Under <c>None</c>, where the tenant wall is not installed at all: the cut is every
/// install's, and an install with one customer has sessions to void like any other.</para></summary>
public sealed class TicketEpochTests : IAsyncLifetime
{
    const int Raised = 2;

    TenancyHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await TenancyHost.Start(TenancyMode.None, epoch: Raised);
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    // The cut itself: the generation before this one is the one the flip left behind, and it
    // decrypts perfectly — the key ring is the server's and was never touched.
    [Fact]
    public async Task ATicketFromTheGenerationBeforeIsRefused()
    {
        var ticket = await _host.SignIn(tenant: null, epoch: Raised - 1);

        using var response = await Send(_host.Get("/guarded", tenant: null, ticket: ticket));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // And the other half of the same act: signing in again is what the person does about it,
    // and it works, because the mint reads the same install the gate does.
    [Fact]
    public async Task ASignInAfterTheCutIsHonoured()
    {
        var ticket = await _host.SignIn(tenant: null);

        using var response = await Send(_host.Get("/guarded", tenant: null, ticket: ticket));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // A ticket from before the cut existed carries no generation at all, and an absence is an
    // absence: deny by default, or the one holder the cut was raised for is the one it misses.
    [Fact]
    public async Task ATicketMintedBeforeTheCutExistedIsRefused()
    {
        var ticket = await _host.SignIn(tenant: null, epoch: 0);

        using var response = await Send(_host.Get("/guarded", tenant: null, ticket: ticket));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Equality and not "at least": a number lowered back after a mistake is still another
    // generation, and the sessions minted while it was wrong are exactly the ones to void.
    [Fact]
    public async Task ATicketFromAGenerationAheadIsRefusedToo()
    {
        var ticket = await _host.SignIn(tenant: null, epoch: Raised + 1);

        using var response = await Send(_host.Get("/guarded", tenant: null, ticket: ticket));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // The refusal is the pipeline's, not the endpoint's: a door that asks for nobody in
    // particular and writes a row is never reached.
    [Fact]
    public async Task TheRefusalLandsBeforeTheHandlerRuns()
    {
        var ticket = await _host.SignIn(tenant: null, epoch: 0);

        using var response = await Send(new HttpRequestMessage(
            HttpMethod.Post,
            new Uri("https://demo.nsail.ar/notes?text=voided"))
        {
            Headers = { { "Cookie", ticket } },
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await Notes());
    }

    // It has to be a refusal its holder can act on: this wall stands ahead of the sign-in like
    // it stands ahead of everything, so a ticket refused and left standing would be the whole
    // of that cookie's life spent being refused on the one act that replaces it.
    [Fact]
    public async Task TheRefusalTakesTheTicketWithIt()
    {
        var stale = await _host.SignIn(tenant: null, epoch: 0);

        using var refused = await Send(_host.Get("/sign-in?stamp=true", tenant: null, ticket: stale));

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Contains(refused.Headers.GetValues("Set-Cookie"), header => Clears(header, stale));

        using var retried = await Send(_host.Get("/guarded", tenant: null, ticket: await _host.SignIn(tenant: null)));

        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
    }

    // Nobody carries no generation: the cut answers only for a credential, so the anonymous
    // path every install opens on — the sign-in page itself — is untouched by it.
    [Fact]
    public async Task ARequestWithNoTicketIsUntouched()
    {
        using var response = await Send(_host.Get("/where", tenant: null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The deletion the cookie handler writes: the same name, emptied, dated to 1970.
    static bool Clears(string header, string ticket)
    {
        return header.StartsWith(ticket.Split('=')[0] + "=;", StringComparison.Ordinal)
            && header.Contains("expires=Thu, 01 Jan 1970", StringComparison.Ordinal);
    }

    async Task<IReadOnlyList<string>> Notes()
    {
        using var response = await Send(_host.Get("/notes", tenant: null));

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<List<string>>())!;
    }

    Task<HttpResponseMessage> Send(HttpRequestMessage request)
    {
        return _host.Client.SendAsync(request);
    }
}

/// <summary>An install that never raised the number: the cut is not a thing that happens to a
/// product because it shipped. Zero mints no claim and refuses nothing, which is what keeps the
/// deploy that carries this feature from signing out every session on every install.</summary>
public sealed class TicketEpochUnraisedTests : IAsyncLifetime
{
    TenancyHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await TenancyHost.Start(TenancyMode.None);
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task ATicketCarryingNoGenerationIsHonoured()
    {
        var ticket = await _host.SignIn(tenant: null);

        using var response = await _host.Client.SendAsync(_host.Get("/guarded", tenant: null, ticket: ticket));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

/// <summary>The unit the cut answers in: a <c>SingleDb</c> cell is one install holding many
/// tenants, and raising the number voids every session in it — which is the whole point of
/// having it at all, since the flip that makes a cell <c>SingleDb</c> is what leaves every
/// tenant's old ticket decrypting and naming rows the row filter now hides.</summary>
public sealed class TicketEpochCellTests : IAsyncLifetime
{
    TenancyHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await TenancyHost.Start(TenancyMode.SingleDb, epoch: 1);
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    [Theory]
    [InlineData("lumina")]
    [InlineData("vision")]
    public async Task EveryTenantOfTheCellIsVoidedAndEveryOneCanSignInAgain(string tenant)
    {
        var before = await _host.SignIn(tenant, epoch: 0);

        using var refused = await _host.Client.SendAsync(_host.Get("/guarded", tenant, ticket: before));

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        using var again = await _host.Client.SendAsync(
            _host.Get("/guarded", tenant, ticket: await _host.SignIn(tenant)));

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }
}
