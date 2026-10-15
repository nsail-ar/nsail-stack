// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;

namespace NSail.Data;

/// <summary>The <c>Tenancy:</c> section. <see cref="Mode"/> is what an install writes;
/// <see cref="Connection"/> and <see cref="Rows"/> are what the seam reads — two switches
/// derived independently, so nothing downstream ever branches on the mode itself.
/// <see cref="Product"/> and <see cref="Cell"/> are the cell's own half of a tenant database
/// name; a mode that connects per tenant is what makes them required.</summary>
public class TenancyOptions
{
    public TenancyMode Mode { get; set; }

    public string? Product { get; set; }

    public string? Cell { get; set; }

    /// <summary>The slugs this install holds, comma-separated — the roster
    /// (<see cref="TenantRoster"/>) that work outside a request sweeps. Stated only where the
    /// wall is the tenant column: there, nothing in the install knows who exists, because the
    /// proxy's whitelist is the roster and the rows cannot be asked. A mode that connects per
    /// tenant reads its databases instead and ignores this.</summary>
    public string? Tenants { get; set; }

    /// <summary>How long this process holds what it read about a tenant
    /// (<see cref="TenantStateMemo"/>). It is the one knob over the gate's cost, and the one
    /// over how late a pause written from outside arrives: an install that suspends for abuse
    /// shortens it, one that only schedules maintenance can leave it alone. Refused at zero
    /// rather than read as "no memo" — a section with a stray key must not quietly put a
    /// primary-key lookup back on every request.</summary>
    [Range(1, 3600)]
    public int StateCacheSeconds { get; set; } = 120;

    /// <summary>Who a paused tenant is told to reach — the address or number whoever operates
    /// this install answers on, because it is the operator and not the tenant that lifts a
    /// pause. Shown as a line on the suspended page; empty means no line, which is the honest
    /// answer for an install that has nobody to name. A tenant that wants its own words puts
    /// them in <see cref="TenantState.Message"/>.</summary>
    public string? SupportContact { get; set; }

    public TenancyScope Connection
    {
        get
        {
            return Mode switch
            {
                TenancyMode.MultiDb => TenancyScope.Tenant,
                _ => TenancyScope.Install,
            };
        }
    }

    public TenancyScope Rows
    {
        get
        {
            return Mode switch
            {
                TenancyMode.SingleDb => TenancyScope.Tenant,
                _ => TenancyScope.Install,
            };
        }
    }
}
