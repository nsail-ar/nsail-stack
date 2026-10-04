// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSail.Data;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>The first touch, where the wall is the column: a tenant here is a slug the proxy let
/// through, with no database to create and no chain to apply — so what makes it usable is the
/// rows its own first request plants. This is the twin of <c>MultiDbTenancyTests</c>' first-touch
/// migration, over the same lock and the same memo, and it runs through the real pipeline against
/// real Postgres.</summary>
public sealed class SingleDbBootstrapTests : IAsyncLifetime
{
    TenancyHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await TenancyHost.Start(TenancyMode.SingleDb, bootstraps: true);
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    [Theory]
    [InlineData("lumina")]
    [InlineData("vision")]
    public async Task AFirstTouchPlantsTheTenantsOwnRows(string tenant)
    {
        Assert.Equal([$"welcome {tenant}"], await Read(tenant));
    }

    // Onboarding is the proxy's block and nothing else: nobody provisioned these two, and the
    // second one is as planted as the first.
    [Fact]
    public async Task EachTenantIsPlantedWithItsOwnAndSeesNobodyElses()
    {
        Assert.Equal(["welcome lumina"], await Read("lumina"));
        Assert.Equal(["welcome vision"], await Read("vision"));
    }

    // Every request after the first pays nothing and plants nothing — the memo answers before
    // the lock is even taken.
    [Fact]
    public async Task ALaterRequestPlantsNothingMore()
    {
        await Read("lumina");
        await Read("lumina");

        Assert.Single(await Read("lumina"));
    }

    // The tenant's own chain, written down: a step that ran left a row saying so, which is what
    // every later touch reads instead of guessing.
    [Fact]
    public async Task AStepThatRanIsRecorded()
    {
        await Read("lumina");

        Assert.Equal(["Welcome"], await Applied("lumina"));
    }

    // And the step's own guard is what holds when the history cannot: a step is idempotent in
    // its own right, so a second process — or a hand run — cannot double what it planted.
    // Called directly because that is the only way past the history.
    [Fact]
    public async Task AStepAskedTwiceInOneScopePlantsOnce()
    {
        await Read("lumina");

        await using var scope = _host.Services.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<ResolvedTenancyProvider>().Enter(Tenant.For("lumina"));

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        foreach (var step in scope.ServiceProvider.GetRequiredService<ITenantSeed>().Steps)
        {
            await step.Sow(db, Tenant.For("lumina"), CancellationToken.None);
        }

        Assert.Single(await Read("lumina"));
    }

    async Task<IReadOnlyList<string>> Applied(string tenant)
    {
        await using var scope = _host.Services.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<ResolvedTenancyProvider>().Enter(Tenant.For(tenant));

        return await scope.ServiceProvider.GetRequiredService<DbContext>()
            .Set<AppliedSeedStep>()
            .Select(step => step.Step)
            .OrderBy(step => step)
            .ToListAsync();
    }

    async Task<IReadOnlyList<string>> Read(string tenant)
    {
        using var response = await _host.Client.SendAsync(_host.Get("/notes", tenant));

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<List<string>>())!;
    }
}
