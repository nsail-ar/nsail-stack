// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

/// <summary>A contributor whose ask fails until it is told to stop failing — an API that was
/// not answering and then was.</summary>
public sealed class FailingNavContributor(params NavMenuItem[] items) : INavMenuContributor
{
    bool _failing = true;

    public int Calls { get; private set; }

    public Task<IReadOnlyList<NavMenuItem>> GetItems()
    {
        Calls++;

        if (_failing)
        {
            throw new InvalidOperationException("the API is not answering");
        }

        return Task.FromResult<IReadOnlyList<NavMenuItem>>(items);
    }

    public void Recover()
    {
        _failing = false;
    }
}
