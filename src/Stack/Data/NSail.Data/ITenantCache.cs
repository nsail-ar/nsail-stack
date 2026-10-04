// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>Something that holds a tenant's rows in memory beyond the request that read them.
/// A first touch writes under whatever it holds without passing through its own writers — a
/// migration applied to the tenant's database, a seed step planted in the shared one — so the
/// touch tells it to forget that tenant, and the next read is the rows as they now stand.
/// Registered with <see cref="Setup.AddTenantCache{TTenantCache}"/>.</summary>
public interface ITenantCache
{
    void Forget(Tenant tenant);
}
