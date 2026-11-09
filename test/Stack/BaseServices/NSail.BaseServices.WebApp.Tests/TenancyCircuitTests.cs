// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Data;

namespace NSail.BaseServices.WebApp.Tests;

/// <summary>A circuit's event runs in a flow of its own, and its outermost send in a scope of its
/// own: the tenant reaches that send only if the activity carries it on the flow.</summary>
public sealed class TenancyCircuitTests
{
    [Fact]
    public async Task Every_inbound_activity_runs_in_the_tenant_the_circuit_was_opened_for()
    {
        await using var services = Compose();
        await using var scope = services.CreateAsyncScope();

        var circuit = new TenancyCircuit(scope.ServiceProvider);

        await Open(circuit, Tenant.For("lumina"));

        Tenant? seen = null;

        var activity = circuit.CreateInboundActivityHandler(_ =>
        {
            seen = AmbientTenant.Current;

            return Task.CompletedTask;
        });

        // The event arrives in a flow the connect request never reached.
        Assert.False(AmbientTenant.Current.IsResolved);

        await activity(null!);

        Assert.Equal("lumina", seen?.Slug);
        Assert.False(AmbientTenant.Current.IsResolved);
    }

    [Fact]
    public async Task A_circuit_opened_in_no_tenant_enters_none()
    {
        await using var services = Compose();
        await using var scope = services.CreateAsyncScope();

        var circuit = new TenancyCircuit(scope.ServiceProvider);

        await circuit.OnCircuitOpenedAsync(null!, CancellationToken.None);

        Tenant? seen = null;

        var activity = circuit.CreateInboundActivityHandler(_ =>
        {
            seen = AmbientTenant.Current;

            return Task.CompletedTask;
        });

        await activity(null!);

        Assert.False(seen?.IsResolved);
    }

    static ServiceProvider Compose()
    {
        return new ServiceCollection()
            .AddSingleton(new TenancyOptions { Mode = TenancyMode.SingleDb })
            .AddScoped<ResolvedTenancyProvider>()
            .BuildServiceProvider();
    }

    // The connect request's flow: the edge entered the tenant, and the write ends with it.
    static async Task Open(TenancyCircuit circuit, Tenant tenant)
    {
        AmbientTenant.Enter(tenant);

        await circuit.OnCircuitOpenedAsync(null!, CancellationToken.None);
    }
}
