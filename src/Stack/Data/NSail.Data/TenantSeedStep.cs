// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;

namespace NSail.Data;

/// <summary>One named unit of what a tenant is born with, applied at most once per tenant ever
/// (<see cref="ITenantSeed"/>). The name is the row the history keeps
/// (<see cref="AppliedSeedStep"/>), so it is frozen the day the step ships: renaming one makes
/// every tenant that already ran it run it again, exactly as renaming a migration would.</summary>
public sealed class TenantSeedStep
{
    public TenantSeedStep(string name, Func<DbContext, Tenant, CancellationToken, Task> sow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(sow);

        Name = name;
        Sow = sow;
    }

    public string Name { get; }

    /// <summary>Plants this step's rows. The context reads and writes as the tenant, so a query
    /// here sees the tenant's rows and the install's shared ones, exactly as a handler does; ids
    /// are derived from the tenant rather than drawn, so a second process planting the same
    /// tenant writes the same rows. Whatever it writes is committed with the record that this
    /// step ran, so a step that throws is not recorded and runs again on the next touch.</summary>
    public Func<DbContext, Tenant, CancellationToken, Task> Sow { get; }
}
