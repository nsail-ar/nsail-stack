// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using NSail.Data;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>The proof of nsail#358: under <c>MultiDb</c> a ticket is honoured on the tenant it
/// was minted for and nowhere else. The connection wall is already standing here — what these
/// ask is whether the identity crosses it.</summary>
public sealed class TenantClaimTests : IAsyncLifetime
{
    TenancyHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await TenancyHost.Start(TenancyMode.MultiDb);

        await _host.Provision("lumina");
        await _host.Provision("vision");
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task ATicketMintedOnOneTenantIsRefusedOnAnother()
    {
        var ticket = await _host.SignIn("lumina");

        using var own = await Send(_host.Get("/guarded", "lumina", ticket: ticket));
        using var crossed = await Send(_host.Get("/guarded", "vision", ticket: ticket));

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, crossed.StatusCode);
    }

    // The refusal is the pipeline's, not the endpoint's: a door that asks for nobody in
    // particular and writes to the tenant's database is never reached at all.
    [Fact]
    public async Task TheRefusalLandsBeforeTheHandlerRuns()
    {
        var ticket = await _host.SignIn("lumina");

        using var response = await Send(new HttpRequestMessage(
            HttpMethod.Post,
            new Uri("https://demo.nsail.ar/notes?text=crossed"))
        {
            Headers =
            {
                { TenancyMiddleware.TenantHeader, "vision" },
                { "Cookie", ticket },
            },
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await Notes("vision"));
    }

    // The claim is checked against the header's resolution, never against a re-read of the Host:
    // one tenant reaches its own install through a wildcard demo label and through its own
    // domain, and a wall that looked at where the request landed would refuse one of them.
    [Fact]
    public async Task ItsOwnTenantIsHonouredOnEveryHostThatReachesIt()
    {
        var ticket = await _host.SignIn("lumina", stamp: true);

        using var wildcard = await Send(_host.Get("/guarded", "lumina", "lumina.demo.nsail.ar", ticket));
        using var custom = await Send(_host.Get("/guarded", "lumina", "www.opticalumina.com.ar", ticket));

        Assert.Equal(HttpStatusCode.OK, wildcard.StatusCode);
        Assert.Equal(HttpStatusCode.OK, custom.StatusCode);
    }

    // A ticket that names no tenant is one this install cannot place — minted before the wall
    // existed, or somewhere that has none. Deny by default: an absence is not consent.
    [Fact]
    public async Task ATicketThatNamesNoTenantIsRefusedOnItsOwnTenant()
    {
        var ticket = await _host.SignIn("lumina", stamp: false);

        using var response = await Send(_host.Get("/guarded", "lumina", ticket: ticket));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // The refusal has to be one its holder can act on: this wall stands ahead of the sign-in
    // like it stands ahead of everything, so a ticket refused and left standing is days of 401
    // on the one act that would replace it. Signed out with the refusal, what the holder
    // retries with is nothing — and nothing is the anonymous path the sign-in lives on.
    [Fact]
    public async Task TheRefusalTakesTheTicketWithIt()
    {
        var stale = await _host.SignIn("lumina", stamp: false);

        using var refused = await Send(_host.Get("/sign-in?stamp=true", "lumina", ticket: stale));

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Contains(refused.Headers.GetValues("Set-Cookie"), header => Clears(header, stale));

        using var retried = await Send(_host.Get("/guarded", "lumina", ticket: await _host.SignIn("lumina")));

        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
    }

    // Nobody carries nothing across: the wall answers only for a credential, so the anonymous
    // path every install opens on — the sign-in page itself — is untouched by it.
    [Fact]
    public async Task ARequestWithNoTicketIsUntouched()
    {
        using var response = await Send(_host.Get("/where", "vision"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The deletion the cookie handler writes: the same name, emptied, dated to the epoch.
    static bool Clears(string header, string ticket)
    {
        return header.StartsWith(ticket.Split('=')[0] + "=;", StringComparison.Ordinal)
            && header.Contains("expires=Thu, 01 Jan 1970", StringComparison.Ordinal);
    }

    async Task<IReadOnlyList<string>> Notes(string tenant)
    {
        using var response = await Send(_host.Get("/notes", tenant));

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<List<string>>())!;
    }

    Task<HttpResponseMessage> Send(HttpRequestMessage request)
    {
        return _host.Client.SendAsync(request);
    }
}
