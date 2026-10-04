// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>The rows a tenant cannot start without, planted on its first touch. Under a
/// per-tenant connection the migration chain carries them — a fresh database runs the app's own
/// <c>Seed</c>; under a shared one there is a single copy of that seed and it belongs to nobody,
/// so the tenant's own organization, administrator and whatever else is per-tenant by nature is
/// created here instead. The twin of the first-touch migration, and the app's to write: the
/// Stack has no rows of its own and kits do not seed (data.md, Seeding).
///
/// <para><b>The seed is a chain, like the migrations it is the twin of.</b> It is an ordered
/// list of named steps; the tenant's own history says which of them it has run
/// (<see cref="AppliedSeedStep"/>), and every touch runs the rest, in order. So the seed grows
/// by APPENDING a step — never by editing one that shipped — and a tenant created before a step
/// existed reaches it on the first touch after the deploy that added it. A step runs once per
/// tenant ever, which is what keeps a row the shop has since edited or deleted from being
/// resurrected.</para></summary>
public interface ITenantSeed
{
    IReadOnlyList<TenantSeedStep> Steps { get; }
}
