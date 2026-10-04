// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

/// <summary>A module's contribution to the nav menu, handed in literally instead of resolved:
/// what the menu is asked to render is the test's own knowledge, not a kit's.</summary>
public sealed class FixedNavContributor(params NavMenuItem[] items) : INavMenuContributor
{
    public Task<IReadOnlyList<NavMenuItem>> GetItems()
    {
        return Task.FromResult<IReadOnlyList<NavMenuItem>>(items);
    }
}
