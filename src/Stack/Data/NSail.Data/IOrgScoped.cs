// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>The org map's org-scoped bucket, stated in the entity's own file: this entity
/// records something that happened AT a branch (org-map.md), so the org filter is put on it —
/// over its own <c>OrganizationId</c> where it declares one, and over the head it rides where
/// it does not. Tenant-level and shared entities carry nothing and are read from every branch;
/// the tenant wall is the other axis and filters all three regardless (data-tenancy.md, The
/// tenant is a column).
///
/// <para>The marker is the classification, not the filter: what it decides is read once over
/// the whole model (<c>ApplySetups</c>), so no handler writes the WHERE and none can forget it.
/// A column is not the classification either — <c>Membership</c> and <c>Branding</c> carry
/// <c>OrganizationId</c> and are tenant-level, because the column says which branch the row is
/// ABOUT rather than where the row happened, and the rows that DEFINE the org graph are what
/// the scope is computed from.</para>
///
/// <para>An entity wearing this marker must be able to REACH the axis, or the model refuses to
/// build: its own <c>OrganizationId</c>, or a required reference to a head that carries one.
/// A rider states no reason of its own — it belongs where its head belongs.</para></summary>
public interface IOrgScoped
{
}
