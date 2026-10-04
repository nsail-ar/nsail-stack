// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSail.Data;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>The seed as a chain, which is what makes a tenant reachable after it exists. The
/// first touch is the twin of the first migration, and every touch after it is the twin of every
/// migration after that: a step this tenant has not run, run now, recorded with what it wrote.
/// Both halves are proved against a real database through the real pipeline, because both are
/// about a transaction and a row.</summary>
public sealed class TenantSeedChainTests : IAsyncLifetime
{
    TenancyHost _host = null!;
    TenancyHost? _redeployed;

    public async Task InitializeAsync()
    {
        FlakySeed.Attempts = 0;

        _host = await TenancyHost.Start(TenancyMode.SingleDb, bootstraps: true);
    }

    public async Task DisposeAsync()
    {
        if (_redeployed is not null)
        {
            await _redeployed.DisposeAsync();
        }

        await _host.DisposeAsync();
    }

    // The story this whole mechanism exists for: the shop was planted last week, the step
    // shipped today, and nobody goes near the database. The deploy is a second process over the
    // same rows — its memo is empty, so the touch after it runs what the tenant is missing.
    [Fact]
    public async Task AStepAppendedAfterATenantExistsReachesIt()
    {
        Assert.Equal(["welcome lumina"], await Read(_host, "lumina"));

        _redeployed = await TenancyHost.Redeploy(_host, typeof(ReminderSeed));

        Assert.Equal(["reminder lumina", "welcome lumina"], await Read(_redeployed, "lumina"));
    }

    // And the step already run is not run again by the build that appended the next one: what
    // decides is the tenant's history, never the seed's own guard, which a step that plants
    // unconditionally does not have.
    [Fact]
    public async Task TheStepsAlreadyRunAreNotRunAgainByTheDeployThatAppends()
    {
        await Read(_host, "lumina");

        _redeployed = await TenancyHost.Redeploy(_host, typeof(ReminderSeed));

        await Read(_redeployed, "lumina");
        await Read(_redeployed, "lumina");

        Assert.Equal(["reminder lumina", "welcome lumina"], await Read(_redeployed, "lumina"));
        Assert.Equal(["Reminder", "Welcome"], await Applied(_redeployed, "lumina"));
    }

    // A tenant that never existed gets the whole chain on its first touch, in order — the same
    // steps, reached from the other end.
    [Fact]
    public async Task ATenantBornUnderTheLongerChainRunsAllOfIt()
    {
        _redeployed = await TenancyHost.Redeploy(_host, typeof(ReminderSeed));

        Assert.Equal(["reminder vision", "welcome vision"], await Read(_redeployed, "vision"));
    }

    // The record and the rows are one transaction: a step that threw wrote nothing durable and
    // is not recorded, so the next touch tries it again — and the step BEFORE it, which
    // committed, is not tried a second time. One read says all three.
    [Fact]
    public async Task AStepThatThrowsIsNotRecordedAndRunsAgainOnTheNextTouch()
    {
        _redeployed = await TenancyHost.Redeploy(_host, typeof(FlakySeed));

        using var refused = await _redeployed.Client.SendAsync(_redeployed.Get("/notes", "lumina"));

        Assert.False(refused.IsSuccessStatusCode);
        Assert.Equal(["Welcome"], await Applied(_redeployed, "lumina"));

        Assert.Equal(["flaky lumina", "welcome lumina"], await Read(_redeployed, "lumina"));
        Assert.Equal(["Flaky", "Welcome"], await Applied(_redeployed, "lumina"));
    }

    static async Task<IReadOnlyList<string>> Read(TenancyHost host, string tenant)
    {
        using var response = await host.Client.SendAsync(host.Get("/notes", tenant));

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<List<string>>())!;
    }

    static async Task<IReadOnlyList<string>> Applied(TenancyHost host, string tenant)
    {
        await using var scope = host.Services.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<ResolvedTenancyProvider>().Enter(Tenant.For(tenant));

        return await scope.ServiceProvider.GetRequiredService<DbContext>()
            .Set<AppliedSeedStep>()
            .Select(step => step.Step)
            .OrderBy(step => step)
            .ToListAsync();
    }
}
