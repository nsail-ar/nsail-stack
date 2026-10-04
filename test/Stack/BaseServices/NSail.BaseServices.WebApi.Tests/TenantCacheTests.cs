// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using NSail.Data;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>A first touch writes a tenant's rows under whatever a process already holds of them
/// — a policy set a background job loaded before the deploy's first request — so it tells every
/// registered cache to forget that tenant. Proved on both walls, through the real pipeline.</summary>
public sealed class TenantCacheTests : IAsyncLifetime
{
    TenancyHost _host = null!;

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task ASeedStepsFirstTouchForgetsTheTenantItPlanted()
    {
        _host = await TenancyHost.Start(TenancyMode.SingleDb, bootstraps: true);

        await Touch("lumina");

        Assert.Equal(["lumina"], Forgotten());
    }

    // The memo answers every later request before any step is asked for, so nothing was written
    // and nothing is forgotten: a cache that loaded after the first touch stays loaded.
    [Fact]
    public async Task ALaterRequestForgetsNothing()
    {
        _host = await TenancyHost.Start(TenancyMode.SingleDb, bootstraps: true);

        await Touch("lumina");
        await Touch("vision");
        await Touch("lumina");

        Assert.Equal(["lumina", "vision"], Forgotten());
    }

    [Fact]
    public async Task AMigratingFirstTouchForgetsTheTenantItMigrated()
    {
        _host = await TenancyHost.Start(TenancyMode.MultiDb);

        await _host.Provision("lumina");

        await Touch("lumina");

        Assert.Equal(["lumina"], Forgotten());
    }

    async Task Touch(string tenant)
    {
        using var response = await _host.Client.SendAsync(_host.Get("/notes", tenant));

        response.EnsureSuccessStatusCode();
    }

    IReadOnlyList<string?> Forgotten()
    {
        return _host.Services.GetRequiredService<ForgottenTenants>().Slugs;
    }
}

public sealed class ForgottenTenants : ITenantCache
{
    readonly ConcurrentQueue<string?> _slugs = new();

    public IReadOnlyList<string?> Slugs
    {
        get { return [.. _slugs]; }
    }

    public void Forget(Tenant tenant)
    {
        _slugs.Enqueue(tenant.Slug);
    }
}
