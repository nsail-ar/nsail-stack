// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>One row per seed step a tenant has run (<see cref="TenantSeedStep"/>) — the tenant's
/// own migration history, and the Stack's only table. It is written in the same transaction as
/// the rows its step wrote, so a step that threw is not recorded and is tried again on the next
/// touch.
///
/// <para>It only ever has rows where <c>Tenancy:Rows</c> scopes to the tenant: under every other
/// mode the install's EF chain is the truth and no first touch ever happens, so the table stays
/// empty.</para></summary>
public class AppliedSeedStep
{
    public Guid Id { get; set; }

    /// <summary>The step's name, compared byte for byte: it is an identifier the seed froze, not
    /// prose, and the unique index over it is what makes a step's second run impossible even
    /// where two processes touch one tenant at once.</summary>
    [CaseSensitive]
    public string Step { get; set; } = string.Empty;

    public DateTime AppliedAt { get; set; }
}
