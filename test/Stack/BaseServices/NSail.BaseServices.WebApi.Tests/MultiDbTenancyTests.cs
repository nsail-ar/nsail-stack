// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using NSail.Data;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>The story-sized proof of nsail#357: one host under <c>MultiDb</c>, two tenants,
/// two databases, and every refusal the request path owes.</summary>
public sealed class MultiDbTenancyTests : IAsyncLifetime
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
    public async Task TwoTenantsAreServedFromTwoDatabases()
    {
        var lumina = await Where("lumina");
        var vision = await Where("vision");

        Assert.Equal("lumina", lumina.Slug);
        Assert.Equal(_host.DatabaseOf("lumina"), lumina.Database);
        Assert.Equal(_host.DatabaseOf("lumina"), lumina.Connection);

        Assert.Equal("vision", vision.Slug);
        Assert.Equal(_host.DatabaseOf("vision"), vision.Connection);
    }

    // A request's own work opens scopes of its own, and every one of them is the same tenant's.
    [Fact]
    public async Task AScopeTheRequestOpensForItselfIsTheSameTenants()
    {
        using var response = await Send(_host.Get("/where-under-a-scope-of-its-own", "lumina"));

        response.EnsureSuccessStatusCode();

        var where = (await response.Content.ReadFromJsonAsync<Where>())!;

        Assert.Equal("lumina", where.Slug);
        Assert.Equal(_host.DatabaseOf("lumina"), where.Connection);
    }

    // The wildcard demo label and a custom domain are two Caddy site blocks writing one header,
    // and the app cannot tell them apart because it never looks at where the request landed.
    [Fact]
    public async Task TheHostTheRequestArrivedOnChangesNothing()
    {
        var wildcard = await Where("lumina", host: "lumina.demo.nsail.ar");
        var custom = await Where("lumina", host: "www.opticalumina.com.ar");

        Assert.Equal(wildcard.Connection, custom.Connection);
        Assert.Equal(_host.DatabaseOf("lumina"), custom.Connection);
    }

    [Fact]
    public async Task OneHostAndTwoHeadersAreTwoTenants()
    {
        var lumina = await Where("lumina", host: "nsail.ar");
        var vision = await Where("vision", host: "nsail.ar");

        Assert.NotEqual(lumina.Connection, vision.Connection);
    }

    [Fact]
    public async Task WhatOneTenantWritesTheOtherNeverReads()
    {
        await Send(new HttpRequestMessage(HttpMethod.Post, new Uri("https://demo.nsail.ar/notes?text=lumina-only"))
        {
            Headers = { { TenancyMiddleware.TenantHeader, "lumina" } },
        });

        Assert.Equal(["lumina-only"], await Notes("lumina"));
        Assert.Empty(await Notes("vision"));
    }

    [Theory]
    // Provisioned for no one: Caddy's wildcard demo block hands the app any label a browser types.
    [InlineData("ghost")]
    [InlineData("")]
    [InlineData("lumina_vision")]
    [InlineData("lumina vision")]
    [InlineData("postgres")]
    [InlineData("lumina;DROP DATABASE postgres")]
    public async Task ASlugWithNoDatabaseIs404(string tenant)
    {
        using var response = await Send(_host.Get("/where", tenant));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NoHeaderAtAllIs404()
    {
        using var response = await Send(_host.Get("/where", tenant: null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Caddy strips an incoming copy before it writes its own, so the app never sees two — and
    // if it ever did, the pair is not a slug and neither copy gets to win.
    [Fact]
    public async Task ASecondCopyOfTheHeaderIs404()
    {
        var request = _host.Get("/where", "lumina");

        request.Headers.Add(TenancyMiddleware.TenantHeader, "vision");

        using var response = await Send(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NothingIsCreatedOnTheRequestPath()
    {
        using var response = await Send(_host.Get("/where", "ghost"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(await _host.Exists("ghost"));
    }

    // The refusal lands ahead of authentication: an unknown install answers 404 on the very
    // endpoint a known one answers 401 on, so nothing about it is reachable by signing in.
    [Fact]
    public async Task TheAnswerComesBeforeAuthentication()
    {
        using var unknown = await Send(_host.Get("/guarded", "ghost"));
        using var known = await Send(_host.Get("/guarded", "lumina"));

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, known.StatusCode);
    }

    [Fact]
    public async Task FirstTouchMigrates()
    {
        await _host.Provision("aurora");

        Assert.Empty(await _host.AppliedMigrations("aurora"));

        Assert.Equal(_host.DatabaseOf("aurora"), (await Where("aurora")).Connection);
        Assert.Equal(["20260824000000_Notes"], await _host.AppliedMigrations("aurora"));
    }

    // Two first requests at once: one migrates, the other waits on the advisory lock and finds
    // the work done. Both are answered, and the chain is applied exactly once.
    [Fact]
    public async Task TwoConcurrentFirstTouchesProduceOneMigration()
    {
        await _host.Provision("boreal");

        var touches = await Task.WhenAll(
            Send(_host.Get("/where", "boreal")),
            Send(_host.Get("/where", "boreal")));

        Assert.All(touches, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(["20260824000000_Notes"], await _host.AppliedMigrations("boreal"));

        foreach (var touch in touches)
        {
            touch.Dispose();
        }
    }

    // The lock is Postgres's own, taken on the tenant's database, so it holds between processes
    // that share no memory — which is the case a cell behind more than one replica actually has.
    // Two registries stand in for the two processes: neither has seen this database before.
    [Fact]
    public async Task TheFirstTouchLockHoldsBetweenProcesses()
    {
        await _host.Provision("austral");

        var monitor = new object();
        var inside = 0;
        var peak = 0;
        var ran = 0;

        async Task Touch(TenantDatabases databases)
        {
            await databases.FirstTouch(_host.DatabaseOf("austral"), async _ =>
            {
                lock (monitor)
                {
                    ran++;
                    inside++;
                    peak = Math.Max(peak, inside);
                }

                await Task.Delay(300);

                lock (monitor)
                {
                    inside--;
                }
            });
        }

        await Task.WhenAll(Touch(Registry()), Touch(Registry()));

        Assert.Equal(2, ran);
        Assert.Equal(1, peak);
    }

    TenantDatabases Registry()
    {
        return new TenantDatabases(
            new TenancyOptions { Mode = TenancyMode.MultiDb, Product = TenancyHost.Product, Cell = _host.Cell },
            "Host=localhost;Username=postgres;Database=postgres");
    }

    async Task<Where> Where(string tenant, string host = "demo.nsail.ar")
    {
        using var response = await Send(_host.Get("/where", tenant, host));

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<Where>())!;
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
