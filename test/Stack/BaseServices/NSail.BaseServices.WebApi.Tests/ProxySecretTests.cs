// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using NSail.Data;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>The tenant header is believed only from a caller that proves it is the proxy.</summary>
public sealed class ProxySecretTests : IAsyncLifetime
{
    const string Secret = "proxy-secret-for-tests";

    TenancyHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await TenancyHost.Start(TenancyMode.SingleDb, proxySecret: Secret);
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong")]
    public async Task TheHeaderWithoutTheSecretIs403(string? presented)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://demo.nsail.ar/where"));

        request.Headers.Add(TenancyMiddleware.TenantHeader, "lumina");

        if (presented is not null)
        {
            request.Headers.Add(TenancyMiddleware.ProxySecretHeader, presented);
        }

        using var response = await _host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TheHeaderWithTheSecretResolvesAsBefore()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://demo.nsail.ar/where"));

        request.Headers.Add(TenancyMiddleware.TenantHeader, "lumina");
        request.Headers.Add(TenancyMiddleware.ProxySecretHeader, Secret);

        using var response = await _host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
