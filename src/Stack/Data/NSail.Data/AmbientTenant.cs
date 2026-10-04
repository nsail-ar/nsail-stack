// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>Which tenant the current flow of work belongs to, carried beside the call stack
/// rather than inside a DI scope. A scope is the wrong holder on its own: a request's work
/// routinely opens scopes of its own — a prerendered component resolving a brand, a provider
/// taking a fresh scope so two threads do not share one <c>DbContext</c> — and a child scope
/// that could not see the tenant would either refuse or, worse, connect somewhere else. Set
/// once at the edge, it reaches every scope the request goes on to open, and it cannot reach
/// a request beside it.</summary>
public static class AmbientTenant
{
    static readonly AsyncLocal<Tenant?> Ambient = new();

    public static Tenant Current
    {
        get { return Ambient.Value ?? Tenant.None; }
    }

    public static void Enter(Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        Ambient.Value = tenant;
    }
}
