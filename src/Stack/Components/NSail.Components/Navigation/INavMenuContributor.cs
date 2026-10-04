// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>Contributes a module's entries to the navigation menu. Async so a future
/// implementation can filter by permissions or tenant.</summary>
public interface INavMenuContributor
{
    Task<IReadOnlyList<NavMenuItem>> GetItems();
}
