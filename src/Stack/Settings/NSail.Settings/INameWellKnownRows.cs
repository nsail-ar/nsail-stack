// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Settings;

/// <summary>Settings that answer with the id of a row the install froze — the account a posting
/// lands on, the currency a price is in. Where the rows belong to a tenant rather than to the
/// install, that id names a row the tenant cannot see, so the manager that reads the settings
/// resolves every one of them through the tenancy seam before anybody acts on the value.
///
/// <para>The hook is here rather than at each reader because reading IS the boundary: a value
/// resolved once on the way in cannot be half-translated downstream, and a value the customer
/// chose itself is not a well-known row and comes back untouched.</para></summary>
public interface INameWellKnownRows
{
    void Resolve(Func<Guid, Guid> row);
}
