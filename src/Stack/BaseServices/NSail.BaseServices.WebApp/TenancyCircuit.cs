// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection;
using NSail.Data;

namespace NSail.BaseServices.WebApp;

// A circuit outlives the request that opened it, so the tenant that request entered stops
// travelling the moment the connection is established and every later render happens in a
// flow of its own. Its scope is built while that connect request is still on the stack, which
// is the one moment the ambient tenant is readable from here — so it is pinned onto the
// circuit's own provider then, and every render the circuit goes on to do resolves the tenant
// it was opened for.
//
// The pin reaches the circuit's scope and nothing beside it, and the outermost send of an event
// runs in a scope of its own (the ambient unit of work): its DbContext and the tenant-state wall
// on the send read the tenant from the flow. So every inbound activity — an event, a JS call, a
// navigation — enters the circuit's tenant on its own flow before it runs.
sealed class TenancyCircuit : CircuitHandler
{
    readonly IServiceProvider _services;

    Tenant? _tenant;

    public TenancyCircuit(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _services = services;
    }

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        // Resolved rather than injected: a host under None registers no tenancy at all, and a
        // constructor dependency on what a mode does not register fails the container's own
        // startup validation for every host that never resolves a tenant.
        if (AmbientTenant.Current is { IsResolved: true } tenant
            && _services.GetService<ResolvedTenancyProvider>() is { } tenancy)
        {
            tenancy.Enter(tenant);

            _tenant = tenant;
        }

        return Task.CompletedTask;
    }

    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next)
    {
        // Async so the write stays on the activity's own flow and never leaks to the caller's.
        return async context =>
        {
            if (_tenant is { } tenant)
            {
                AmbientTenant.Enter(tenant);
            }

            await next(context);
        };
    }
}
