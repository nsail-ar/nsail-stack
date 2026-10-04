// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

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
