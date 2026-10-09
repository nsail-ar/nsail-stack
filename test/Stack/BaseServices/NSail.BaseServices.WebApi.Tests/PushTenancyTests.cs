// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using NSail.Data;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>nsail#1480, the wall: a pushed event published in one tenant reaches that tenant's
/// open clients and nobody else's. The audience is the tenant the edge resolved on each end —
/// the connect request for the listener, the publishing request for the event — so this runs
/// the real pipeline under SingleDb, where the tenant column is the only wall there is.
///
/// <para>Over both transports (nsail#1850): the wall is the edge's, ahead of either road, and
/// a seat hears over the one its own composition picked.</para></summary>
public sealed class PushTenancyTests : IAsyncLifetime
{
    TenancyHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await TenancyHost.Start(TenancyMode.SingleDb);
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    // Lumina's publish goes out first, on purpose: each listener's line is ordered, so Vision
    // hearing its own and nothing before it is proof Lumina's never reached it.
    [Theory]
    [InlineData(Transport.Hub)]
    [InlineData(Transport.Sse)]
    public async Task APushedEventReachesItsOwnTenantAlone(Transport transport)
    {
        await using var lumina = await PushClient.Listen(_host.Pipeline, transport, await _host.SignIn("lumina"), "lumina");
        await using var vision = await PushClient.Listen(_host.Pipeline, transport, await _host.SignIn("vision"), "vision");

        await Publish("lumina", "for lumina");
        await Publish("vision", "for vision");

        Assert.Equal("for lumina", (await lumina.Next<Rang>()).Text);
        Assert.Equal("for vision", (await vision.Next<Rang>()).Text);

        Assert.Equal(["for vision"], vision.Heard.OfType<Rang>().Select(rang => rang.Text));
    }

    // A ticket minted for one tenant does not open another tenant's push: TenantClaimMiddleware
    // stands ahead of the hub as it stands ahead of every endpoint.
    [Theory]
    [InlineData(Transport.Hub)]
    [InlineData(Transport.Sse)]
    public async Task ATicketFromAnotherTenantCannotListen(Transport transport)
    {
        var ticket = await _host.SignIn("lumina");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            await PushRefusal.Of(_host.Pipeline, transport, ticket, tenant: "vision"));
    }

    async Task Publish(string tenant, string text)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"https://demo.nsail.ar/rang?text={Uri.EscapeDataString(text)}"));

        request.Headers.Add(TenancyMiddleware.TenantHeader, tenant);

        using var response = await _host.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
    }
}
