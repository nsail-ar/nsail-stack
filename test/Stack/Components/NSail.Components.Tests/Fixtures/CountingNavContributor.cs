// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

/// <summary>What a menu build costs, counted: a contributor call is a kit's opportunity to ask
/// its own server, so the number of calls is the number of round trips the drawer is worth.</summary>
public sealed class CountingNavContributor(params NavMenuItem[] items) : INavMenuContributor
{
    public int Calls { get; private set; }

    public Task<IReadOnlyList<NavMenuItem>> GetItems()
    {
        Calls++;

        return Task.FromResult<IReadOnlyList<NavMenuItem>>(items);
    }
}
