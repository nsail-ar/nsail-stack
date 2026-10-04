// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Settings;

namespace NSail.Components;

/// <summary>What this install did to its own menu, read where the tree is built. It is not an
/// INavMenuContributor and must not become one: a contributor says what is on the table and the
/// contributors' order decides who overrides whom, while an arrangement says where what is on
/// the table sits — so NavMenu applies it AFTER every contributor by construction, and no
/// registration order can put an install's word under a module's.</summary>
public interface INavMenuArrangement
{
    Task<NavMenuArrangement> GetArrangement(CancellationToken cancellationToken = default);
}
