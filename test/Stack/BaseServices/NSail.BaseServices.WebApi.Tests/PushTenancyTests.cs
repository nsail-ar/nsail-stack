// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Net;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using NSail.Data;
using NSail.Messaging.Runtime.Publishing;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>nsail#1480, the wall: a pushed event published in one tenant reaches that tenant's
/// open clients and nobody else's. The audience is the tenant the edge resolved on each end —
/// the connect request for the listener, the publishing request for the event — so this runs
/// the real pipeline under SingleDb, where the tenant column is the only wall there is.</summary>
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
    [Fact]
    public async Task APushedEventReachesItsOwnTenantAlone()
    {
        await using var lumina = await PushClient.Listen(_host.Pipeline, await _host.SignIn("lumina"), "lumina");
        await using var vision = await PushClient.Listen(_host.Pipeline, await _host.SignIn("vision"), "vision");

        await Publish("lumina", "for lumina");
        await Publish("vision", "for vision");

        Assert.Equal("for lumina", (await lumina.Next<Rang>()).Text);
        Assert.Equal("for vision", (await vision.Next<Rang>()).Text);

        Assert.Equal(["for vision"], vision.Heard.OfType<Rang>().Select(rang => rang.Text));
    }

    // A ticket minted for one tenant does not open another tenant's push: TenantClaimMiddleware
    // stands ahead of the hub as it stands ahead of every endpoint.
    [Fact]
    public async Task ATicketFromAnotherTenantCannotListen()
    {
        var ticket = await _host.SignIn("lumina");

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(PushHost.Origin, PushFeed.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => _host.Pipeline.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.Headers["Cookie"] = ticket;
                options.Headers[TenancyMiddleware.TenantHeader] = "vision";
            })
            .Build();

        await using (connection)
        {
            var refused = await Assert.ThrowsAsync<HttpRequestException>(() => connection.StartAsync());

            Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        }
    }

    async Task Publish(string tenant, string text)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"https://demo.nsail.ar/rang?text={Uri.EscapeDataString(text)}"));

        request.Headers.Add(TenancyMiddleware.TenantHeader, tenant);

        using var response = await _host.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
    }
}
