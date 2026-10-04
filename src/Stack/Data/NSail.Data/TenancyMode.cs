// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>What an install states in <c>Tenancy:Mode</c>. A mode is a preset of the two
/// independent switches on <see cref="TenancyOptions"/> — never a thing the seam branches
/// on — so the quadrant no mode names (per-tenant connection AND per-tenant rows) stays
/// representable without being built.</summary>
public enum TenancyMode
{
    None,
    MultiDb,
    SingleDb,
}
