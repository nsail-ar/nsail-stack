// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>Reference data that belongs to the INSTALL and not to any tenant: the tenant column
/// is not put on it, no filter hides it, and nothing stamps it — every tenant sharing the
/// database reads the same rows. Tax rates, taxpayer categories, the kinds of address a state
/// names: rows a market decides and no customer can edit.
///
/// <para><b>An entity a tenant can WRITE is the tenant's, whatever it looks like.</b> The
/// editor's existence is the classification: under a per-tenant database every one of those rows
/// is already the customer's own, so marking it here would not share reference data, it would let
/// one customer rewrite everybody's. That is why roles, policies, currencies and voucher types
/// are not marked — the tenant's first touch copies them (<see cref="ITenantSeed"/>) and code
/// that names one by its frozen id resolves through
/// <see cref="TenancyProvider.Row(Guid)"/>.</para>
///
/// <para>The marker is the wall's whole exception list, and it is the entity's own file rather
/// than a roster somewhere, so adding one is a reviewable act. One law governs it, and
/// <c>TenantAxisTests</c> enforces the law rather than the list: <b>an install-scoped row may
/// never hold a required foreign key into a tenant-scoped table.</b> Shared pointing at private
/// is the wall crossed backwards — one shared template naming one customer's account. Private
/// pointing at shared is just using the furniture, and is free.</para></summary>
public interface IInstallScoped
{
}
